using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mercurius.Client;
using Mercurius.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// This app is served under /app (see Mercurius.Client.csproj's StaticWebAssetBasePath and
// Program.cs's app.UseBlazorFrameworkFiles("/app")) — HostEnvironment.BaseAddress reflects that
// sub-path (".../app/"), but the API controllers it calls live at the site root, not under /app.
var siteRoot = new Uri(builder.HostEnvironment.BaseAddress).GetLeftPart(UriPartial.Authority) + "/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(siteRoot) });

// Cookie-based auth, not a token: the client is same-origin with the API (ASP.NET Core hosted
// model), so the browser already attaches the existing Identity cookie to every fetch — no
// separate Blazor-side login flow needed. See CookieAuthenticationStateProvider.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddScoped<ProductsApiClient>();
builder.Services.AddScoped<CustomersApiClient>();
builder.Services.AddScoped<SuppliersApiClient>();
builder.Services.AddScoped<InvoicesApiClient>();
builder.Services.AddScoped<AdjustmentReasonsApiClient>();
builder.Services.AddScoped<ProductCategoriesApiClient>();
builder.Services.AddScoped<LocationsApiClient>();
builder.Services.AddScoped<CategoryFieldsApiClient>();
builder.Services.AddScoped<RefundReasonsApiClient>();
builder.Services.AddScoped<AdjustmentsApiClient>();
builder.Services.AddScoped<PurchaseOrdersApiClient>();
builder.Services.AddScoped<ShipmentsApiClient>();
builder.Services.AddScoped<MedicineBatchesApiClient>();
builder.Services.AddScoped<RolesApiClient>();
builder.Services.AddScoped<UsersApiClient>();
builder.Services.AddScoped<LogsApiClient>();
builder.Services.AddScoped<DashboardApiClient>();
builder.Services.AddScoped<SalesApiClient>();

await builder.Build().RunAsync();
