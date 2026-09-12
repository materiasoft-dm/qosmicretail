# Mercurius Multitenancy Architecture — Proposed Design

Status: **draft for review** — nothing in this document has been implemented. It describes a
recommended target architecture and migration path, grounded in the current codebase as of
2026-09-12. Written for review before any code changes begin.

## 1. Goals (as given)

- Support multiple tenants (e.g. multiple pharmacy-owning companies) on one deployment.
- Each tenant can operate several pharmacies ("locations") and switch between them.
- Every piece of tenant data — sales, items, categories, users, everything — stays inside its
  tenant's boundary.
- No tenant can access another tenant's data, under any code path.
- Move the database back to SQL Server (currently SQLite in practice; SQL Server exists in
  config but is dormant — see §4).

## 2. Where we're starting from

This section is the "as-is" baseline the rest of the document builds on. It's more detailed than
usual for an architecture doc because the design below leans heavily on specific existing
patterns rather than introducing new ones from scratch.

### 2.1 The good news: "pharmacy" already exists as a concept

`Location` (`Mercurius.Repo/Models/Location.cs`) is already exactly "a pharmacy": `Id`, `Name`,
`Address`, `ContactInformation`, `IsActive`. Users already switch between locations today via:

- `UserCurrentLocation` — one DB row per user (`UserId` → `LocationId`), not a cookie or session.
- `LocationSelectorController.SetLocation(id, returnUrl)` — the switch action, a plain upsert into
  `UserCurrentLocation`.
- `DashboardLocationContext.GetCurrentLocationIdAsync(unitOfWork, user)` — the read-side resolver,
  used by `SalesController`, `SyncController`, and the three dashboard sales widgets.
- `HeaderViewComponent` — a second, independent read path for the header's location dropdown,
  with its own 10-minute/5-minute `IMemoryCache` entries.

This is the multi-pharmacy-switching feature the ask describes. It does not need to be
rebuilt — it needs a `Tenant` anchored above it, and its few gaps (below) closed.

**Gaps found in the existing Location model, independent of tenancy:**
- `LocationSelectorController.SetLocation` never checks that the `Location` being switched to is
  one the user is allowed to see — it blindly upserts whatever numeric `id` is posted. Today
  that's a minor issue (single pharmacy chain, low stakes). Under multitenancy, an unvalidated
  `id` becomes a direct cross-tenant access path: nothing stops a user from typing another
  tenant's `LocationId` into the request and switching into it.
- `HeaderViewComponent`'s `all_locations` cache key is global, not per-tenant. Two tenants at the
  same moment would share one cached location list.
- Only 6 of 39 entities carry `LocationId` today (`Adjustment`, `BulkPackage`, `Invoice`,
  `ItemMovement`, `LocationSetting`, `ShipmentArrival`). Everything else the ask cares about most
  — `Product`, `ProductCategory`, `Customer`, `Supplier`, `MedicineBatch`, `PurchaseOrder` — has no
  location scoping at all.
- Even where `LocationId` already exists, enforcement is manual and has already failed silently:
  `InvoiceListController.DataTable` and `AdjustmentsController.DataTable` both list records across
  every location with no filter, despite `Invoice.LocationId`/`Adjustment.LocationId` existing on
  the model. (We hit this exact class of bug twice this week already — see §8.4.)

That last point is the single most important finding from the research pass: **relying on each
controller to remember to filter is not a strategy that has worked even at single-tenant scale.**
The design below deliberately does not repeat it.

### 2.2 Data access is uniform enough to make one fix apply everywhere

Every one of the ~12 DataTables-style list controllers (`ProductsController`,
`InvoiceListController`, `AdjustmentsController`, `SuppliersController`, `CustomersController`,
etc.) gets its data the same way:

```csharp
var collection = _unitOfWork.Query<Product>();   // IUnitOfWork.Query<T>() → EfUnitOfWork
```

`EfUnitOfWork.Query<T>()` is literally `return _context.Set<T>();` — the raw `DbSet<T>`. Nothing
sits between that and the controller. This matters because it means a single **EF Core global
query filter**, registered once in `MercuriusDbContext.OnModelCreating`, is inherited automatically
by every list page, every `Repository<T>().FindAsync(...)` call, and every mobile sync endpoint —
with zero changes to any of those ~12 controllers. `MercuriusDbContext.OnModelCreating` already
applies several conventions this way (forcing nullable strings, `DeleteBehavior.Restrict`,
`decimal` precision) by looping `builder.Model.GetEntityTypes()` — the tenant filter slots into
the same idiom, not a new one.

