# Mercurius — Project Knowledgebase

## 1. Project Overview
- **Name**: Mercurius
- **Type**: ASP.NET Core MVC (.NET 9.0) web application
- **Database**: LiteDB (single source of truth for settings, including Shopify credentials)
- **Architecture**: Repository pattern with `IUnitOfWork` and `IRepository<T>`
- **Auth**: ASP.NET Core Identity (seed admin via `SeedAdmin` config)
- **App URL (dev)**: `http://localhost:5094`
- **Login**: `admin@mercurius.com` / `Admin@123` (from `appsettings.Development.json`)

## 2. Solution Structure
```
Mercurius.sln
├── Mercurius/                  # Main web app
│   ├── Controllers/
│   │   ├── Configurations/
│   │   │   └── ShopifySettingsController.cs
│   │   └── ShopifyController.cs
│   ├── Services/
│   │   └── ShopifyService.cs
│   ├── Views/
│   │   └── Shopify/
│   │       ├── Index.cshtml
│   │       └── ConnectionFailed.cshtml
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── Mercurius.Repo/             # Data access layer
│   └── Models/
│       └── AppSetting.cs
└── Mercurius.Tests/            # xUnit tests
```

## 3. Key Paths
- Solution: `c:\Users\markj\OneDrive - MSFT\Mercurius\Mercurius.sln`
- Main project: `Mercurius\Mercurius\`
- Repo project: `Mercurius\Mercurius.Repo\`
- Tests: `Mercurius\Mercurius.Tests\`

## 4. Shopify Integration Architecture

### 4.1 Single Source of Truth
- **AppSetting table in LiteDB** is the single source of truth for Shopify credentials
- `appsettings.json` does NOT contain Shopify config

### 4.2 Settings Keys (stored in `AppSetting` table)
- `Shopify.StoreUrl` (e.g., `veramay.myshopify.com`)
- `Shopify.AccessToken` (e.g., `shpat_...`)
- `Shopify.ApiVersion` (default `2026-04`)

### 4.3 DI Registration (in `Program.cs`)
```csharp
builder.Services.AddHttpClient("Shopify");
builder.Services.AddSingleton<ShopifySettings>(_ => new ShopifySettings());
builder.Services.AddScoped<IShopifyService, ShopifyService>();
```

### 4.4 Service Pattern
- `ShopifyService` reads credentials from `AppSetting` table on **every request** via `CreateShopifyClientAsync()`
- Does NOT cache credentials in constructor
- Uses `IHttpClientFactory` to create a fresh `HttpClient` per call
- Sets `BaseAddress` to `https://{storeUrl}/admin/api/{apiVersion}/`
- Adds `X-Shopify-Access-Token` header on each request

### 4.5 Configuration UI
- URL: `/ShopifySettings`
- Controller: `Controllers\Configurations\ShopifySettingsController.cs`
- Persists to `AppSetting` table and updates in-memory `ShopifySettings`

### 4.6 Products UI
- URL: `/Shopify`
- Controller: `Controllers\ShopifyController.cs`
- Uses `IShopifyService` for all Shopify operations

## 5. Build & Run
- Build: `dotnet build Mercurius.sln` from solution root
- Run: `dotnet run --project Mercurius\Mercurius\Mercurius.csproj` (or use `run_app.ps1`)
- App listens on `http://localhost:5094` in Development

## 6. Known Issues / Lessons Learned
- `ShopifySettings` POCO singleton mutation does NOT propagate reliably to `ShopifyService` — always read from `AppSetting` table per request
- API version `2024-01` is retired by Shopify — use `2026-04` or later
- `ConnectionFailed.cshtml` view still references `appsettings.json` in error message — needs update to point to `/ShopifySettings`

## 7. Recent Refactor (Shopify Credentials)
### Problem
- `appsettings.json` contained placeholder values
- `ShopifyService` captured `ShopifySettings` once in constructor and baked `BaseAddress` + `X-Shopify-Access-Token` header into the HttpClient
- Even after saving valid credentials via UI, service still used stale values

### Solution
- Refactored `ShopifyService` to use `IHttpClientFactory` and read credentials from `AppSetting` table on every request
- Removed `IConfiguration.GetSection("Shopify")` binding from `Program.cs`
- Removed `"Shopify"` section from `appsettings.json`

### Validation
- Build: green (0 warnings, 0 errors)
- App starts successfully on port 5094
- Login works
- `/ShopifySettings` loads with credentials
- Test Connection returns 404 — needs investigation (likely invalid token or closed store)
