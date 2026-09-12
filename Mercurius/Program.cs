using Mercurius;
using Mercurius.Common.Constants;
using Mercurius.Models;
using Mercurius.Repo;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Repositories;
using Mercurius.Repo.Models;
using Mercurius.Services;
using Mercurius.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IO;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// DATABASE CONFIGURATION
// ============================================
// EF Core backs all data (business + Identity). Two providers are supported, chosen via
// "DatabaseProvider" ("Sqlite" [default] or "SqlServer"):
//   Sqlite    — single local file, from "ConnectionStrings:DefaultConnection" if set, else
//               MERCURIUS_DB=mercurius → mercurius.sqlite (default);
//               MERCURIUS_DB=veramay → veramay.sqlite (clean import target for new client).
//               The daily DatabaseBackupService only makes sense for this provider (it VACUUMs
//               the local file), so it's only registered when Sqlite is active.
//   SqlServer — "ConnectionStrings:SqlServer" is used as-is. No local file, so no backup
//               service — hosting providers typically manage SQL Server backups themselves.
// Kept as separate keys (rather than both reusing DefaultConnection) so flipping the provider
// back and forth doesn't require re-entering/losing either connection string.
// ============================================

var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var useSqlServer = string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase);

var dbName = Environment.GetEnvironmentVariable("MERCURIUS_DB") ?? "mercurius";
var configuredSqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var sqliteConnectionString = string.IsNullOrWhiteSpace(configuredSqliteConnectionString)
    ? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, $"{dbName}.sqlite")}"
    : configuredSqliteConnectionString;
var sqlServerConnectionString = builder.Configuration.GetConnectionString("SqlServer");

// Resolves the tenant boundary MercuriusDbContext's global query filter (and EfRepository's
// auto-stamping) rely on — see MULTITENANCY_ARCHITECTURE.md. Registered before AddDbContext so
// MercuriusDbContext's constructor dependency resolves regardless of registration order (DI
// doesn't actually require this ordering, but it reads better next to what it's for).
builder.Services.AddScoped<Mercurius.Repo.Repositories.ICurrentTenantContext, Mercurius.Services.HttpContextCurrentTenantContext>();

builder.Services.AddDbContext<MercuriusDbContext>(options =>
{
    if (useSqlServer)
    {
        // A separate migrations assembly/history from SQLite's — the two providers' migrations
        // aren't interchangeable (column type strings like "INTEGER"/"TEXT" are baked in at
        // generation time for whichever provider was active then). See
        // Mercurius.Repo.Migrations.SqlServer and MULTITENANCY_ARCHITECTURE.md §6.1.
        options.UseSqlServer(sqlServerConnectionString,
            sql => sql.MigrationsAssembly("Mercurius.Repo.Migrations.SqlServer"));
    }
    else
    {
        options.UseSqlite(sqliteConnectionString);
    }
});

// Register Unit of Work pattern
builder.Services.AddScoped<IUnitOfWork>(sp =>
    new EfUnitOfWork(sp.GetRequiredService<MercuriusDbContext>(), sp.GetRequiredService<Mercurius.Repo.Repositories.ICurrentTenantContext>()));

// ============================================
// ASP.NET CORE IDENTITY (EF Core-backed)
// ============================================

builder.Services.AddDefaultIdentity<MercuriusUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MercuriusDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<MercuriusClaimsPrincipalFactory>();

// ============================================
// JWT BEARER AUTH (mobile/API clients)
// ============================================
// Added alongside Identity's cookie scheme (the MVC site's default) rather than replacing it —
// AddAuthentication() with no default-scheme argument here just registers an additional scheme.
// Api/AuthController issues tokens; Api/SyncController requires this scheme explicitly via
// [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)].

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key") ?? string.Empty;

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// ============================================
// MVC & RAZOR PAGES
// ============================================