### 2.3 Authorization is claims-based, and mobile bypasses most of it today

Web: `MercuriusClaimsPrincipalFactory` (extends `UserClaimsPrincipalFactory<MercuriusUser,
IdentityRole>`) merges every role's `access.pages` claims onto the cookie principal at sign-in.
`Program.cs` turns each of `ModuleRegistry.Modules`'s 69 entries into one authorization policy.

Mobile: `Api/AuthController.Login` issues a JWT carrying exactly three claims — `NameIdentifier`,
`Email`, `Name`. **No role claims, no `access.pages` claims, no location claim.** The mobile JWT
never goes through `MercuriusClaimsPrincipalFactory`. This is a pre-existing gap (mobile has no
concept of per-page permissions at all today) that multitenancy makes more urgent to close,
because the tenant boundary needs to be a claim too, and it needs to reach both auth schemes.

Roles themselves (`IdentityRole`, unmodified — no custom subclass) are **entirely global**: one
flat `Administrator`/`Manager`/`User` set for the whole application, no tenant or location scoping
mechanism to extend. (A `RoleModuleAccess` model exists but is dead code — grepped, zero
references outside its own file; the real mechanism is role claims via the factory above.)

### 2.4 The raw-SQL admin tool bypasses everything

`Admin/DataQueryController` (`/DataQuery/Execute`) runs caller-supplied SQL directly against the
raw ADO.NET connection pulled off the EF context — it talks to `DbConnection`, not `DbContext`, so
**no EF query filter of any kind applies to it.** It's already double-gated (an `[Authorize]`
policy claim + a separate static API key, constant-time compared) and every call is logged. This
is flagged as a specific, unavoidable risk in §7 rather than something the filter design can
paper over — raw SQL access and row-level tenant isolation are fundamentally in tension.

### 2.5 SQL Server is configured but has never really been used

`appsettings.json` defaults `DatabaseProvider` to `Sqlite`; SQLite is what the live site has
actually been running on this whole time. `appsettings.Production.json` carries a real SQL Server
connection string (site4now.net) that currently sits unused. `Program.cs` branches:

```csharp
if (useSqlServer) db.Database.EnsureCreated();
else               db.Database.Migrate();
```

SQLite is the only provider with a real migration history (4 migrations so far, all from the last
few days). SQL Server has never gone through `Migrate()` — the one time it was stood up, it was
via `EnsureCreated()` plus a manually-inserted fake `__EFMigrationsHistory` row to retroactively
pretend a migration had run. There is no SQL-Server-specific migrations folder today. Moving SQL
Server from "dormant, `EnsureCreated`-only" to "the real, migrated production provider" is
effectively a first for this codebase, not a reversion.

### 2.6 Zero existing multitenancy scaffolding

Grepped the entire repository, case-insensitive, for "tenant" — no hits, anywhere, of any kind.
This is a from-scratch design, not an extension of a partial existing one.

## 3. Proposed data model

### 3.1 New: `Tenant`

```csharp
public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; }          // e.g. the pharmacy chain's company name
    public bool IsActive { get; set; } = true; // soft-delete / suspend, matches existing convention
    public DateTime CreatedDate { get; set; }
}
```

Deliberately minimal for a first pass — no billing/subscription fields, since nothing in the ask
calls for them. Easy to extend later without a breaking migration.

### 3.2 `Location` becomes tenant-owned

Add `Location.TenantId` (required FK → `Tenant`). This is the entire structural change needed to
express "a tenant has several pharmacies" — the switching mechanism (§2.1) is reused as-is.
`LocationSelectorController.SetLocation` gains one new check: the target `Location.TenantId` must
equal the caller's own `TenantId`, else `Forbid()`. `HeaderViewComponent`'s cache keys gain a
`TenantId` segment.

### 3.3 `TenantId` added directly to every tenant-scoped entity (not just via `Location`)

