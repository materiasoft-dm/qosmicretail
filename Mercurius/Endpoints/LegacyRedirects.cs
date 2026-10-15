using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Mercurius.Endpoints
{
    /// <summary>
    /// Permanent redirects from the old MVC site's URLs (and the Blazor app's previous /app base
    /// path) to the current pages, so bookmarks, the installed web app and old links keep working.
    /// Paths that only differ in letter case (e.g. /Products → /products) need no rule — routing
    /// is case-insensitive.
    /// </summary>
    public static class LegacyRedirects
    {
        private static readonly (Regex From, string To)[] Rules =
        {
            (Rule(@"^/app/?$"), "/"),
            (Rule(@"^/app/(?<rest>.+)$"), "/${rest}"),
            (Rule(@"^/home(/index)?/?$"), "/"),
            (Rule(@"^/identity/account/login/?$"), "/account/login"),
            (Rule(@"^/identity/account/(logout|register)/?$"), "/account/login"),
            (Rule(@"^/identity/account/manage(/.*)?$"), "/account/settings"),
            (Rule(@"^/identity/account/accessdenied/?$"), "/account/access-denied"),
            (Rule(@"^/identity/account/lockout/?$"), "/account/lockout"),
            (Rule(@"^/products/index/?$"), "/products"),
            (Rule(@"^/products/create/?$"), "/products/new"),
            (Rule(@"^/products/details/(?<id>\d+)/?$"), "/products/${id}"),
            (Rule(@"^/products/(edit|delete)/(?<id>\d+)/?$"), "/products/${id}/edit"),
            (Rule(@"^/invoices/details/(?<id>\d+)/?$"), "/invoices/${id}"),
            (Rule(@"^/(quickinvoice|sales(/newsale|/index)?)/?$"), "/sell"),
            (Rule(@"^/customers/(create|index)/?$"), "/customers"),
            (Rule(@"^/suppliers/(details|edit|delete|create|index)(/\d+)?/?$"), "/suppliers"),
            (Rule(@"^/purchaseorders(/index)?/?$"), "/purchase-orders"),
            (Rule(@"^/purchaseorders/create/?$"), "/purchase-orders/new"),
            (Rule(@"^/purchaseorders/edit/(?<id>\d+)/?$"), "/purchase-orders/${id}/edit"),
            (Rule(@"^/shipment(/index|/create)?/?$"), "/shipments"),
            (Rule(@"^/adjustments/(create|index)/?$"), "/adjustments"),
            (Rule(@"^/adjustmentreasons(/.*)?$"), "/adjustment-reasons"),
            (Rule(@"^/productcategories(/.*)?$"), "/product-categories"),
            (Rule(@"^/locations/(details|edit|delete|create|index)(/\d+)?/?$"), "/locations"),
            (Rule(@"^/refundreasons(/.*)?$"), "/refund-reasons"),
            (Rule(@"^/(config/)?categoryfields(/.*)?$"), "/category-fields"),
            (Rule(@"^/rolesmanager(/.*)?$"), "/roles-manager"),
            (Rule(@"^/users(/.*)?$"), "/users-admin"),
            (Rule(@"^/logs/(viewlog|index)(/.*)?$"), "/logs"),
            (Rule(@"^/audittrail/?$"), "/audit-trail"),
            (Rule(@"^/dataquery/?$"), "/data-query"),
            (Rule(@"^/databasebackups/?$"), "/database-backups"),
            (Rule(@"^/platformadmin(/.*)?$"), "/platform-admin"),
            (Rule(@"^/settings/index/?$"), "/settings"),
            (Rule(@"^/reports/index/?$"), "/reports")
        };

        private static Regex Rule(string pattern) =>
            new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static IApplicationBuilder UseLegacyRedirects(this IApplicationBuilder app) =>
            app.Use(async (context, next) =>
            {
                var target = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                    ? Resolve(context.Request.Path.Value ?? string.Empty, context.Request.QueryString)
                    : null;
                if (target == null)
                {
                    await next();
                    return;
                }
                context.Response.Redirect(target, permanent: true);
            });

        // The new address for an old one, keeping its query string — or null if it isn't old.
        public static string? Resolve(string path, QueryString query)
        {
            // Old batch links carried the product as a query value: /MedicineBatches?productId=5.
            if (string.Equals(path.TrimEnd('/'), "/medicinebatches", StringComparison.OrdinalIgnoreCase))
            {
                var productId = System.Web.HttpUtility.ParseQueryString(query.Value ?? string.Empty)["productId"];
                return int.TryParse(productId, out var id) ? $"/medicine-batches/{id}" : "/products";
            }

            foreach (var (from, to) in Rules)
            {
                var match = from.Match(path);
                if (match.Success)
                {
                    return match.Result(to) + query.Value;
                }
            }
            return null;
        }
    }
}
