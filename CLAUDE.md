# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Admin Data Query API

`/DataQuery` (`Mercurius/Controllers/Admin/DataQueryController.cs`) runs raw SQL directly
against the live database — full read/write, key-gated. See `ADMIN_DATA_QUERY_ACCESS.md` at
the repo root for the key, request/response shape, and the auth sequence needed to call it
programmatically (it requires an authenticated admin session, not just the key). This is the
intended way to inspect/fix live data now that SQLite has no live remote-query path — prefer
it over ad-hoc scripts against the DB file, and note that unlike casual investigation, using
this endpoint to actually modify data is explicit, logged, deliberate action, not something
the "do not touch live database data" rule below is meant to block.

## Do not touch live database data

`appsettings.json` currently holds a real connection string to a live, remote SQL Server (site4now.net shared hosting) — this is not a disposable local dev database. Do not run anything that inserts, updates, deletes, or drops data/schema against it (including "just testing" seed/cleanup scripts, ad-hoc `SqlConnection`/`sqlcmd` commands, or destructive EF Core operations) without the user explicitly asking for that specific action first. Read-only queries against it are fine.

## Commands

There is no `.sln` file — build/run projects individually; MSBuild resolves `ProjectReference`s automatically.

```
# Build the web app (also builds Mercurius.Common and Mercurius.Repo transitively)
cd Mercurius && dotnet build

# Run it (serves http://localhost:5094 in the Development environment)
cd Mercurius && dotnet run --no-build

# Run the full test suite
cd Mercurius.Tests && dotnet test

# Run a single test
cd Mercurius.Tests && dotnet test --filter "FullyQualifiedName~EfRepositoryTests.AddAsync_ShouldInsertProduct"

# Run the browser-driven end-to-end tests (see "End-to-end tests" below) — requires
# Mercurius to already be built once
cd Mercurius && dotnet build
cd Mercurius.E2ETests && dotnet test
```

All four projects target `net9.0` with `TreatWarningsAsErrors=true` — a nullable-reference warning fails the build, not just `dotnet build` locally.

If a `dotnet run` process is left running, a subsequent `dotnet build` will fail with `MSB3027`/file-lock errors on `Mercurius.Common.dll`/`Mercurius.Repo.dll`; stop the running `Mercurius` process first.

## Architecture

**Projects**: `Mercurius` (ASP.NET Core MVC web app, entry point), `Mercurius.Repo` (EF Core persistence + domain models), `Mercurius.Common` (cross-cutting constants, `ModuleRegistry`, shared helpers), `Mercurius.Tests` (xUnit).

### Persistence is provider-switchable

`Program.cs` reads `DatabaseProvider` from configuration (`"Sqlite"` default, or `"SqlServer"`) and configures `MercuriusDbContext` (in `Mercurius.Repo/Repositories/`) accordingly. `MercuriusDbContext.OnModelCreating` does several things that apply regardless of provider and are easy to miss:

- Forces every `string` property without an explicit `[Required]` attribute to be nullable at the EF model level, overriding the convention that infers `NOT NULL` from C#'s non-nullable-reference-type annotation. This mirrors `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` in `Program.cs`'s MVC config — the two must stay in sync, since many models declare optional fields (`Description`, `Note`, etc.) as non-nullable `string` for convenience.
- Sets `DeleteBehavior.Restrict` on every foreign key, globally overriding EF's cascade-by-default convention for required relationships. SQL Server refuses to create a schema where two cascade paths converge on the same table (e.g. `InvoiceStatus` → `Invoice` → `InvoiceItem` and `InvoiceStatus` → `InvoiceItem` directly) — SQLite never enforced this, so the failure only surfaces against SQL Server.
- Gives every `decimal`/`decimal?` property explicit `HasPrecision(18, 4)` — SQL Server requires an explicit precision/scale or defaults silently; SQLite doesn't care.
- Mirrors the index list from the old LiteDB implementation (see `EnsureCoreIndexes`-style grouping by entity) directly as `HasIndex` calls.

### Schema changes go through EF Core Migrations (Sqlite only)

`Mercurius.Repo/Migrations/` holds versioned migrations, applied via `Database.Migrate()` at startup in `Program.cs` — this preserves existing data, unlike the `EnsureCreated()` approach used earlier in this project's history. The `SqlServer` provider path still calls `EnsureCreated()` instead: that provider is currently dormant (no live SQL Server database exists), so a second migrations assembly for it isn't worth building until it's back in active use.