The ask is explicit: "all sales, items, categories, users, everything" — that includes `Product`
and `ProductCategory`, which have no `LocationId` today and aren't logically owned by a single
pharmacy branch anyway (a tenant's catalog is shared across its own pharmacies, the same way it
implicitly is today). So the isolation key is **`TenantId`, denormalized onto each scoped entity
directly** — not inferred by joining through `Location`. Three reasons to denormalize rather than
join:

1. Several of the most important entities to lock down (`Product`, `Customer`, `Supplier`,
   `MedicineBatch`, `PurchaseOrder`) have no `LocationId` at all today and, per the ask, shouldn't
   be pharmacy-scoped anyway — only tenant-scoped. A join-based filter has nothing to join through
   for these.
2. A global query filter expressed as `e => e.TenantId == currentTenantId` is a plain indexed
   column comparison EF can push into every query cheaply. `e => e.Location.TenantId == ...`
   requires a navigation/join in *every* query system-wide, including hot paths like the products
   grid — and silently breaks if a row's `LocationId` is ever wrong or null (which has already
   happened twice this week — see §8.4).
3. It keeps the same filter shape and the same generic registration loop for all ~30 scoped
   entities, instead of two different filter expressions depending on whether an entity has its
   own `LocationId` or not.

**Entities that get a new required `TenantId` column** (from the full 39-entity inventory):

| Group | Entities |
|---|---|
| Catalog / master data | `Product`, `ProductCategory`, `CategoryField`, `Supplier`, `AdjustmentReason`, `RefundReason`, `MedicineBatch`, `BulkPackage` |
| Transactional | `Invoice`, `InvoiceItem`, `InvoiceItemRefund`, `InvoiceRefund`, `Adjustment`, `ItemMovement`, `PurchaseOrder` (+ `PurchaseOrderItem`), `ShipmentArrival` (+ `ShipmentArrivalItem`), `ZeroStockSaleAuditLog` |
| Customer data | `Customer`, `CustomerContactInformation` |
| Location / config | `Location`, `LocationSetting` |
| User-adjacent | `MercuriusUser`, `UserDashboardLayout` |
| Shared value objects owned by a scoped record | `Address`, `ContactInformation`, `File` |

**Entities that stay global / unscoped** — genuinely shared, non-tenant reference data, same as
today:

- `Province` (static PH reference list)
- `DosageForm` (pharmacy reference data, shared across all tenants)
- `InvoiceStatus`, `ShipmentArrivalStatus` (fixed, enum-backed lookup tables)

**Entities recommended for removal rather than migration** — confirmed dead/legacy during the
research pass, unreferenced anywhere outside their own file:

- `Branch` / `BranchContactInformation` — an unused parallel to `Location`.
- `Transaction` / `TransactionItem` / `TransactionIdGenerator` — an unused parallel to
  `Invoice`/`InvoiceItem`.
- `RoleModuleAccess` — an unused parallel to the role-claims mechanism actually in use.
- `UserInformation` — superseded by `MercuriusUser.FirstName`/`LastName` directly.

Carrying these into a tenant-scoped schema means deciding a `TenantId` story for tables nothing
reads — cleaner to drop them in the same migration pass. Flagging this as a recommendation to
confirm, not something to do silently.

### 3.4 Roles become per-tenant

`IdentityRole` is unmodified today and entirely global. Introduce `MercuriusRole : IdentityRole`
with a `TenantId` property, register it (`AddRoles<MercuriusRole>()`,
`IdentityDbContext<MercuriusUser, MercuriusRole, string>`), and provision each tenant's own
independent `Administrator`/`Manager`/`User` rows at tenant-creation time — mirroring exactly how
`MercuriusUser : IdentityUser` already extends Identity's base class, so this is the established
pattern, not a new one. Role **claims** (`access.pages`) stay exactly as they are — only which
role rows exist, and who can see/assign them, changes.

### 3.5 Users belong to exactly one tenant

