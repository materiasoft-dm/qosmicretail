using Mercurius.Client.Services;
using Mercurius.Common;
using Mercurius.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Mercurius.Client
{
    public static class ClientServices
    {
        public const string PlatformAdminPolicy = "PlatformAdmin";

        /// <summary>
        /// Everything the app's pages inject. Registered in the browser (the real thing) and on the
        /// server too, because pages are first rendered there (prerendering) before WebAssembly
        /// takes over. The HttpClient, and the source of the signed-in user's profile, differ per
        /// side and are registered by each host.
        /// </summary>
        public static IServiceCollection AddMercuriusClientServices(this IServiceCollection services)
        {
            services.AddMudServices();

            // The same policies as the server's: one per ModuleRegistry page, satisfied by the
            // matching access.pages claim, plus PlatformAdmin. They only decide what the UI shows —
            // every API call is still authorized by the server itself.
            services.AddAuthorizationCore(options =>
            {
                foreach (var module in ModuleRegistry.Modules)
                {
                    options.AddPolicy(module, policy => policy.RequireClaim(MercuriusClaimTypes.AccessPages, module));
                }
                options.AddPolicy(PlatformAdminPolicy, policy => policy.RequireClaim(MercuriusClaimTypes.IsPlatformAdmin, "true"));
            });
            services.AddCascadingAuthenticationState();

            services.AddScoped<CurrentUserState>();
            services.AddScoped<ProductsApiClient>();
            services.AddScoped<CustomersApiClient>();
            services.AddScoped<SuppliersApiClient>();
            services.AddScoped<InvoicesApiClient>();
            services.AddScoped<AdjustmentReasonsApiClient>();
            services.AddScoped<ProductCategoriesApiClient>();
            services.AddScoped<LocationsApiClient>();
            services.AddScoped<CategoryFieldsApiClient>();
            services.AddScoped<RefundReasonsApiClient>();
            services.AddScoped<AdjustmentsApiClient>();
            services.AddScoped<PurchaseOrdersApiClient>();
            services.AddScoped<ShipmentsApiClient>();
            services.AddScoped<MedicineBatchesApiClient>();
            services.AddScoped<RolesApiClient>();
            services.AddScoped<UsersApiClient>();
            services.AddScoped<LogsApiClient>();
            services.AddScoped<AuditTrailApiClient>();
            services.AddScoped<AdminToolsApiClient>();
            services.AddScoped<DashboardApiClient>();
            services.AddScoped<SalesApiClient>();
            services.AddScoped<AccountApiClient>();
            services.AddScoped<OfflineStoreService>();
            services.AddScoped<OfflineSyncService>();
            services.AddScoped<PwaService>();
            return services;
        }
    }
}
