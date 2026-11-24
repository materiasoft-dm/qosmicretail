using Microsoft.Extensions.DependencyInjection;

namespace Mercurius.Client.Services
{
    // When the server can't be reached, the service worker answers API reads with the last response it
    // saved for that address, marked with an X-Mercurius-Offline header (when it was saved). Pages show
    // that data as usual; this tells SyncService, so the top bar can say "showing saved data from 10:42".
    // SyncService is resolved per call, not injected: it uses the same HttpClient this handler sits in.
    public sealed class OfflineDataHandler : DelegatingHandler
    {
        private readonly IServiceProvider _services;

        public OfflineDataHandler(IServiceProvider services) => _services = services;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("X-Mercurius-Offline", out var values))
            {
                DateTime? savedAt = DateTime.TryParse(values.FirstOrDefault(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed : null;
                _services.GetService<SyncService>()?.NoteSavedDataShown(savedAt);
            }
            return response;
        }
    }
}
