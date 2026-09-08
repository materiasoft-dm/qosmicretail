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
```

All four projects target `net9.0` with `TreatWarningsAsErrors=true` — a nullable-reference warning fails the build, not just `dotnet build` locally.

If a `dotnet run` process is left running, a subsequent `dotnet build` will fail with `MSB3027`/file-lock errors on `Mercurius.Common.dll`/`Mercurius.Repo.dll`; stop the running `Mercurius` process first.

## Architecture

**Projects**: `Mercurius` (ASP.NET Core MVC web app, entry point), `Mercurius.Repo` (EF Core persistence + domain models), `Mercurius.Common` (cross-cutting constants, `ModuleRegistry`, shared helpers), `Mercurius.Tests` (xUnit).

### Persistence is provider-switchable

`Program.cs` reads `DatabaseProvider` from configuration (`"Sqlite"` default, or `"SqlServer"`) and configures `MercuriusDbContext` (in `Mercurius.Repo/Repositories/`) accordingly — there are no EF Core migrations; the schema is created via `Database.EnsureCreated()` at startup instead. `MercuriusDbContext.OnModelCreating` does several things that apply regardless of provider and are easy to miss:

- Forces every `string` property without an explicit `[Required]` attribute to be nullable at the EF model level, overriding the convention that infers `NOT NULL` from C#'s non-nullable-reference-type annotation. This mirrors `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` in `Program.cs`'s MVC config — the two must stay in sync, since many models declare optional fields (`Description`, `Note`, etc.) as non-nullable `string` for convenience.
- Sets `DeleteBehavior.Restrict` on every foreign key, globally overriding EF's cascade-by-default convention for required relationships. SQL Server refuses to create a schema where two cascade paths converge on the same table (e.g. `InvoiceStatus` → `Invoice` → `InvoiceItem` and `InvoiceStatus` → `InvoiceItem` directly) — SQLite never enforced this, so the failure only surfaces against SQL Server.
- Gives every `decimal`/`decimal?` property explicit `HasPrecision(18, 4)` — SQL Server requires an explicit precision/scale or defaults silently; SQLite doesn't care.
- Mirrors the index list from the old LiteDB implementation (see `EnsureCoreIndexes`-style grouping by entity) directly as `HasIndex` calls.

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

`Program.cs`'s `SeedDataAsync` runs on every startup (idempotent — checks before creating): the three fixed roles, all `ModuleRegistry.Modules` claims on `Administrator`, a default `Address`/`ContactInformation`/`Location`, the admin user from `SeedAdmin:Email`/`SeedAdmin:Password` config, and pharmacy reference data (`PharmacySeedData`: product categories, per-category custom fields, dosage forms).

### Legacy artifacts — do not treat as current

`Mercurius.Repo/DbCreateScripts/*.sql` and `Mercurius.Repo/Scripts/Demo_intial.sql` are dumps from a prior SQL Server-based version of this app (before a LiteDB port, before the current EF Core setup) — historical only, not read by any code path. `Properties/PublishProfiles/*.pubxml` and `site1.PublishSettings` are Web Deploy profiles for a site4now.net shared-hosting target.