Add `MercuriusUser.TenantId` (required). A user account lives inside one tenant permanently, set
at creation and not switchable — mirroring how the ask is phrased ("no tenant must have any access
to other tenant's data") and avoiding a whole separate class of cross-tenant-membership complexity
that nothing in the ask calls for. Switching *pharmacies* within that tenant remains exactly
today's `UserCurrentLocation` flow, unchanged.

### 3.6 A platform-admin layer, outside every tenant

Someone has to be able to create a new tenant in the first place — and that action, by definition,
can't happen from inside a tenant's own boundary. Add one flag: `MercuriusUser.IsPlatformAdmin`
(bool, default false — nobody has it except whoever operates the deployment). A small, separate
`/Platform/*` controller area, gated on `IsPlatformAdmin` rather than any `ModuleRegistry` policy,
handles tenant lifecycle (create, suspend, list) and explicitly calls `.IgnoreQueryFilters()` where
it needs cross-tenant visibility — the only place in the codebase that should ever do so
deliberately. Every other controller, view, and API endpoint stays inside the filter.

## 4. Enforcement: EF Core global query filters, not per-controller checks

### 4.1 Mechanism

1. A new marker interface, `ITenantScoped { int TenantId { get; set; } }`, implemented by every
   entity in the §3.3 "gets a new TenantId column" table.
2. A scoped `ICurrentTenantContext` service (one per request), resolved from
   `ClaimsPrincipal.FindFirstValue("tenant_id")` — added to both the cookie principal (via
   `MercuriusClaimsPrincipalFactory`, the same place `access.pages` claims are already merged in)
   and the mobile JWT (via `Api/AuthController.Login`, alongside the three claims it already
   issues). Because both auth schemes populate the same `HttpContext.User`, one resolver correctly
   serves web and mobile alike — no separate mobile-specific tenant logic needed.
3. In `MercuriusDbContext.OnModelCreating`, after the existing convention loops, add one more loop
   over `builder.Model.GetEntityTypes()` that, for every entity type implementing `ITenantScoped`,
   builds and applies `HasQueryFilter(e => e.TenantId == _currentTenantContext.TenantId)` via
   reflection (a generic helper method invoked once per closed entity type — the same "resolve
   once per `T`" shape `EfRepository<T>`'s reflection-based `IsActiveProperty` lookup already
   uses for soft-delete, so it's consistent with an existing pattern in this codebase, not a new
   idiom).
4. `AddAsync` in `EfRepository<T>` gains one more piece of auto-populated bookkeeping — same spot
   soft-delete's `IsActiveProperty` reflection already lives — to stamp `TenantId` from
   `ICurrentTenantContext` on every new row, so no controller has to remember to set it manually
   (mirroring, and fixing, the exact class of bug that left `Invoice.LocationId` unset for months
   until this week — see §8.4).
5. `ICurrentTenantContext` fails **closed**: if no tenant claim is present (shouldn't happen for an
   authenticated request post-rollout, but must be handled), it resolves to an impossible tenant id
   (e.g. `-1`) rather than `0`/`null`, so a filter bug shows up as "you see nothing" rather than
   "you see everything."

### 4.2 Why this over the alternatives

- **Per-controller `WHERE TenantId = ...` checks** — the status quo's approach for `LocationId` —
  already demonstrably fails silently (`InvoiceListController`, `AdjustmentsController`, both
  currently unfiltered by `LocationId` despite the column existing). A global filter makes the
  *unfiltered* state impossible to reach by omission; a developer has to explicitly opt out
  (`IgnoreQueryFilters()`) to see cross-tenant data, which is far safer than opting in correctly
  every single time.
- **Separate database per tenant** — the strongest possible isolation, but a large operational
  step up (per-tenant connection strings, migrations run N times, no shared reporting without
  cross-database queries) that nothing in the ask requires. Worth keeping in mind if a future
  compliance requirement demands physical separation for one large tenant; not proposed now.
- **Schema-per-tenant (SQL Server schemas)** — a middle ground, but EF Core's tooling for
  dynamically switching schemas per request is more fragile than a query filter and doesn't clearly
  buy anything the row-level filter doesn't already give us here.

### 4.3 The one deliberate exception: the Admin Data Query API

`DataQueryController` talks to the raw `DbConnection`, so it cannot inherit the EF query filter —
no code-level fix closes this without defeating the tool's own purpose (ad-hoc SQL). Recommended
treatment:

- Restrict its authorization policy to `IsPlatformAdmin` only, removing it from the set of claims
  a tenant's own `Administrator` role can ever be granted. Today it's just another
  `ModuleRegistry.Pages` entry a tenant admin could theoretically receive; post-rollout it must not
  be grantable inside any tenant at all.
- Keep the existing double-gate (claim + API key) and logging as-is on top of that restriction.
- Document it plainly (already partially done in `ADMIN_DATA_QUERY_ACCESS.md`) as a break-glass
  operational tool that sits outside the tenant boundary by design, used only by whoever operates
  the deployment — not something any tenant customer ever has access to.

## 5. Mobile sync API changes

- `Api/AuthController.Login` adds a `tenant_id` claim to the issued JWT (alongside the existing
  three). While touching this, also worth adding the `access.pages` claims mobile currently never
  gets (a pre-existing gap, not caused by tenancy, but one that becomes more visible once the app
  has real customer boundaries to reason about).
- `Api/SyncController`'s pull/push endpoints (`products/pull`, `products/push`, `invoices/pull`,
  `invoices/push`) need no explicit code changes for isolation — they read through
  `_unitOfWork.Repository<T>()`/`Query<T>()` like everything else, so the same global filter from
  §4.1 applies automatically. The only required change is making sure newly-created rows via
  `POST .../push` get `TenantId` stamped (handled generically by the `AddAsync` change in §4.1,
  point 4 — no bespoke logic needed in `SyncController` itself).
