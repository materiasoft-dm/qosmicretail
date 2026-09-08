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

builder.Services.AddDbContext<MercuriusDbContext>(options =>
{
    if (useSqlServer)
    {
        options.UseSqlServer(sqlServerConnectionString);
    }
    else
    {
        options.UseSqlite(sqliteConnectionString);
    }
});

// Register Unit of Work pattern
builder.Services.AddScoped<IUnitOfWork>(sp =>
    new EfUnitOfWork(sp.GetRequiredService<MercuriusDbContext>()));

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

// ============================================
// DATABASE SCHEMA
// ============================================
// EF Core Migrations (Migrations/ folder) apply the schema for the actively-used Sqlite
// provider — Migrate() applies only the delta, so existing data survives future schema changes.
// The SqlServer path stays on EnsureCreated(): that provider is currently unused (no live SQL
// Server database exists), and building out a second migrations assembly for a dormant provider
// isn't worth the complexity until it's actually back in use.

using (var schemaScope = app.Services.CreateScope())
{
    var db = schemaScope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
    if (useSqlServer)
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}

// ============================================
// SEED DATA
// ============================================

await SeedDataAsync(app);

app.Run();

// ============================================
// SEED DATA METHOD
// ============================================

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
        var addressRepo = unitOfWork.Repository<Address>();
        var contactInformationRepo = unitOfWork.Repository<ContactInformation>();
        var locationRepo = unitOfWork.Repository<Location>();
        var userCurrentLocationRepo = unitOfWork.Repository<UserCurrentLocation>();
        var locations = await locationRepo.GetAllAsync();
        if (!locations.Any())
        {
            var defaultAddress = new Address { IsActive = true, Province = "Pampanga", Country = "Philippines" };
            await addressRepo.AddAsync(defaultAddress);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation($"Created default address: {defaultAddress.Province}, {defaultAddress.Country}");

            // Location.ContactInformationId is a required FK — create a placeholder record so
            // seeding succeeds; branches can fill in real contact details via Locations/Edit.
            var defaultContactInformation = new ContactInformation();
            await contactInformationRepo.AddAsync(defaultContactInformation);
            await unitOfWork.SaveChangesAsync();

            var defaultLocation = new Location { Name = "Branch1", AddressId = defaultAddress.Id, ContactInformationId = defaultContactInformation.Id };
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
                adminUser = new MercuriusUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, FirstName = "Admin", LastName = "User" };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                    logger.LogInformation($"Created admin user: {adminEmail}");
                }
            }

            // Assign default location to admin (new or existing)
            var existingLocations = await locationRepo.GetAllAsync();
            var defaultLocation = existingLocations.FirstOrDefault();
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
        await PharmacySeedData.SeedAsync(unitOfWork, logger);

    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}