# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Admin Data Query API

The `/data-query` page, backed by `POST api/admin/data-query` (`DataQueryApiController` in
`Mercurius/Controllers/Api/AdminToolsApiControllers.cs`, rules in `DataQueryService`), runs raw
SQL directly against the live database — full read/write, key-gated. See
`ADMIN_DATA_QUERY_ACCESS.md` at the repo root for the key and how to call it programmatically
(written for the old MVC `/DataQuery` endpoint, so check its request details against the API
controller) (it requires an authenticated admin session, not just the key). This is the
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

**Projects**: `Mercurius` (ASP.NET Core host, entry point: the `/api` controllers, the sign-in/error pages in `Components/`, and prerendering for the Blazor client), `Mercurius.Client` (the web UI, Blazor WebAssembly + MudBlazor, served from the site root), `Mercurius.Shared` (DTOs shared by server and client, plus `ModuleRegistry`), `Mercurius.Repo` (EF Core persistence + domain models), `Mercurius.Common` (cross-cutting constants, shared helpers), `Mercurius.Tests` (xUnit).

### Persistence is provider-switchable

`Program.cs` reads `DatabaseProvider` from configuration (`"Sqlite"` default, or `"SqlServer"`) and configures `MercuriusDbContext` (in `Mercurius.Repo/Repositories/`) accordingly. `MercuriusDbContext.OnModelCreating` does several things that apply regardless of provider and are easy to miss:

- Forces every `string` property without an explicit `[Required]` attribute to be nullable at the EF model level, overriding the convention that infers `NOT NULL` from C#'s non-nullable-reference-type annotation. This mirrors `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` in `Program.cs`'s `AddControllers` config — the two must stay in sync, since many models declare optional fields (`Description`, `Note`, etc.) as non-nullable `string` for convenience.
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

- **Products** fully support create-or-update via push (matched by `SyncId`) — the mobile app's
  Products page can add and edit products. On *update*, `CurrentStock` from the device is ignored
  (a device's copy can be stale; stock only moves via sales/adjustments/shipments); it's only
  accepted on create, as opening stock. `Barcode`: `null` in a push means "keep the server's"
  (older app builds), `""` means "cleared".
- **Invoices** are create-once/idempotent by `SyncId` — a synced sale is a completed financial
  record; re-pushing the same `SyncId` is a safe no-op rather than overwriting it. Edits to a
  submitted sale should go through a refund/void flow, not sync. `PushInvoices` and web checkout
  (`SalesApiController`) record every line through the same `SaleRecorder` (`Mercurius/Services/`):
  it picks the batch (`BatchPricingService.FindFulfillingBatchAsync`), takes the line's cost from
  that batch, decrements `MedicineBatch.RemainingQuantity`/`Product.CurrentStock`, writes a
  `ZeroStockSaleAuditLog` row when stock goes to zero or below, and refreshes the active price
  afterward. **Pricing rule for both tills:** a sale the customer has already paid keeps the price
  the till charged (a mobile push's `SalePrice`, a web offline sale's `CartLineRequest.UnitPrice`);
  a web sale charged online is priced by the server, and if the ticket showed a different unit
  price checkout returns 409 with the current prices and saves nothing, so the cashier re-quotes
  before charging. The device's `CostPrice` is ignored (cost is server bookkeeping). The payment
  method is stored in `Invoice.PaymentMethod` (`Cash`/`Card`); older tills only wrote it into
  `Notes` ("Paid by card"), which `PaymentMethods.Normalize` falls back to.
- **Permissions on push.** Mobile JWTs carry no `access.pages` claims, so `SyncController` reads the
  user's role permissions from the database (`UserPermissions`): invoice push needs
  `NEWSALE_CREATE`, product push needs `PRODUCTS_CREATE` (new) or `PRODUCTS_EDIT` (existing).
  A refusal is a per-item error, so the device keeps the record queued and shows the reason.
- Conflict resolution is last-write-wins using the **server's own clock**, never a client-supplied
  timestamp — `UpdatedDate`/`CreatedDate` are always stamped server-side on write acceptance, whether
  the write came from the admin UI or a sync push. Business-meaning dates the client actually observed
  (e.g. `InvoiceDate`) are still trusted from the client.
- Pull cursors use `>=`, not `>`, so a record modified at exactly `since` is returned again rather
  than silently skipped — harmless, since push/pull both upsert by `SyncId`.

No refresh-token flow exists yet (access tokens are simply long-lived, `Jwt:AccessTokenDays`); add one
if/when token revocation or shorter lifetimes become a real requirement.

**Invoice push guarantees** (`PushInvoices`): each sale is its own transaction (a failed line rolls the
whole sale back — previously the header committed first, and since a re-push of an existing SyncId is a
no-op, a failed line was lost forever); a line whose product isn't on the server yet fails the sale
(retryable) instead of being silently dropped; invoice numbers are computed per sale with a SyncId
suffix (`INV-<ms>-XXXX`) — one timestamp per batch used to give every sale in an offline backlog the same
number, violating the unique index and failing them all, forever. `EfUnitOfWork.RollbackTransactionAsync`
also clears the change tracker so a rolled-back sale isn't re-attempted by the next save. The device may
send `CashierUserId` (who rang it up); it's only trusted if that user is in the pusher's tenant.

**Receipts pull** (`GET api/sync/receipts/pull?cursor&fromDate&take`): every sale in the tenant (all
terminals + web), display-ready (branch/cashier/channel/terminal names, line items with product names and
refunded quantities). Keyset-paged on (last-modified, Id) — a plain timestamp cursor either skips rows
sharing the boundary timestamp or loops on them. `InvoiceChangeStampInterceptor` (registered before the
audit interceptor) bumps `Invoice.UpdatedDate` whenever the invoice, its items, or its refund rows change —
refunds never touched it before, so devices pulling "changed since" would never have seen them.
`Invoice.SourceChannel`/`SourceDeviceId`/`SourceDeviceName` record where a sale was rung up (web
checkouts also now record `CreatedBy`, which they never did).

**Mobile sync strategy** (`Mercurius.Mobile/Services/SyncCoordinator.cs`): every trigger — login,
checkout, Receipts page, a 2-minute timer, connectivity regained, app resumed — goes through one
single-flight pass: push products → push sales → pull products → pull receipts (30-day window, cursor
persisted per page, synced rows older than the window pruned). Pages must not call `SyncService`
directly (concurrent passes can double-push). Local `Sales` is one table for this device's sales and
everyone else's, keyed by SyncId: a pulled copy overwrites the local one and marks it synced (recovering
pushes whose response was lost); unsynced rows are never overwritten, pruned, or deleted — logout
(`ClearAllAsync`) keeps them, and `AppShell` warns before logging out with anything unsent.
Android Back on a top-level page backgrounds the app instead of finishing the activity (finishing crashed
MAUI when a text field was focused, and closing a till on one stray press is wrong anyway).