- `PushInvoices` already resolves `LocationId` server-side rather than trusting the device (the
  device always sends `0` — no location picker exists in the mobile app yet). The same
  server-resolves-it-not-the-client principle now also applies to `TenantId`, via the JWT claim
  rather than anything in the request body.

## 6. Database migration plan (SQLite → SQL Server, plus the Tenant rollout)

These two changes are easiest to land together, since both require a real EF Core migration
history for SQL Server that doesn't exist yet.

### 6.1 Give SQL Server a real migration history

Generate a from-scratch baseline migration against the `SqlServer` provider (the current
`EnsureCreated()`-plus-faked-history-row bootstrap was a one-time workaround, not a repeatable
process — see §2.5). Recommended shape: keep SQLite fully working for local dev exactly as today
(fast, zero-setup, matches CLAUDE.md's existing dev workflow), but generate SQL Server's own
migrations into a separate folder/output (e.g. `Migrations/SqlServer/`) so the two providers'
migration histories don't collide — this is a well-trodden EF Core pattern for apps supporting more
than one provider. `Program.cs`'s branch changes from:

```csharp
if (useSqlServer) db.Database.EnsureCreated();
else               db.Database.Migrate();
```

to `Database.Migrate()` unconditionally, pointed at whichever provider-specific migrations
assembly matches the active `DatabaseProvider`. `DatabaseProvider`'s default flips from `Sqlite` to
`SqlServer` for anything beyond local dev.

### 6.2 Add `TenantId` columns without breaking existing data

Standard safe-migration sequence, needed because real rows already exist in the SQLite database
that's been live this whole time:

1. Migration A: add every `TenantId` column as **nullable**, no FK/index yet.
2. One-time data step: insert a single `Tenant` row (call it whatever the actual pharmacy owner's
   company is, or "Default Tenant" as a placeholder), then backfill every existing row's new
   `TenantId` column to that tenant's `Id` — plus assign the existing `Location`, existing
   `MercuriusUser` rows, and existing roles to it. Same shape as the `LocationId = 0` backfill
   already performed twice this week via the Admin Data Query API, just broader in scope.
3. Migration B: alter the columns to **required**, add the FK constraints and indexes.

This mirrors the exact "add nullable → backfill → tighten to required" sequence already used
successfully for this app's other required-FK gaps (e.g. `InvoiceStatuses` needing to be seeded
before `Invoice.StatusId` could be trusted).

### 6.3 Move the actual data from SQLite to SQL Server

Because the live site's real data has been accumulating on SQLite (not the dormant SQL Server
connection), the cutover needs an explicit data-copy step, not just a schema migration: export
every table from the live SQLite file, import into the freshly-migrated SQL Server database,
preserving identity/PK values (existing `InvoiceNumber`/`SyncId`/etc. values must survive
unchanged, since mobile devices already reference them). A small one-off console script reading
via `EfUnitOfWork`-style repository calls against a SQLite `DbContext` and writing to a SQL Server
`DbContext` is the safest approach — safer than a raw file-level import, since it goes through the
same EF model/conventions (nullable-string handling, decimal precision) on the way in.

