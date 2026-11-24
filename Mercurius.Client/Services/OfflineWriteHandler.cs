using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace Mercurius.Client.Services
{
    /// <summary>
    /// Lets the business screens keep working without a connection. A change a page makes (a refund, a
    /// stock adjustment, a received shipment, a new customer, ...) goes through here:
    ///   - it's sent with an Idempotency-Key, so a resend can never apply it twice;
    ///   - if the server can't be reached (no connection, or no answer within 15 s) or the sign-in has
    ///     expired, the request is saved to the outbox instead and the page is told "accepted" (202), so it
    ///     carries on as if it had worked: SyncService sends it, in order, when it can.
    /// Only the list below can be queued. Anything that needs the answer to carry on (creating a product or
    /// a purchase order, whose new id the page opens) or that shouldn't be changed from a laptop with no
    /// connection (users, roles, settings, security) is sent normally and simply fails when offline.
    /// A refusal from the server (400, 403, 404, 409...) is never queued: it comes back to the page as usual.
    /// </summary>
    public sealed class OfflineWriteHandler : DelegatingHandler
    {
        /// <summary>Set on requests that must go straight to the server (SyncService's own, which resend the outbox).</summary>
        public static readonly HttpRequestOptionsKey<bool> Bypass = new("MercuriusOfflineBypass");

        public const string QueuedHeader = "X-Mercurius-Queued";
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

        private sealed record Queueable(Regex Path, string Method, string Kind, string Label);

        private static Queueable Q(string method, string pattern, string kind, string label) =>
            new(new Regex("^/?" + pattern + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), method, kind, label);

        private static readonly Queueable[] Allowed =
        {
            Q("POST", "api/invoice-list/refund", "refund", "Refund"),
            Q("POST", "api/adjustments", "adjustment", "Stock adjustment"),
            Q("POST", "api/shipments", "shipment", "Shipment received"),
            Q("POST", "api/customers", "customer", "New customer"),
            Q("PUT", @"api/customers/\d+", "customer", "Customer update"),
            Q("PUT", @"api/products/\d+", "product", "Product edit"),
            Q("POST", "api/suppliers", "supplier", "New supplier"),
            Q("PUT", @"api/suppliers/\d+", "supplier", "Supplier update"),
            Q("POST", "api/medicine-batches", "batch", "New batch"),
            Q("DELETE", @"api/medicine-batches/\d+", "batch", "Remove batch"),
            Q("POST", "api/wholesale", "wholesale", "Wholesale order"),
            Q("POST", @"api/wholesale/\d+/payments", "wholesale-payment", "Wholesale payment"),
            Q("POST", @"api/purchase-orders/\d+/(approve|mark-sent)", "purchase-order", "Purchase order update"),
        };

        private readonly IServiceProvider _services;

        public OfflineWriteHandler(IServiceProvider services) => _services = services;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bypass = request.Options.TryGetValue(Bypass, out var b) && b;
            var match = bypass || request.RequestUri == null ? null
                : Allowed.FirstOrDefault(a => a.Method == request.Method.Method && a.Path.IsMatch(request.RequestUri.AbsolutePath));
            if (match == null) return await base.SendAsync(request, cancellationToken);

            // The body is read first: it has to be kept to resend, and a failed send consumes it.
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var id = Guid.NewGuid().ToString("N");
            request.Headers.TryAddWithoutValidation(IdempotencyHeader, id);

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(Wait);
            try
            {
                var response = await base.SendAsync(request, limit.Token);
                if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
                // The sign-in expired mid-form. Keep the change rather than lose it to a redirect; it's sent once the person signs in.
                response.Dispose();
                return await QueueAsync(request, match, id, body, authRequired: true);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // No connection, or no answer in time. The server may even have got it: the same
                // Idempotency-Key makes the resend safe either way.
                return await QueueAsync(request, match, id, body, authRequired: false);
            }
        }

        private const string IdempotencyHeader = "Idempotency-Key";

        private async Task<HttpResponseMessage> QueueAsync(HttpRequestMessage request, Queueable match, string id, string body, bool authRequired)
        {
            var sync = _services.GetRequiredService<SyncService>();
            await sync.QueueAsync(new OutboxEntry
            {
                Id = id,
                Kind = match.Kind,
                Label = Describe(match, body),
                Method = request.Method.Method,
                Url = request.RequestUri!.PathAndQuery.TrimStart('/'),
                Body = body
            }, authRequired);

            var accepted = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                RequestMessage = request,
                Content = new StringContent("{\"queued\":true}", Encoding.UTF8, "application/json")
            };
            accepted.Headers.Add(QueuedHeader, "true");
            return accepted;
        }

        // "Product edit · Paracetamol 500mg": the Outbox page lists these with no other context.
        private static string Describe(Queueable match, string body)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                string? Text(string name) => doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
                var detail = match.Kind switch
                {
                    "product" or "supplier" => Text("name"),
                    "customer" => $"{Text("firstName")} {Text("lastName")}".Trim(),
                    _ => null
                };
                return string.IsNullOrWhiteSpace(detail) ? match.Label : $"{match.Label} · {detail}";
            }
            catch (System.Text.Json.JsonException)
            {
                return match.Label;
            }
        }

        /// <summary>True when the server never saw this: the response is the handler's "saved for later" answer.</summary>
        public static bool WasQueued(HttpResponseMessage response) => response.Headers.Contains(QueuedHeader);
    }
}