To add a schema change: edit the model, then from `Mercurius/`, run:
```
dotnet-ef migrations add <Name> --project "..\Mercurius.Repo\Mercurius.Repo.csproj" --startup-project "Mercurius.csproj" --output-dir Migrations
```
(Use the `dotnet-ef` executable directly, not `dotnet ef` — a stray user-level tool manifest at `C:\Users\<user>\.config\dotnet-tools.json` can make the local-manifest resolution fail with "Run dotnet tool restore" even though the global tool works fine.) Commit the generated migration; it applies automatically on next startup via `Migrate()`, both locally and on publish to the live site.

The live database was migrated onto this system by wiping its schema (via the Admin Data Query API) and letting `EnsureCreated()` rebuild it one last time, then manually inserting a row into `__EFMigrationsHistory` to mark the generated `InitialCreate` migration as already applied — this let migrations take over without redundantly recreating a schema that already matched. That was a one-time bootstrap; it should never need repeating.

### Mobile sync API (`Api/AuthController`, `Api/SyncController`)

`Product`, `Invoice`, and `InvoiceItem` each carry an additive `SyncId` (Guid) column — a stable
cross-device identity, independent of the server's int `Id`, for the offline-first `Mercurius.Mobile`
MAUI app being built alongside this repo. `Api/AuthController.Login` issues a JWT (a second auth
scheme registered alongside Identity's cookie scheme in `Program.cs`; `Jwt:Key`/`Issuer`/`Audience` in
config) — note it deliberately does not gate on `MercuriusUser.IsActive`, since that field isn't
enforced anywhere else in the app either (it's display-only), so checking it here would be an
inconsistent, surprise restriction. `Api/SyncController` (`[Authorize(AuthenticationSchemes =
JwtBearerDefaults.AuthenticationScheme)]`) exposes `GET .../pull?since=<utc timestamp>` and `POST
.../push` for `products` and `invoices`:

- **Products** fully support create-or-update via push (matched by `SyncId`).
- **Invoices** are create-once/idempotent by `SyncId` — a synced sale is a completed financial
  record; re-pushing the same `SyncId` is a safe no-op rather than overwriting it. Edits to a
  submitted sale should go through a refund/void flow, not sync.
- Conflict resolution is last-write-wins using the **server's own clock**, never a client-supplied
  timestamp — `UpdatedDate`/`CreatedDate` are always stamped server-side on write acceptance, whether
  the write came from the admin UI or a sync push. Business-meaning dates the client actually observed
  (e.g. `InvoiceDate`) are still trusted from the client.
- Pull cursors use `>=`, not `>`, so a record modified at exactly `since` is returned again rather
  than silently skipped — harmless, since push/pull both upsert by `SyncId`.

No refresh-token flow exists yet (access tokens are simply long-lived, `Jwt:AccessTokenDays`); add one
if/when token revocation or shorter lifetimes become a real requirement.

### End-to-end tests (`Mercurius.E2ETests`)

`Mercurius.Tests` only covers the repository/data layer — nothing in it ever drives an actual HTTP
request through a controller, which is how the checkout bug above went unnoticed for as long as it
did. `Mercurius.E2ETests` (Playwright + xUnit) closes that gap by driving a real headless Chromium
browser against a real instance of the app:

- `Infrastructure/TestServerFixture` launches the **already-built** `Mercurius.dll` (it does not
  rebuild) on a free port, against a throwaway SQLite database unique to the run
  (`MERCURIUS_DB=e2e_<guid>`, deleted on teardown) — never the developer's own `mercurius.sqlite`,
  and never the live site. `ASPNETCORE_ENVIRONMENT` is forced to `Development` so
  `appsettings.Development.json`'s seed admin credentials actually load (the base
  `appsettings.json` ships those blank on purpose — see the secrets-splitting note below).
- One shared server + one shared Chromium instance run for the whole test run
  (`MercuriusCollectionFixture`); each test method gets its own `IBrowserContext`/`IPage` (its own
  cookies/session) via `MercuriusTestBase`. Tests run sequentially, not in parallel
  (`xunit.runner.json`), since they all share one database.
- Before running for the first time: build `Mercurius` (`dotnet build`, not `dotnet run` — the
  fixture execs the DLL directly) and install the Chromium binary once via the packaged script,
  e.g. `pwsh Mercurius.E2ETests/bin/Debug/net9.0/playwright.ps1 install chromium`.