### 6.4 Suggested overall sequence

1. Build and test the `Tenant`/`TenantId`/`MercuriusRole`/query-filter changes against SQLite
   locally first (keeps the dev loop fast, isolates tenancy risk from provider-switch risk).
2. Once tenancy is verified end-to-end on SQLite, generate the SQL Server migration baseline and
   verify it against a scratch SQL Server database (not the live one).
3. Schedule a maintenance window for the live cutover: freeze writes briefly, run the data-copy
   script, flip `DatabaseProvider` to `SqlServer` in production config, redeploy, verify, lift the
   freeze.

## 7. Residual risks and open questions (for your review)

- **The Admin Data Query API is a hard limit on "no tenant must have any access to other tenant's
  data."** Restricting it to platform-admin-only (§4.3) closes the everyday risk, but it's worth
  being explicit that this tool's entire purpose — raw SQL — is fundamentally incompatible with
  row-level isolation. If that's not an acceptable residual risk, the alternative is removing the
  tool from any environment tenants' own staff could ever reach credentials for, which is a bigger
  operational conversation than this document can resolve on its own.
- **Are product catalogs shared across a tenant's own pharmacies, or per-pharmacy?** This design
  assumes shared-within-a-tenant (matching today's behavior, where `Product` has no location
  scoping at all) — only the tenant boundary is new for catalog data, not a second per-pharmacy
  boundary. Flagging in case that's not the intent.
- **Can a user belong to more than one tenant?** Assumed no (§3.5) — simplest model, matches the
  ask's phrasing. If a future need arises for someone to work across tenants day-to-day (e.g. your
  own support access), that's a different feature (more like the platform-admin flag, §3.6) than
  multi-tenant membership.
- **Tenant provisioning workflow**: this document assumes an admin-provisioned model (you create a
  tenant and its first admin user via the platform-admin area) rather than tenant self-signup.
  Self-signup is a materially bigger feature (billing, email verification, abuse handling) that
  nothing in the ask currently calls for.
- **Legacy dead entities** (`Branch`, `Transaction*`, `RoleModuleAccess`, `UserInformation`) —
  recommended for removal rather than migration (§3.3); flagging for your confirmation before
  anything touches them, since "confirmed unreferenced by grep" isn't the same guarantee as
  "confirmed safe to delete."
- **Downtime for the live cutover** (§6.3) — moving the actual data store is not a zero-downtime
  operation as scoped here. If zero downtime is a hard requirement, that needs a dual-write/backfill
  strategy instead of a freeze-and-copy window, which is a substantially larger effort.

## 8. Summary of what changes vs. what's reused

**Reused as-is:** `Location`, `UserCurrentLocation`, `LocationSelectorController`,
`DashboardLocationContext`, `HeaderViewComponent` (plus the two small hardening fixes noted in
§2.1), the entire repository/`IUnitOfWork` pattern, all ~12 DataTables-style controllers (no code
changes needed — they inherit the filter automatically), the `MercuriusUser : IdentityUser`
extension pattern (directly reused for `MercuriusRole : IdentityRole`), the existing
`OnModelCreating` convention-loop style (directly reused for the tenant filter), and the existing
`EfRepository<T>` reflection-based auto-bookkeeping pattern (directly reused for auto-stamping
`TenantId`).

**New:** `Tenant` entity, `MercuriusRole`, `ICurrentTenantContext`, the `ITenantScoped` marker
interface and its query-filter registration loop, the platform-admin area, a SQL Server migration
history, and the SQLite→SQL Server data-copy step.

**Changed:** ~30 entities gain a `TenantId` column (§3.3), `MercuriusUser` gains `TenantId` and
`IsPlatformAdmin`, `Api/AuthController` issues a richer JWT, `LocationSelectorController` gains an
ownership check, `HeaderViewComponent`'s cache keys gain a tenant segment, `DataQueryController`'s
authorization tightens to platform-admin-only, `SeedDataAsync` becomes a per-tenant provisioning
routine rather than a single global bootstrap, and `Program.cs`'s provider branch moves from
`EnsureCreated()`-for-SqlServer to a real migration on both providers.
