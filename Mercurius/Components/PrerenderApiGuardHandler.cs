using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mercurius.Components
{
    // The HttpClient the app's pages get while being prerendered on the server. Pages render their
    // layout and loading state there and fetch data only once running in the browser (they check
    // RendererInfo.IsInteractive), so any API call made here is a bug — fail loudly rather than
    // quietly calling this site over the network from itself.
    public sealed class PrerenderApiGuardHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"A page called the API ({request.Method} {request.RequestUri}) while being prerendered on the server. " +
                "Load data only when RendererInfo.IsInteractive is true.");
    }
}