- `SalesTests.CompleteSale_SucceedsAndRedirectsToInvoiceList` and `RolesManagerTests` exist
  specifically to catch the two production bugs above if they're ever reintroduced — don't remove
  or weaken them without a good reason.
- Gotcha hit while writing these: DataTables' `#searchInput` boxes only react to a real `keyup`
  DOM event — Playwright's `FillAsync` sets the value directly without dispatching one, so searches
  silently no-op. Use `Locator.PressSequentiallyAsync` for those. Also, an ajax-form's success
  redirect can't be awaited with `WaitForURLAsync(url => url.Contains(...))` if you're already
  sitting on a URL that contains that same substring — it resolves before the underlying fetch
  even finishes. Wait for the POST's own response instead (`RunAndWaitForResponseAsync`).
- Set `E2E_HEADED=1` before `dotnet test` to watch the browser locally instead of running headless
  (also slows actions down via `SlowMo` so it's actually followable).
- The test server's full stdout/stderr (including any unhandled-exception dumps from
  `ExceptionHandlingMiddleware`) is mirrored to `%TEMP%\mercurius-e2e-server.log` on every run —
  check it first when a test fails with no obvious cause from the Playwright error alone.
- A subtle timing trap worth knowing about: waiting for a POST's own `Response` event
  (`RunAndWaitForResponseAsync`) is *not* the same as waiting for the page that POST redirects to.
  For an ajax-form, the response arrives before the page's own `.then()` callback has run
  `window.location.href = ...`; for a plain form that 302s, the response for the POST can arrive
  before the browser finishes following the redirect. Either way, code that immediately calls
  `WaitForLoadStateAsync` afterward can resolve against the *old* page. Wait for a real DOM signal
  instead — a modal closing, a button/row re-rendering — see `TestDataHelpers.CreateTestProductAsync`
  and `ShipmentTests.CreateAndApprovePurchaseOrderAsync` for two different flavors of this.
- `PurchaseOrderTests` caught a fourth production bug: `PurchaseOrder.OrderNumber` was `[Required]`
  but is always server-generated in `PurchaseOrdersController.Create` *after* `ModelState` is
  already validated — the Create form has no field for it, so it bound to `""`, failed `[Required]`
  before the controller ever ran, and silently re-rendered the Create page with no visible error
  (that view has no validation summary). Creating a Purchase Order via the web UI had likely never
  worked. Fixed by removing `[Required]` from that property (it was never meant to validate
  user input in the first place) — see the `MakePurchaseOrderNumberNullable` migration.
- `ShipmentTests` caught two more, both in the "receive a shipment against a PO" flow — confirmed
  zero `ShipmentArrival` rows existed live, so this feature had never worked either:
  1. `ShipmentArrivalStatuses` was never seeded, and `ShipmentArrival.ShipmentArrivalStatus` is a
     required relationship — same class of bug as `InvoiceStatuses`. Fixed by seeding it in
     `Program.cs` (`Pending`/`Received`/`Delayed`/`Damaged`).
  2. `Views/Shipment/Create.cshtml`'s script called `.trigger('change')` on the pre-selected PO
     dropdown *before* registering the `.on('change', ...)` handler that actually auto-fills the
     supplier — so clicking "Receive" from an approved PO always left the supplier field stuck on
     its placeholder. Fixed by reordering the registration before the trigger.
  3. `ShipmentArrival.PurchaseOrderId` (an `int?`) was decorated with `[StringLength(500)]` — an
     attribute that only applies to strings. The moment it actually held a value, ASP.NET Core's
     validator threw `InvalidCastException` trying to cast the boxed int to a string, taking down
     the whole request. Fixed by removing the attribute.

### FIFO batch pricing (`BatchPricingService`)