### Mobile selling flow (`SalesPage`, `PaymentPage`, `ReceiptsPage`)

The mobile app's point-of-sale screen (`SalesPage`, the first Shell flyout item — the app now
lands here after login, not on the Dashboard) is modeled on Loyverse's POS: category tabs, a
product tile grid (or list — toggle in the header) on the left, a running "Ticket" sidebar on the
right with qty steppers and a Charge button. Tapping Charge pushes `PaymentPage` (cash-received
with quick round-amount buttons, or a Card option) modally and awaits a `PaymentResult`; on
confirmation `SalesPage` writes a `LocalSale` + `LocalSaleItem` rows via
`LocalDatabase.SaveSaleAsync` — which also optimistically decrements the matching `LocalProduct.CurrentStock`
rows in the same transaction, so the tile grid reflects the sale immediately — then
best-effort calls `SyncService.SyncSalesAsync()` to push it. If that fails (offline, server down),
the sale simply stays in the local queue (`LocalSale.IsSynced == false`) and is retried on the next
sync attempt from anywhere in the app; nothing about checkout itself requires connectivity.
`ReceiptsPage` lists every sale the device knows about — its own (including ones still waiting to
upload) plus every other terminal's and the web's, pulled via the receipts endpoint below — filterable
by scope (this device / this branch / all branches), period, and search, with refund/void status and
where each sale came from (terminal names are set per device in Settings). Server-side stock/batch bookkeeping
for a pushed sale lives in `SyncController.PushInvoices` (see above), not in the mobile client.