builder.Services.AddControllersWithViews(options =>
{
    // The POCOs in Mercurius.Repo.Models use non-nullable `string` properties for many
    // optional fields (Description, Note, CustomWarning, Model, ImageFilename, etc.).
    // With <Nullable>enable</Nullable>, ASP.NET Core's model binder treats every
    // non-nullable reference as implicitly [Required], which made Edit/Create POSTs
    // silently fail validation when those fields weren't on the form. Restore the
    // pre-.NET 6 behaviour: rely on explicit [Required] only.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.AddRazorPages();

// ============================================
// API VERSIONING
// ============================================

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// ============================================
// APPLICATION SERVICES
// ============================================

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<ILoggerService, LoggerService>();
builder.Services.AddScoped<Mercurius.Services.BatchPricingService>();
builder.Services.AddScoped<Mercurius.Services.AdjustmentService>();

builder.Services.Configure<DatabaseBackupOptions>(builder.Configuration.GetSection("DatabaseBackup"));
// Registered as a singleton (not just via AddHostedService) so DatabaseBackupsController can
// inject the concrete type directly (e.g. to read BackupDirectory) alongside the hosting
// infrastructure starting/stopping the same instance as an IHostedService. Kept registered
// regardless of provider so the controller's DI resolves either way; only actually scheduled
// as a background job when Sqlite is the active provider — the VACUUM INTO snapshot mechanism
// has no SQL Server equivalent, and hosting providers manage SQL Server backups themselves.
builder.Services.AddSingleton<DatabaseBackupService>();
if (!useSqlServer)
{
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DatabaseBackupService>());
}

// Product CSV import runs on a background queue so a large file doesn't block the upload
// request, and keeps processing even if the uploader navigates away.
builder.Services.AddSingleton<ImportJobTracker>();
builder.Services.AddSingleton<ProductImportQueue>();
builder.Services.AddHostedService<ProductImportBackgroundService>();

// ============================================
// AUTHORIZATION
// ============================================

builder.Services.Configure<IdentityOptions>(options =>
    options.SignIn.RequireConfirmedEmail = false);

builder.Services.AddAuthorization(options =>
{
    // Note: No FallbackPolicy - controllers opt-in to auth with [Authorize].
    // Controllers/actions that need to be public must explicitly use [AllowAnonymous].
    foreach (var module in Mercurius.Common.ModuleRegistry.Modules)
    {
        options.AddPolicy(module, policy => policy.RequireClaim(MercuriusClaimTypes.AccessPages, module));
    }

    // Outside every tenant's boundary — see MULTITENANCY_ARCHITECTURE.md §3.6/§4.3. Deliberately
    // not part of ModuleRegistry: no tenant's own Administrator role can ever be granted this,
    // since it's what creates/manages tenants in the first place.
    options.AddPolicy("PlatformAdmin", policy => policy.RequireClaim(MercuriusClaimTypes.IsPlatformAdmin, "true"));
});

builder.Services.AddHttpContextAccessor();

// ============================================
// SESSION & SECURITY
// ============================================

builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = ".Mercurius.Session";
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// ============================================
// RATE LIMITING
// ============================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddPolicy("fixed", context => 
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10
            }));
    
    options.AddPolicy("sliding", context => 
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 20
            }));
    
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            StatusCode = 429,
            Message = "Too many requests. Please try again later.",
            RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) 
                ? retryAfter.TotalSeconds 
                : 60
        }, cancellationToken);
    };
});

// ============================================
// HEALTH CHECKS
// ============================================

builder.Services.AddHealthChecks()
    .AddCheck("database", () =>
    {
        try
        {
            // Simple connectivity check
            using System.Data.Common.DbConnection connection = useSqlServer
                ? new Microsoft.Data.SqlClient.SqlConnection(sqlServerConnectionString)
                : new SqliteConnection(sqliteConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.ExecuteScalar();
            return HealthCheckResult.Healthy($"{databaseProvider} connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{databaseProvider} connection failed", ex);
        }
    }, tags: new[] { "db", "ready" });

// ============================================
// BUILD APPLICATION
// ============================================

// Validate required configuration at startup so misconfigured deployments fail fast
// with a clear error rather than silently misbehaving at runtime.
var seedAdminEmail = builder.Configuration.GetValue<string>("SeedAdmin:Email");
var seedAdminPassword = builder.Configuration.GetValue<string>("SeedAdmin:Password");
if (string.IsNullOrWhiteSpace(seedAdminEmail) || string.IsNullOrWhiteSpace(seedAdminPassword))
{
    // Log a warning — missing seed config is non-fatal (admin may already exist).
    Console.Error.WriteLine("WARNING: SeedAdmin:Email and/or SeedAdmin:Password are not configured. Admin user will not be seeded.");
}

var app = builder.Build();

// ============================================
// HTTP REQUEST PIPELINE
// ============================================

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseExceptionHandling();
app.UseRateLimiter();  // Rate limiting
// app.UseHttpsRedirection(); // Disabled for localhost dev

// ============================================
// BLAZOR WEBASSEMBLY (Mercurius.Client), hosted under /app
// ============================================
// ASP.NET Core hosted model: this project (Mercurius) serves both the JSON API the client calls
// and the client's own compiled static assets — no separate deploy/CORS story. Mounted under
// /app rather than replacing "/" because the frontend conversion is incremental, page by page;
// the MVC app keeps serving everything else in the meantime. See MULTITENANCY_ARCHITECTURE.md.
app.UseBlazorFrameworkFiles("/app");
app.UseStaticFiles();
app.UseSession();          // Session MUST be before Authentication
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds,
                Description = e.Value.Description
            })
        });
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Client-side routes within the Blazor app (e.g. /app/products) aren't real server endpoints —
// fall back to the client's index.html so its own Router can handle them.
app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");