`MedicineBatch` rows (created via `MedicineBatchesController`, one per stock receipt) each carry their own `UnitCost`/`UnitSalePrice`. A new shipment at a different price is just a new batch queued behind older stock — `SalesController.NewSale` calls `BatchPricingService.FindFulfillingBatchAsync` to pick, oldest first, the first batch whose `RemainingQuantity` alone covers the full line quantity; if the oldest batch falls short, the *whole* line is priced and drawn from the next batch that can cover it rather than splitting across two batches' ledgers (a deliberate simplification, confirmed with the product owner). `InvoiceItem.MedicineBatchId` records which batch a line was actually priced/deducted from, for refund credit and lot-recall tracing. Products with no batches at all (most non-drug items) fall back to `Product.CurrentSalePrice`/`CurrentCostPrice` unchanged — this feature is purely additive. `BatchPricingService.RefreshActivePriceAsync` keeps those flat fields in sync with whichever batch is now the oldest non-depleted one, so every screen that reads them (Products list, DataTables, the mobile sync API) keeps working without knowing batches exist.

### Repository pattern — controllers never touch `DbContext` directly

`IUnitOfWork` / `IRepository<T>` (in `Mercurius.Repo/Repositories/`) wrap `MercuriusDbContext`; controllers depend on `IUnitOfWork` (scoped per request), never the context. `IRepository<T>` covers standard CRUD/paging. For DataTables-driven list pages (server-side sort/filter/paging), ~12 controllers instead call `IUnitOfWork.Query<T>()` (returns `IQueryable<T>` straight off the `DbSet`) and compose `.Where()`/`.OrderBy()`/`.Skip()`/`.Take()` directly — this is the pattern to follow for any new paged list page, mirroring `ProductsController.DataTable` as the reference implementation.

Nothing persists until `IUnitOfWork.SaveChangesAsync()` is called — every `AddAsync`/`UpdateAsync`/`DeleteAsync` call must be paired with an explicit save afterward.

### Database Backups feature is SQLite-only

`DatabaseBackupService` (a `BackgroundService` in `Mercurius/Services/`) takes a daily `VACUUM INTO` snapshot of the SQLite file and prunes anything older than `DatabaseBackup:RetentionDays`, hard-capped at 10 days. It's only registered as a hosted service — and the "Database Backups" sidebar link / controller only reachable — when `DatabaseProvider` is `Sqlite`; there's no equivalent under SQL Server (shared hosting typically blocks `BACKUP DATABASE` at the OS level, and hosting providers manage their own backups anyway).

### Authorization is claim-driven from a single registry

`Mercurius.Common.ModuleRegistry` defines every permission as a `Pages.*` string constant, plus a `Modules` array listing all of them. `Program.cs` turns that array into one ASP.NET Core authorization policy per entry (`options.AddPolicy(module, policy => policy.RequireClaim(...))`) and, at seed time, grants every one of them to the `Administrator` role. Adding a new protected page means: add the constant, add it to `Modules`, gate the controller with `[Authorize(Policy = ModuleRegistry.Pages.X)]`, and (if it should appear in the sidebar) add a `CanView*` property to `SidebarViewComponent`/`SidebarViewModel` and a guarded link in `Views/Shared/Components/Sidebar/Default.cshtml`.

### Soft delete, not hard delete

"Delete" actions should set `IsActive = false` via `UpdateAsync`, not remove the row via `DeleteAsync` — this is the established convention for master-data controllers (e.g. `SuppliersController`). Some older controllers still hard-delete; when touching them, bring them in line rather than adding new hard-deletes. A model being soft-deletable requires an `IsActive` column — not every entity has one (e.g. `Color`, `Size`, `Location` currently don't).

### Startup seeding

`Program.cs`'s `SeedDataAsync` runs on every startup (idempotent — checks before creating): the three fixed roles, all `ModuleRegistry.Modules` claims on `Administrator`, a default `Address`/`ContactInformation`/`Location`, the admin user from `SeedAdmin:Email`/`SeedAdmin:Password` config, pharmacy reference data (`PharmacySeedData`: product categories, per-category custom fields, dosage forms), and `InvoiceStatuses` (from the `StatusCollection.InvoiceStatus` enum — `Invoice.StatusId`/`InvoiceItem.StatusId` are required FKs to this table; it was missing entirely until this was added, which meant creating any invoice on a freshly created database failed with a FOREIGN KEY constraint violation).

### Legacy artifacts — do not treat as current

`Mercurius.Repo/DbCreateScripts/*.sql` and `Mercurius.Repo/Scripts/Demo_intial.sql` are dumps from a prior SQL Server-based version of this app (before a LiteDB port, before the current EF Core setup) — historical only, not read by any code path. `Properties/PublishProfiles/*.pubxml` and `site1.PublishSettings` are Web Deploy profiles for a site4now.net shared-hosting target.