`LoginPage.xaml.cs` calls `SyncService.SyncProductsAsync()` right after a successful login, before
handing off to `AppShell` — a login already proves connectivity, so this is the one guaranteed
moment to refresh the local product catalog. Without it, a device that just logged in (or logged
out and back in, which wipes the local cache via `AppShell.xaml.cs`'s `ClearAllAsync`) landed on an
empty Sell page with no obvious way to know why, until the cashier happened to visit
Dashboard/Products and hit "Sync Now" manually.

`SalesPage`'s cart (`_cart`) is an `ObservableCollection<CartLine>` bound once to
`CartCollectionView`, and `CartLine.Quantity`'s setter raises `PropertyChanged` — a quantity bump
(tile re-tap, the ticket's own +/- steppers) updates the existing bound cell directly rather than
resetting the whole `ItemsSource`. Only an actual add/remove-line change touches the collection
itself (`Add`/`Remove`/`Clear`), which `ObservableCollection` already surfaces to the
`CollectionView` via `INotifyCollectionChanged` — no manual re-render call needed anywhere.

**Real platform bug hit while building this**: `Microsoft.Maui.Controls.Border` silently drops its
entire content (not just the stroke — everything inside it) on Android when given a one-sided,
non-uniform `StrokeThickness` (e.g. `"1,0,0,0"` for a left-only divider). It fails identically
regardless of what's inside (confirmed down to a bare `BoxView`), survives a full clean rebuild, and
throws nothing to logcat — it just silently renders as an empty box. `Border` instances with a
uniform/default `StrokeThickness` (including `Stroke="Transparent"`) are unaffected. The fix used
throughout `SalesPage.xaml`/`ReceiptsPage.xaml` is to replace the outer `Border` with a plain `Grid`
plus a separate hairline `BoxView` positioned at the edge that needs the divider. If a future page
adds a `Border` with an asymmetric `StrokeThickness` and its content mysteriously doesn't render on
Android, this is almost certainly why.

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
  e.g. `pwsh Mercurius.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium`.
- Every test drives the Blazor UI through `Infrastructure/BlazorTestBase` (see the Blazor client
  section below for its helpers and MudBlazor gotchas). The suite was first written against the
  classic MVC pages; those pages are gone and the tests were ported, keeping their names and the
  bugs they guard against.
- `SalesTests.CompleteSale_ViaCardPayment_ClearsCartAndShowsConfirmation` and `RolesManagerTests`
  exist specifically to catch the two production bugs above if they're ever reintroduced — don't
  remove or weaken them without a good reason.
- Set `E2E_HEADED=1` before `dotnet test` to watch the browser locally instead of running headless
  (also slows actions down via `SlowMo` so it's actually followable).
- The test server's full stdout/stderr (including any unhandled-exception dumps from
  `ExceptionHandlingMiddleware`) is mirrored to `%TEMP%\mercurius-e2e-server.log` on every run —
  check it first when a test fails with no obvious cause from the Playwright error alone.
- Wait for a real DOM signal (a dialog closing, a row or toast appearing, the URL changing to the
  new record) rather than a network response or load state — a response can arrive before the page
  has re-rendered. `BlazorTestBase.CreateProductViaFormAsync` and
  `ShipmentTests.CreateAndApprovePurchaseOrderAsync` show the pattern.
- The bugs below were caught by this suite while it still drove the classic MVC pages. The classic
  views and controllers named in them no longer exist; the model, seeding and validation fixes
  still apply.
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

### Mobile end-to-end tests (`Mercurius.Mobile.UITests`)

Appium + UiAutomator2 drives the actual Android app (emulator or device) — the mobile
equivalent of `Mercurius.E2ETests`, covering login/logout, search, cart add/remove/increment/
decrement, checkout + sync, and the grid/list view toggle. Unlike the web suite, this one drives
a real, persistent app installation rather than a fresh throwaway instance per run — there is no
"launch a fresh copy" primitive for a MAUI app the way `TestServerFixture` execs a fresh
`Mercurius.dll`.

**One-time setup**: `npm install -g appium`, then `appium driver install uiautomator2`. Requires
`ANDROID_HOME`/`JAVA_HOME` set and a running emulator (`adb devices` should show one) or attached
device.

**Before running**: the app must already be built and installed — this suite only launches and
drives whatever's on the device, it doesn't build/deploy (mirrors `Mercurius.E2ETests` execing an
already-built `Mercurius.dll`). Build/deploy with `dotnet build -f net10.0-android -t:Run -c
Debug` from `Mercurius.Mobile/`. Then, from `Mercurius.Mobile.UITests/`: `dotnet test` (or
`--filter "FullyQualifiedName~ClassName"` for one class).

**Pointing the app at a local dev server for controlled test data**: `Mercurius.Mobile/
Configuration/ApiConfig.cs`'s `BaseUrl` normally points at the live site (see the comment there).
For this suite, change it to `http://10.0.2.2:5094` — the Android emulator's host-loopback
alias for the dev machine's own `localhost` — and run the web app with
`dotnet run --urls "http://0.0.0.0:5094"` (binding to `0.0.0.0`, not just `localhost`, is required
for the emulator to reach it; `ASPNETCORE_URLS` alone gets silently overridden by
`launchSettings.json`). `Platforms/Android/Resources/xml/network_security_config.xml` already
has a cleartext-traffic exception for `10.0.2.2` alongside the live host's, so no further
Android-side config is needed. **Revert `BaseUrl` back to the live URL before shipping** — don't
leave a debug build pointed at the local server.

**Deterministic test data**: two products exist in the local dev database specifically for this
suite — `E2E Widget Alpha` (`E2E-ALPHA-001`, ₱25.00, 50 in stock, non-low) and `E2E Widget Beta`
(`E2E-BETA-002`, ₱12.50, 0 in stock, low). `CheckoutAndSyncTests` performs one REAL sale against
whichever server `BaseUrl` points at, permanently decrementing Alpha's stock by 1 — re-run the
baseline reset below before every full-suite run, via the Admin Data Query API (local dev key:
`local-dev-only-key-not-a-secret`, see `ADMIN_DATA_QUERY_ACCESS.md` for the auth dance):
```sql
UPDATE Products SET CurrentSalePrice = 25.00, MarkUpPercentage = 150, CurrentStock = 50 WHERE ProductCode = 'E2E-ALPHA-001';
UPDATE Products SET CurrentSalePrice = 12.50, MarkUpPercentage = 150, CurrentStock = 0  WHERE ProductCode = 'E2E-BETA-002';
```
(Setting `CurrentSalePrice` directly, rather than through the Create-product UI form, sidesteps a
real bug found while seeding these products: the Create modal's own JS recalculates Sale Price
from Cost × Markup whenever Markup changes, so filling Sale Price *before* Markup lets that
auto-calc silently overwrite it back to the cost price.)

**Gotchas found building this suite** (each cost real debugging time — the fixes are already in
`Infrastructure/MercuriusMobileTestBase.cs`, but the *reasoning* is worth knowing before "fixing"
something that looks wrong):
- **AutomationId maps to Android's `resource-id`, not `content-desc`.** The commonly-assumed
  mapping (`content-desc`) comes back empty for every element in this app — confirmed via
  `adb shell uiautomator dump`. The working locator is
  `//*[contains(@resource-id, ':id/{automationId}')]` (formatted as `<package>:id/<AutomationId>`;
  `contains()` avoids hardcoding the package name). Native `AlertDialog` buttons (from
  `DisplayAlertAsync`) are the exception — they only expose their label via the standard `text`
  attribute, so those need a by-text XPath instead (`WaitForText`).
- **A field's leftover on-screen keyboard can cover the next field.** Tapping straight into a
  second field (e.g. Password right after Email) can land the tap on the still-open keyboard
  sitting on top of it, not the field underneath — confirmed by a screenshot showing the cursor
  still active in the first field. Both `SetText` and `Tap` call `Driver.HideKeyboard()` first
  (guarded by `IsKeyboardShown()`) for this reason.
- **A native `AlertDialog`'s title and its own accept button can share the same text.**
  `AppShell.xaml.cs`'s logout confirmation dialog is titled "Log Out" with a "Log Out" button —
  `WaitForText("Log Out").Click()` matches the (non-clickable) title first, since it's earlier in
  document order, and the dialog never closes. Standard Android `AlertDialog` positive buttons
  always carry resource-id `android:id/button1` — target that directly instead of by text.
- **A persisted login session survives a force-stop/relaunch, silently skipping the post-login
  sync.** `AndroidAppFixture` deliberately force-stops rather than `pm clear`s between runs (see
  below), which means a session from a previous run can carry into the next one, so
  `EnsureLoggedIn()` lands straight on the Sell page without ever calling `LoginAsAdmin()`. If
  you've just reset test data server-side, the device's local cache won't see it until an actual
  login re-triggers `LoginPage.xaml.cs`'s sync — `AndroidAppFixture.EnsureFreshSessionAsync` logs
  out via the UI at the start of every suite run specifically to guarantee this, but a manual
  interactive test session (e.g. debugging one test at a time from an IDE) doesn't get that for
  free.
- **`pm clear` corrupts this Debug build.** It's deployed via .NET for Android's Fast Deployment
  (assemblies pushed to `files/.__override__/<abi>` by `dotnet build -t:Run`, not embedded in the
  APK) — clearing app data wipes that directory too, and the app fatally aborts on next launch
  ("No assemblies found ... Exiting..."). Recovering requires a full clean rebuild+redeploy
  (`dotnet clean` the `net10.0-android` output, then `dotnet build -f net10.0-android -t:Run`) or
  an explicit uninstall + reinstall of the APK — a plain incremental `-t:Run` after the corruption
  is not enough to fix it.
- **A raw `adb shell uiautomator dump` cannot run while an Appium/UiAutomator2 session is
  attached** — both need exclusive use of the same on-device accessibility-service instrumentation.
  It silently fails ("No such file or directory") rather than erroring clearly. For live debugging
  during an active test run, use the test's own driver (`Driver.PageSource`/
  `Driver.GetScreenshot()`) instead of a competing adb call — `WaitForId`/`WaitForText`/
  `WaitForIdText` already dump both to `%TEMP%` (`mercurius-pagesource-*`/`mercurius-screenshot-*`)
  on a genuine timeout, which is usually enough to tell a real bug apart from ordinary slowness.
- **Known flaky area, not yet root-caused**: `CartTests`' rapid decrement-stepper taps
  (`Decrement_<line>`) occasionally land on `ClearButton` instead, wiping the whole cart rather
  than decrementing one line — reproduced with `SalesPage`'s cart backed by both a plain `List`
  and an `ObservableCollection`, so it isn't (only) the "reset ItemsSource" pattern that was the
  first suspect. Page-source dumps from a related failure (`SalesPageTests`' "search narrows to
  zero results" case) show the Ticket panel's `Grid RowDefinitions="Auto,*,Auto"` losing entire
  rows (the empty-state message, and separately the Total/Clear/Charge row) from the accessibility
  tree — not off-screen, not zero-size, genuinely absent — suggesting Android's CollectionView can
  cause that Grid's `*` row to momentarily collapse and reflow during an item-count transition,
  which would explain a stale tap coordinate landing on a since-relocated button. Generous settle
  delays between rapid cart taps (`Thread.Sleep(1500)` in `CartTests`) reduce but don't eliminate
  it. A real fix means finding why that Grid's rows collapse, not just waiting longer.

### Windows mobile end-to-end tests (`Mercurius.Mobile.UITests.Windows`)

WinAppDriver drives the actual `Mercurius.Mobile` Windows build (WinUI3, `net10.0-windows10.0.19041.0`)
— the Windows counterpart to `Mercurius.Mobile.UITests`' Appium/UiAutomator2 suite, covering the
same flows (login/logout, search, cart, checkout + sync, grid/list toggle). Unlike the Android
suite, `WindowsAppFixture` launches a genuinely fresh process every run (`AppiumOptions.App` points
straight at the built `.exe`) rather than attaching to an already-running, manually-launched
instance — there's no Fast-Deployment/`pm clear` complication on this platform, so a cold launch is
just the standard thing to do.

**One-time setup**:
1. `winget install --id Microsoft.WindowsApplicationDriver` (needs interactive UAC approval — if it
   hangs, that's why; approve the prompt or run from an already-elevated shell).
2. Enable Windows **Developer Mode**: Settings → Privacy & security → For developers → toggle
   "Developer Mode" on (also needs UAC approval; can't be flipped from an unelevated shell). Without
   this, WinAppDriver starts and immediately exits with `Failed to initialize: 0x80004005` — confirmed
   by actually trying to run this suite: `%TEMP%\mercurius-winappdriver-server.log` read
   `Developer mode is not enabled. Enable it through Settings and restart Windows Application Driver`.
   There is no unelevated or scripted workaround for this step.

**Before running**: build the app first — this suite only launches whatever's already on disk, it
doesn't build/deploy (mirrors the Android suite execing an already-built app, and
`Mercurius.E2ETests` execing an already-built `Mercurius.dll`):
```
dotnet build -f net10.0-windows10.0.19041.0 -c Debug   # from Mercurius.Mobile/
```
Then, from `Mercurius.Mobile.UITests.Windows/`: `dotnet test` (or
`--filter "FullyQualifiedName~ClassName"` for one class).

**Pointing the app at a local dev server for controlled test data**: unlike the Android emulator
(which needs the `10.0.2.2` host-loopback alias), the Windows build runs natively on the same
machine as the dev server, so `Mercurius.Mobile/Configuration/ApiConfig.cs`'s `BaseUrl` can just be
set to `http://localhost:5094` — no `0.0.0.0` binding or network-security-config exception needed.
**Revert `BaseUrl` back to the live URL before shipping**, same caveat as the Android suite.

**Deterministic test data**: this suite exercises the same two seeded products as the Android suite
(`E2E Widget Alpha`/`E2E-ALPHA-001` and `E2E Widget Beta`/`E2E-BETA-002` — see that section above for
exact values and the baseline-reset SQL). If both suites are being run against the same dev server,
re-run that reset between suites too — `CheckoutAndSyncTests` performs one real sale in both.

**Differences from the Android suite worth knowing** (see `MercuriusMobileWindowsTestBase`'s class
comment for the full reasoning):
- `AutomationId` maps straight through to WinUI3's own `AutomationId` for ordinary static XAML
  elements — no `resource-id` concatenation or `content-desc` confusion the way Android needed.
  This does NOT hold for every element; see the CollectionView/Border gotchas below.
- No on-screen keyboard to occlude fields, so `SetText`/`Tap` don't need Android's
  `HideKeyboard()`/`IsKeyboardShown()` dance.
- No hamburger/drawer — `AppShell.xaml` sets `FlyoutBehavior=Locked` for the Desktop idiom, so the
  sidebar is always visible and `NavigateTo` just clicks the flyout item by text directly.
- The cart's `CartTests` drives `QuantityPopup` (tap a cart line → +/-/OK/Remove Item in the modal)
  rather than per-line `Increment_`/`Decrement_`/`Remove_` steppers — `SalesPage.xaml`'s cart UI
  moved to this popup-per-line model on both platforms; the Android suite's `CartTests.cs` was
  brought in line with the same change.

**Current state**: 18/20 tests passing consistently as of the run this section was last updated
from. The remaining two are both downstream of the same external WinUI3 instability described
below (one catches the crash mid-test as `Currently selected window has been closed`, the other is
almost certainly the same run running slow/degraded in the window just before a crash) — not a new
class of bug. Re-running usually clears both.

**Gotchas found actually running this suite** (each confirmed against a real WinAppDriver session,
not assumed — the fixes are in `Infrastructure/`, but the reasoning is worth knowing before
"fixing" something that looks wrong):
- **`Appium.WebDriver` must stay pinned below v5.** From v5 on, the .NET client is strict-W3C and
  vendor-prefixes every non-standard capability (`App` → `appium:app`). WinAppDriver 1.2.2009.02003
  (the current Microsoft release) only recognizes the unprefixed `app`/`appTopLevelWindow` keys and
  rejects `appium:app` with `Bad capabilities. Specify either app or appTopLevelWindow to create a
  session`. `4.4.5` is the last version whose `AddAdditionalCapability` sends capabilities
  unprefixed — confirmed by dumping the actual capabilities dictionary each version builds. This
  also means `WindowsDriver` is the older generic `WindowsDriver<WindowsElement>`, not the v6
  non-generic form.
- **`WinAppDriverServerFixture` must redirect the child's stdin.** WinAppDriver.exe blocks on a
  console `ReadLine()` ("Press ENTER to exit.") to know when to shut down. Without
  `RedirectStandardInput = true` in its `ProcessStartInfo`, it inherits the test runner's stdin —
  which, under a non-interactive runner, is already at EOF — so that read returns immediately and
  WinAppDriver exits right after printing "listening for requests", before the fixture's first
  readiness poll ever lands.
- **A WinUI `ContentDialog`'s own host window shares its title's Name.** The logout confirmation
  (`DisplayAlertAsync("Log Out", ..., "Log Out", "Cancel")`) put FOUR elements named "Log Out" in
  the tree — the dialog's own top-level window, its title text, the actual button, and (still
  present off to the side) the flyout `MenuItem` that opened it — with the WINDOW first in document
  order. A plain `WaitForText("Log Out").Click()` clicks that inert window and the dialog never
  closes, silently blocking every test that runs afterward in the shared session. Fixed with
  `WaitForButtonText`, which scopes to `@LocalizedControlType='button'`.
- **`SendKeys` can silently no-op with no settle delay.** A diagnostic screenshot from a downstream
  timeout showed a search field still genuinely empty (blinking caret, no characters) after a plain
  `Click()` + `Clear()` + `SendKeys()` — a known WinAppDriver flakiness where `SendKeys` arriving
  before the control finishes processing the preceding focus/click is dropped rather than queued.
  `SetText` now adds a settle delay and verifies+retries (mirroring Android's `SetText`). The
  verification itself has its own gotcha: a password `Entry`'s `Text` comes back as masked bullet
  characters, and `GetAttribute("IsPassword")` does NOT reflect that on the live element-attribute
  endpoint the way the static PageSource dump does (always reads back empty/false there) — so
  verification falls back to a length-only check whenever the exact match fails, rather than trying
  to detect password fields.
- **A `CollectionView` item's own root element loses its bound `AutomationId` entirely on Windows,
  in the MAUI version this app uses (10.0.20).** Confirmed by parsing a page-source dump as XML and
  walking the tree: `ProductsListView`'s two real, correctly-rendered children came back as
  `Microsoft.Maui.Controls.Platform.ItemTemplateContext` nodes with `AutomationId=""` — even though
  the XAML binds one (`AutomationId="{Binding ProductCode, StringFormat='ProductTile_{0}'}"`, etc.).
  Content NESTED inside that root keeps its own AutomationId fine (`ProductPrice_*`,
  `ProductStock_*`, `Quantity_*`, `CartLineTotal_*` all work exactly as written) — only the item's
  own outer wrapper (`ProductTile_*`, `CartLine_*`, `ClinicOption_*`) doesn't forward one. A fix
  for this is in progress upstream ([dotnet/maui#38723](https://github.com/dotnet/maui/pull/38723),
  still `[WIP]`/draft as of when this was investigated, targeting a future MAUI 11) — nothing
  available in any shipped release today. Two ways to work around it, both now used in this suite:
  1. **Tap a nested id instead of the root**, wherever one already exists (a click anywhere inside
     the item's bounds still fires the root's own `TapGestureRecognizer` — this isn't a hack so
     much as picking a different, working point inside the same clickable area). E.g.
     `CartTests`/`CheckoutAndSyncTests`/`CheckoutTests` tap `ProductStock_*`/`CartLineTotal_*`, not
     `ProductTile_*`/`CartLine_*`.
  2. **Positional indexing into the container**, where no nested id exists at all (e.g. Settings'
     clinic rows, whose only content is an unlabeled `Label`). `MercuriusMobileWindowsTestBase`'s
     `CollectionItems(containerAutomationId)` / `CollectionItem(containerAutomationId, index)` get
     the container's direct children by XPath position (`./*`) and return real, clickable elements
     with correct bounds — no manual pixel math needed. `SettingsPageTests` uses this to find/tap
     the first clinic row and read its name from its own child element's `Text`.
- **A `Border` wrapping a single `Label` can *also* lose its own `AutomationId`, independent of the
  CollectionView issue above.** `SalesPage.xaml`'s Tiles/List toggle (`TileViewButton`/
  `ListViewButton`, both plain static XAML, not inside any `CollectionView`) hit the exact same
  symptom — confirmed via the same page-source-as-XML technique: neither AutomationId appears
  anywhere in the tree, while the wrapped Labels' own text-derived Names (`"▦ Tiles"`, `"☰ List"`)
  do. Worked around the same way as case 1 above — `TapText("▦ Tiles")`/`TapText("☰ List")` instead
  of `Tap("TileViewButton")`/`Tap("ListViewButton")`. Worth checking for on any OTHER
  Border-wrapping-single-child element that isn't findable by its bound AutomationId — this isn't
  necessarily CollectionView-specific.
- **`ApiConfig.BaseUrl` pointed at the live site has none of this suite's fixture data — and
  neither does the local dev server have data some OTHER hand-written test might assume.**
  Confirmed via a read-only Admin Data Query API call against the live site:
  `SELECT ... WHERE ProductCode IN ('E2E-ALPHA-001','E2E-BETA-002')` returned zero rows there.
  Symmetrically, `CheckoutTests.cs` originally targeted a real live-catalog product ("Amoxicillin
  250mg Capsule" / `AMOX-250`) that turned out not to exist on the LOCAL dev server either (same
  query technique, zero rows) — once this suite standardized on the local server for controlled
  data, that test just timed out forever waiting for a tile that could never appear. Converted it
  to use `E2E-ALPHA-001` like the rest of the suite. Point `BaseUrl` at the local dev server (see
  above), confirm/seed the fixture products there, and don't assume a product literally seen once
  in a screenshot is available in whichever configuration is currently running.
- **Selling the same fixture product from more than one test class means exact-stock assertions
  race on execution order.** Once `CheckoutTests.cs` was also converted to sell
  `E2E-ALPHA-001` (alongside `CheckoutAndSyncTests`), a hard-coded `Assert.Contains("49", ...)`
  broke depending on which class's sale landed first. Fixed by asserting direction (stock strictly
  decreased) instead of an exact value — same pattern `SalesPageTests` already used for the same
  reason, now widened from a 2-value tolerance (50 or 49) to a 3-value range (48–50).
- **View mode (Tiles/List) is persisted via `Preferences`, which survives process relaunches —
  including `WindowsAppFixture`'s crash-recovery ones.** A run that leaves the app in List view
  (e.g. an interrupted `ViewToggle` test) means every subsequent run starts in List view too, not
  just later tests in the same run. `SalesPageTests.ClearSearch()` now force-switches back to Tiles
  at the start of every test rather than assuming the default.
- **The launched window isn't reliably maximized.** A diagnostic screenshot showed the app at a
  small default size with the Windows taskbar visible below it — and `SalesPage`'s Tiles/List
  toggle footer, at the bottom of the products panel, was pushed entirely out of the rendered
  viewport (not just off-screen-but-scrollable; `WaitForId` genuinely couldn't find it). This
  matters more than it might on a simpler page because the local dev server's synced catalog is
  large — the developer's whole real product list, hundreds of rows (see the Android E2E section's
  same observation) — making the page tall enough for window size to matter.
  `WindowsAppFixture.LaunchSession()` now calls `Driver.Manage().Window.Maximize()` right after
  creating the session, on both the initial launch and every crash-recovery relaunch.
- **A real, external, cumulative WinUI3 instability — confirmed, partially mitigated, not fully
  fixable from here.** Multiple full-suite runs hit a native crash in `Microsoft.ui.xaml.dll`
  itself (Windows Event Log: `Faulting module name: Microsoft.UI.Xaml.dll`, exception code
  `0xc000027b`, confirmed via `Get-WinEvent`, not inferred), killing `Mercurius.Mobile.exe` outright
  partway through a run. It is NOT tied to one specific action — the exact same crash offset
  recurred even after removing the one confirmed API misuse that matched it (see the `PaymentPage`
  entry below), and it lands at a fairly consistent point in a run (after roughly a dozen-plus
  popup/dialog/navigation interactions have accumulated), consistent with a leaked native
  reference count tipping over rather than one bad call — matches the "bad refcount... during popup
  destruction" root-cause language in a related upstream report
  ([microsoft/microsoft-ui-xaml#10279](https://github.com/microsoft/microsoft-ui-xaml/issues/10279)).
  `WindowsAppFixture.EnsureSessionAlive()` (called at the start of every test, via
  `MercuriusMobileWindowsTestBase`'s constructor) detects a dead session and relaunches against the
  same still-running WinAppDriver server, which is what took this suite from cascading to a full
  stop (4/20) to a stable majority passing (18/20) — but it can't save a test that happens to be
  *running* when the crash lands mid-test, or a test running in the degraded window just before one.
  Expect an occasional `Currently selected window has been closed` or unusually slow test near the
  middle-to-end of a full run; a re-run of just that test almost always passes. If this ever needs
  a real fix rather than a mitigation, start from PR #38723 (the same in-progress upstream MAUI fix
  referenced above, which also touches Windows CollectionView/popup handling) — no released version
  has it yet.
- **One confirmed, real production bug fixed along the way**: `PaymentPage` used
  `Navigation.PushModalAsync`/`PopModalAsync`, which is a documented, still-open upstream bug
  ([microsoft/microsoft-ui-xaml#10897](https://github.com/microsoft/microsoft-ui-xaml/issues/10897))
  that fatally crashes MAUI apps on Windows when closing a modal page this way — this would have
  crashed for a real cashier tapping Cancel or completing a cash/card sale, not just under test
  automation. Converted to a `CommunityToolkit.Maui.Views.Popup`, matching the already-working
  pattern `QuantityPopup`/`CategoryPickerPopup` already used elsewhere in this same file's
  neighborhood. This did NOT turn out to be the (sole) cause of the broader crash above — the same
  crash signature persisted afterward — but it's a legitimate fix in its own right regardless.

### BIR invoicing (`Mercurius.Shared/Bir`, `BirSyncController`, mobile `BirService`)

Philippine BIR accreditation basics (RMO 24-2023 CRM/POS). The tax maths and the printed layouts
live in `Mercurius.Shared/Bir/` (`BirTaxCalculator`, `BirDocumentFormatter`), which the mobile app
compiles in as linked source, so those files must stay dependency-free.

- **VAT**: shelf prices include 12% VAT. `Product.VatType` is Vatable/VatExempt/ZeroRated; a branch
  (`Location`) carries the seller's registered name, TIN, branch code and `IsVatRegistered` (a
  non-VAT seller reports everything as exempt). Invoices store VATable/VAT/exempt/zero-rated
  totals, `LessVat` and `DiscountAmount`; the invariant is
  `AmountDue = Vatable + VAT + Exempt + ZeroRated - Discount`.
- **Discount types** (`DiscountType`, Settings → Discount Types, `CONFIG_DISCOUNT_TYPES`): per
  tenant, percent or fixed amount, optionally VAT-exempt (VAT removed first, SC/PWD style) and/or
  asking for the holder's ID. `Category` is the BIR line it's reported on in X/Z readings (SC, PWD,
  Solo Parent, NAAC, MOV, or 0 = other). The statutory ones are seeded per tenant by the
  `SeedDefaultDiscountTypes` migration (idempotent: skips a category a tenant already has),
  `TenantProvisioningService`, and startup for a brand-new default tenant. A discount applies to
  the whole ticket; a fixed amount is spread across lines by price, capped at the ticket total.
  Invoices keep `DiscountTypeId` plus a `DiscountName` snapshot.
- **Terminals** (`PosTerminal`): each device registers itself by DeviceId on every sync
  (`POST api/sync/terminal/register`) and gets a code `T<id>`. Its MIN, serial and Permit to Use are
  entered on the device (Settings), gated by `CONFIG_POS_TERMINALS`. Supplier accreditation comes
  from config `Bir:Supplier` (blank until BIR grants it).
- **Invoice numbering is offline, per terminal**: `LocalDatabase.SaveSaleAsync` issues the next
  8-digit serial, renders the receipt and extends a SHA-256 hash chain (the e-journal) in one
  transaction. The server stores `InvoiceNumber = T<id>-<serial>` and keeps the highest serial it
  has seen; the device is the authority for its counters. A device not yet registered still sells,
  but its receipts say "NOT A VALID INVOICE" until the MIN and PTU are set.
- **X/Z readings** (mobile End of Day page): the Z-reading closes the day, moves the Z counter and
  the accumulating grand total on, and uploads as `PosZReading` (`POST api/sync/zreadings/push`,
  create-once by SyncId). Sales awaiting a Z-reading are never pruned or cleared at logout.
- **Printing** goes through the OS share sheet (e.g. RawBT on Android); paper width is per device.
- Not built yet: refunds/voids in Z-readings (print as 0), per-item discount eligibility, discounts
  on the web Sell page, a web page for terminals, and EIS e-invoicing.

### FIFO batch pricing (`BatchPricingService`)

`MedicineBatch` rows (created via `MedicineBatchesApiController`, one per stock receipt) each carry their own `UnitCost`/`UnitSalePrice`. A new shipment at a different price is just a new batch queued behind older stock — web checkout (`SalesApiController`) and mobile invoice push (`SyncController.PushInvoices`) call `BatchPricingService.FindFulfillingBatchAsync` to pick, oldest first, the first batch whose `RemainingQuantity` alone covers the full line quantity; if the oldest batch falls short, the *whole* line is priced and drawn from the next batch that can cover it rather than splitting across two batches' ledgers (a deliberate simplification, confirmed with the product owner). Only when *no* single batch covers the line is it priced from the newest batch, which is emptied with the shortfall taken from the other batches oldest first (`BatchPricingService.DeductAsync`), so batches never go negative and still add up to the shelf stock. A refund with restock puts the units back on the line's batch. `InvoiceItem.MedicineBatchId` records which batch a line was actually priced/deducted from, for refund credit and lot-recall tracing. Products with no batches at all (most non-drug items) fall back to `Product.CurrentSalePrice`/`CurrentCostPrice` unchanged — this feature is purely additive. `BatchPricingService.RefreshActivePriceAsync` keeps those flat fields in sync with whichever batch is now the oldest non-depleted one, so every screen that reads them (the Products list, the Sell page, the mobile sync API) keeps working without knowing batches exist.

### Audit trail (`AuditLog`, Settings → Audit Trail)

Every insert/update/delete through EF is recorded automatically by
`Mercurius.Repo/Repositories/Auditing/AuditSaveChangesInterceptor` (registered per-request in
`Program.cs`'s `AddDbContext`) — no controller needs to remember to log. Each row carries who
(user id/email), branch (the mobile device's clinic via `X-Location-Id`, else the user's
`UserCurrentLocation`), source (Web/MobileApp/Api/System), device (mobile `X-Device-*` headers),
IP, user agent, request path, and a request id (`TraceIdentifier`, so all rows from one sale can be
pulled up together), plus a JSON diff of what changed (secrets masked). Updates that change no
values are skipped; `IsActive` true→false is labeled `Deactivated` (this codebase soft-deletes).
Things with no EF write of their own are logged explicitly via `IAuditLogger`: web/Blazor/mobile
sign-in (success, failure, lockout), sign-out, and every raw-SQL console statement
(`DataQueryService` — raw SQL and `ExecuteUpdate`/`ExecuteDelete` bypass the interceptor).
`AuditLog` is intentionally *not* `ITenantScoped` (failed sign-ins for unknown emails have no
tenant); `AuditTrailApiController` filters by tenant explicitly. Viewing is gated by the
`ADMIN - Audit Trail` permission, grantable/revocable per role in Roles management. Reads are not
audited.

### Repository pattern — controllers never touch `DbContext` directly

`IUnitOfWork` / `IRepository<T>` (in `Mercurius.Repo/Repositories/`) wrap `MercuriusDbContext`; controllers depend on `IUnitOfWork` (scoped per request), never the context. `IRepository<T>` covers standard CRUD/paging. For server-paged list endpoints behind the Blazor tables (server-side sort/filter/paging), the API controllers instead call `IUnitOfWork.Query<T>()` (returns `IQueryable<T>` straight off the `DbSet`) and compose `.Where()`/`.OrderBy()`/`.Skip()`/`.Take()` directly — this is the pattern to follow for any new paged list, mirroring `ProductsApiController.Get` as the reference implementation.

Nothing persists until `IUnitOfWork.SaveChangesAsync()` is called — every `AddAsync`/`UpdateAsync`/`DeleteAsync` call must be paired with an explicit save afterward.

### Database Backups feature is SQLite-only

`DatabaseBackupService` (a `BackgroundService` in `Mercurius/Services/`) takes a daily `VACUUM INTO` snapshot of the SQLite file and prunes anything older than `DatabaseBackup:RetentionDays`, hard-capped at 10 days. It's only registered as a hosted service — and the "Database Backups" sidebar link / page only reachable — when `DatabaseProvider` is `Sqlite`; there's no equivalent under SQL Server (shared hosting typically blocks `BACKUP DATABASE` at the OS level, and hosting providers manage their own backups anyway).

### Authorization is claim-driven from a single registry

`Mercurius.Common.ModuleRegistry` defines every permission as a `Pages.*` string constant, plus a `Modules` array listing all of them. The file lives in `Mercurius.Shared/Security/` (still namespace `Mercurius.Common`, and `Mercurius.Common` references `Mercurius.Shared`, so no `using` changed) so the Blazor client can build its UI from the same names. `Program.cs` turns that array into one ASP.NET Core authorization policy per entry (`options.AddPolicy(module, policy => policy.RequireClaim(...))`) and, at seed time, grants every one of them to the `Administrator` role except `ModuleRegistry.DatabaseModules` (Data Query, Database Backups, Server Logs), which reach every tenant's data and belong only to the seeded `Database Administrator` role (`ModuleRegistry.DatabaseAdministratorRole`). Only a platform admin can grant or remove that role on a user (`UsersApiController`), or add those permissions to any role or edit/delete that role (`RolesApiController`); the seed admin (`SeedAdmin:Email`) is given both `Administrator` and `Database Administrator` on every startup; `Mercurius.Client/ClientServices.cs` registers the same policies client-side. Adding a new protected page means: add the constant, add it to `Modules`, gate the API with `[Authorize(Policy = ModuleRegistry.Pages.X)]`, gate the Blazor page the same way, and give it a guarded entry in `Mercurius.Client/Layout/NavMenu.razor`.

### Blazor client (`Mercurius.Client`, served from the site root)

The web UI is a Blazor WebAssembly app (MudBlazor 9). It replaced the classic MVC site, whose
views, controllers, view components and webpack theme assets have been removed. The host
prerenders each page (`MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()` in
`Program.cs`), then WebAssembly takes over. The sign-in, lockout, access-denied and error pages
are server-rendered components in `Mercurius/Components`. `Endpoints/LegacyRedirects.cs`
permanently redirects the old MVC URLs and the app's previous `/app/...` paths to the current
pages; add a rule there if you rename a route.

- **Auth**: no tokens. The browser already sends the Identity cookie to the same-origin `/api/*`
  controllers. Claims flow from server to client through
  `AddAuthenticationStateSerialization`/`AddAuthenticationStateDeserialization`. `CurrentUserState`
  holds the rest (tenant, current branch, branch list, whether backups exist): the server builds it
  while prerendering, and the browser re-fetches it from `api/account/me` when needed (see
  `ICurrentUserProfileSource`). Sign-in happens on `/account/login`.
- **Permissions**: pages use `@attribute [Authorize(Policy = ModuleRegistry.Pages.X)]`, the same
  policy their API enforces. `Routes.razor` shows `AccessDenied` to a signed-in user who lacks one
  (redirecting them to login would just loop). Pages serving two routes with different permissions
  (`/products/new` vs `/products/{id}/edit`, same for POs) check in code instead. Buttons for
  actions the role can't perform are hidden via `user.Can(...)` (`Access.cs`). The UI checks are
  cosmetic; the API is what enforces.
- **Links**: in-app links are relative (`products/5`) and resolve against `<base href="/">`.
  Anything served by the server itself (images, QR codes, file downloads such as `/Downloads/Windows`,
  `/api/admin/backups/...`, `/api/logs/.../download`) must be an absolute `/...` path with a full
  page load, so the browser fetches it instead of the Blazor router.
- **Branch switcher** (top of the sidebar): `POST api/account/branch` updates
  `UserCurrentLocation` (only to a branch in the caller's tenant), then the page force-reloads so
  everything re-queries for the new branch.
- **Shared server logic**: `DataQueryService` (key check + auditing for raw SQL),
  `TenantProvisioningService` (creates a tenant, branch and admin in one transaction, so a rejected
  password no longer leaves an orphan tenant) and `AuditLogQuery` (audit trail filters) hold those
  rules — change them there, not in the API controllers that call them.

Gotchas hit while building it:
- **SQLite can't `ORDER BY` a `decimal` column** (`NotSupportedException`); SQL Server can, so it
  only fails locally. Cast the sort key: `OrderBy(p => (double?)p.CurrentSalePrice)`
  (`ProductsApiController`).
- **A `MudDialog`'s `TitleContent` isn't redrawn** when the dialog's own state changes (the title
  renders outside the component). Pass a title to `DialogService.ShowAsync` instead of building
  one after loading.
- **MudBlazor 9 `MudFileUpload`** uses `<CustomContent>` with `context.OpenFilePickerAsync`
  (not `ActivatorContent`), and shows a chip list of chosen files unless given an empty
  `<SelectedTemplate>`.
- **Background jobs have no signed-in user**, so the tenant query filter sees tenant -1 and hides
  everything. The CSV import captures the uploader's tenant and applies it with
  `ITenantScopeOverride.UseTenant(...)` on its own scope; do the same for any new background work.
- **E2E (`Mercurius.E2ETests/Tests`, including `Tests/Blazor`)**: `BlazorTestBase.GotoAppAsync`
  waits for `.page[data-interactive='true']`, which `MainLayout` sets once WebAssembly has taken
  over from the prerendered HTML. Waiting on `article.content` alone is a race: the prerendered
  page is visible but its buttons do nothing yet. It also has helpers for MudBlazor
  fields/selects/autocompletes. Fields that aren't `Immediate` only commit on change/blur, so use
  `FillFieldAsync` (fill, then Tab) rather than a bare `FillAsync`. MudTable's *header* row also
  has class `mud-table-row` — count body rows with `.mud-table-body tr.mud-table-row`. The test
  server can't start while a local `dotnet run` of Mercurius is up (both open
  `logs/mercurius_<date>.log` exclusively), so stop that first. There is no self-registration page
  (the old `/Identity/Account/Register` redirects to sign-in), so the tests create a tenant's
  admin through Platform Admin.

### Soft delete, not hard delete

"Delete" actions should set `IsActive = false` via `UpdateAsync`, not remove the row via `DeleteAsync` — this is the established convention for master-data APIs (e.g. `SuppliersApiController`, where deactivating is a `PUT` with `IsActive = false`). Some older controllers still hard-delete; when touching them, bring them in line rather than adding new hard-deletes. A model being soft-deletable requires an `IsActive` column — not every entity has one (e.g. `Color`, `Size`, `Location` currently don't).

### Startup seeding

`Program.cs`'s `SeedDataAsync` runs on every startup (idempotent — checks before creating): the fixed roles (`Administrator`, `Manager`, `User`, `Database Administrator`), every non-database `ModuleRegistry.Modules` claim on `Administrator` (removing any database claim it still has), the database claims on `Database Administrator` (Data Query, Database Backups, Server Logs), a default `Address`/`ContactInformation`/`Location`, the admin user from `SeedAdmin:Email`/`SeedAdmin:Password` config, pharmacy reference data (`PharmacySeedData`: product categories, per-category custom fields, dosage forms), and `InvoiceStatuses` (from the `StatusCollection.InvoiceStatus` enum — `Invoice.StatusId`/`InvoiceItem.StatusId` are required FKs to this table; it was missing entirely until this was added, which meant creating any invoice on a freshly created database failed with a FOREIGN KEY constraint violation).

### Legacy artifacts — do not treat as current

`Mercurius.Repo/DbCreateScripts/*.sql` and `Mercurius.Repo/Scripts/Demo_intial.sql` are dumps from a prior SQL Server-based version of this app (before a LiteDB port, before the current EF Core setup) — historical only, not read by any code path. `Properties/PublishProfiles/*.pubxml` and `site1.PublishSettings` are Web Deploy profiles for a site4now.net shared-hosting target.