// ============================================
// DATABASE SCHEMA
// ============================================
// EF Core Migrations apply the schema for whichever provider is active — Migrate() applies only
// the delta, so existing data survives future schema changes. SQLite's migrations live in
// Mercurius.Repo/Migrations; SQL Server's live in the separate Mercurius.Repo.Migrations.SqlServer
// assembly (configured above via MigrationsAssembly), since the two providers' migration files
// aren't interchangeable. SQL Server previously used EnsureCreated() while it was dormant — now
// that it's the live provider, it needs a real, repeatable migration history like SQLite always
// had.

using (var schemaScope = app.Services.CreateScope())
{
    var db = schemaScope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
    db.Database.Migrate();
}

// ============================================
// SEED DATA
// ============================================

await SeedDataAsync(app);

app.Run();

// ============================================
// SEED DATA METHOD
// ============================================

// A couple of seed blocks (InvoiceStatuses, ShipmentArrivalStatuses) insert rows with an
// explicit Id matching a fixed enum/constant, rather than letting the identity column assign
// one — necessary since other code casts directly to/from those specific int values. SQLite
// allows this unconditionally; SQL Server rejects it ("Cannot insert explicit value for identity
// column") unless IDENTITY_INSERT is toggled on for the duration of the insert. Found by actually
// testing against SQL Server before the live cutover, not by inspection.
static async Task SaveWithExplicitIdsAsync(MercuriusDbContext dbContext, IUnitOfWork unitOfWork, string tableName, bool useSqlServer)
{
    if (!useSqlServer)
    {
        await unitOfWork.SaveChangesAsync();
        return;
    }

    // SET IDENTITY_INSERT is scoped to the connection/session it ran on, not the database — EF's
    // connection pooling would otherwise hand the actual SaveChanges insert a different pooled
    // connection than the one this just configured, and the setting silently wouldn't apply
    // (confirmed: this exact failure mode, even with the toggle present, until the connection was
    // pinned open explicitly). OpenConnectionAsync/CloseConnectionAsync keep one connection alive
    // across both calls.
    await dbContext.Database.OpenConnectionAsync();
    try
    {
        // Table names here are always one of this file's own hardcoded literals, never external
        // input — plain concatenation (not an interpolated-string literal) so the analyzer
        // doesn't flag it as if it were unparameterized user data; a SQL identifier can't be a
        // query parameter anyway, so ExecuteSqlAsync's parameterization wouldn't apply here.
        await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + tableName + "] ON");
        await unitOfWork.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + tableName + "] OFF");
    }
    finally
    {
        await dbContext.Database.CloseConnectionAsync();
    }
}

static Task<int> BackfillEntityTenantGenericAsync<TEntity>(MercuriusDbContext dbContext, int tenantId)
    where TEntity : class, ITenantScoped
{
    return dbContext.Set<TEntity>().IgnoreQueryFilters()
        .Where(e => e.TenantId == 0)
        .ExecuteUpdateAsync(s => s.SetProperty(e => e.TenantId, tenantId));
}

async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<MercuriusUser>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        var roles = new[] { "Administrator", "Manager", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation($"Created role: {role}");
            }
        }

        var adminRole = await roleManager.FindByNameAsync("Administrator");
        if (adminRole != null)
        {
            var existingClaims = await roleManager.GetClaimsAsync(adminRole);
            foreach (var page in Mercurius.Common.ModuleRegistry.Modules)
            {
                if (!existingClaims.Any(c => c.Type == MercuriusClaimTypes.AccessPages && c.Value == page))
                {
                    await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim(MercuriusClaimTypes.AccessPages, page));
                }
            }
            logger.LogInformation("Added all permissions to Administrator role");
        }

        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        // The first tenant — either a fresh install, or the existing single-pharmacy deployment
        // being anchored under multitenancy for the first time. See MULTITENANCY_ARCHITECTURE.md
        // §6.2 for the full rollout plan (this covers only the "fresh database" bootstrap case;
        // backfilling a database that already has pre-tenancy data is a separate, explicit
        // one-time migration step, not something startup seeding should do silently).
        var tenantRepo = unitOfWork.Repository<Tenant>();
        var existingTenants = await tenantRepo.GetAllAsync();
        var defaultTenant = existingTenants.FirstOrDefault();
        if (defaultTenant == null)
        {
            defaultTenant = new Tenant { Name = "Default Tenant", IsActive = true, CreatedDate = DateTime.UtcNow };
            await tenantRepo.AddAsync(defaultTenant);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Created default tenant");
        }

        // Backfill: the AddTenantFoundation migration added every ITenantScoped column as
        // NOT NULL with a default of 0, which no real Tenant row can ever have — so every row
        // that existed before this migration ran needs its TenantId pointed at the default
        // tenant, or the query filter above makes it invisible to everyone. Bypasses
        // IUnitOfWork.Query<T>() (which is itself filtered — during startup there's no
        // HttpContext, so ICurrentTenantContext resolves to -1 and would see nothing here) via
        // the raw DbContext, deliberately, for this one bootstrap operation only. Safe to leave
        // running on every startup: idempotent, and a real second tenant's own rows are never at
        // TenantId 0 in the first place (EfRepository always stamps a real tenant id on create).
        var dbContext = services.GetRequiredService<MercuriusDbContext>();

        // Every ITenantScoped entity, backfilled explicitly (compile-time generics — reflection
        // over a top-level-statement local function turned out not to resolve reliably at
        // runtime, so this trades a bit of repetition for something guaranteed to work). Add a
        // line here whenever a new entity adopts ITenantScoped.
        var totalBackfilled = 0
            + await BackfillEntityTenantGenericAsync<Product>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Location>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<ProductCategory>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<CategoryField>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Supplier>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<AdjustmentReason>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<RefundReason>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<MedicineBatch>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<BulkPackage>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Invoice>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<InvoiceItem>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<InvoiceItemRefund>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<InvoiceRefund>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Adjustment>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<ItemMovement>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<PurchaseOrder>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<PurchaseOrderItem>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<ShipmentArrival>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<ShipmentArrivalItem>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<ZeroStockSaleAuditLog>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Customer>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<CustomerContactInformation>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<LocationSetting>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Address>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<ContactInformation>(dbContext, defaultTenant.Id)
            + await BackfillEntityTenantGenericAsync<Mercurius.Repo.Models.File>(dbContext, defaultTenant.Id);

        // MercuriusUser isn't ITenantScoped (see its own comment — the filter would break
        // sign-in), but it still carries a plain TenantId column that claims-issuing reads, so it
        // needs the same one-time correction.
        var backfilledUsers = await dbContext.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TenantId, defaultTenant.Id));

        if (totalBackfilled + backfilledUsers > 0)
        {
            logger.LogInformation(
                "Backfilled TenantId to default tenant: {Entities} tenant-scoped rows, {Users} users",
                totalBackfilled, backfilledUsers);
        }

        var addressRepo = unitOfWork.Repository<Address>();
        var contactInformationRepo = unitOfWork.Repository<ContactInformation>();
        var locationRepo = unitOfWork.Repository<Location>();
        var userCurrentLocationRepo = unitOfWork.Repository<UserCurrentLocation>();
        // Bypasses the tenant filter deliberately — same reasoning as the backfill above. Startup
        // seeding runs with no HttpContext (ICurrentTenantContext resolves to -1), so a filtered
        // read here would always see zero locations and re-seed a duplicate "Branch1" on every
        // restart once tenancy is active, regardless of what already exists.
        var locationExists = await dbContext.Locations.IgnoreQueryFilters().AnyAsync();
        if (!locationExists)
        {
            var defaultAddress = new Address { TenantId = defaultTenant.Id, IsActive = true, Province = "Pampanga", Country = "Philippines" };
            await addressRepo.AddAsync(defaultAddress);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation($"Created default address: {defaultAddress.Province}, {defaultAddress.Country}");

            // Location.ContactInformationId is a required FK — create a placeholder record so
            // seeding succeeds; branches can fill in real contact details via Locations/Edit.
            var defaultContactInformation = new ContactInformation { TenantId = defaultTenant.Id };
            await contactInformationRepo.AddAsync(defaultContactInformation);
            await unitOfWork.SaveChangesAsync();

            var defaultLocation = new Location { Name = "Branch1", TenantId = defaultTenant.Id, AddressId = defaultAddress.Id, ContactInformationId = defaultContactInformation.Id };
            await locationRepo.AddAsync(defaultLocation);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation($"Created default location: {defaultLocation.Name}");
        }

        var seedConfig = app.Configuration.GetSection("SeedAdmin");
        var adminEmail = seedConfig.GetValue<string>("Email");
        var adminPassword = seedConfig.GetValue<string>("Password");

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin:Email and SeedAdmin:Password are not configured. Skipping admin seed.");
        }
        else
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new MercuriusUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, FirstName = "Admin", LastName = "User", TenantId = defaultTenant.Id, IsPlatformAdmin = true };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                    logger.LogInformation($"Created admin user: {adminEmail}");
                }
            }
            else if (!adminUser.IsPlatformAdmin)
            {
                // The very first seeded admin doubles as the platform operator (whoever runs this
                // deployment) until a real "invite a platform admin" flow exists — see
                // MULTITENANCY_ARCHITECTURE.md §3.6. Only ever applies to this one seed account,
                // never to a tenant's own admin created later through normal signup/provisioning.
                adminUser.IsPlatformAdmin = true;
                await userManager.UpdateAsync(adminUser);
                logger.LogInformation("Granted platform-admin to seed admin user");
            }

            // Assign default location to admin (new or existing) — unfiltered for the same
            // startup-has-no-tenant-context reason as above.
            var defaultLocation = await dbContext.Locations.IgnoreQueryFilters().FirstOrDefaultAsync();
            if (defaultLocation != null && adminUser != null)
            {
                var existingUserLocation = await userCurrentLocationRepo.FindAsync(ucl => ucl.UserId == adminUser.Id);
                if (!existingUserLocation.Any())
                {
                    await userCurrentLocationRepo.AddAsync(new UserCurrentLocation { UserId = adminUser.Id, LocationId = defaultLocation.Id });
                    await unitOfWork.SaveChangesAsync();
                    logger.LogInformation("Assigned default location to admin user");
                }
            }
        }

        // Seed pharmacy reference data (categories, custom fields, dosage forms)
        await PharmacySeedData.SeedAsync(unitOfWork, dbContext, defaultTenant.Id, logger);

        // Seed InvoiceStatuses — a required FK on both Invoice and InvoiceItem (StatusId).
        // This was missing entirely, so any invoice creation (NewSale, sync push) would fail
        // with a FOREIGN KEY constraint violation on a freshly created database.
        var invoiceStatusRepo = unitOfWork.Repository<InvoiceStatus>();
        var existingStatuses = await invoiceStatusRepo.GetAllAsync();
        if (!existingStatuses.Any())
        {
            foreach (var status in Enum.GetValues<Mercurius.Common.Constants.StatusCollection.InvoiceStatus>())
            {
                await invoiceStatusRepo.AddAsync(new InvoiceStatus { Id = (int)status, Name = status.ToString() });
            }
            await SaveWithExplicitIdsAsync(dbContext, unitOfWork, "InvoiceStatuses", useSqlServer);
            logger.LogInformation("Seeded InvoiceStatuses");
        }

        // Seed ShipmentArrivalStatuses — same gap as InvoiceStatuses above:
        // ShipmentArrival.ShipmentArrivalStatus is a required (non-nullable) navigation, so this
        // being empty meant creating any shipment failed with a FOREIGN KEY constraint violation
        // on a freshly created database. Confirmed zero rows in ShipmentArrivals live — this
        // feature had never actually been used successfully.
        var shipmentStatusRepo = unitOfWork.Repository<ShipmentArrivalStatus>();
        var existingShipmentStatuses = await shipmentStatusRepo.GetAllAsync();
        if (!existingShipmentStatuses.Any())
        {
            var shipmentStatuses = new[]
            {
                new ShipmentArrivalStatus { Id = 1, Name = "Pending", CssClass = "badge-light-warning", IsActive = true },
                new ShipmentArrivalStatus { Id = 2, Name = "Received", CssClass = "badge-light-success", IsActive = true },
                new ShipmentArrivalStatus { Id = 3, Name = "Delayed", CssClass = "badge-light-danger", IsActive = true },
                new ShipmentArrivalStatus { Id = 4, Name = "Damaged", CssClass = "badge-light-dark", IsActive = true },
            };
            foreach (var status in shipmentStatuses)
            {
                await shipmentStatusRepo.AddAsync(status);
            }
            await SaveWithExplicitIdsAsync(dbContext, unitOfWork, "ShipmentArrivalStatuses", useSqlServer);
            logger.LogInformation("Seeded ShipmentArrivalStatuses");
        }

    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}