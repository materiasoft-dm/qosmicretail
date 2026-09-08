using System.Net.Http.Headers;

namespace Mercurius.Mobile.Services;

// Attaches the stored JWT to every request made through the "MercuriusApi" named HttpClient,
// so individual API services never have to handle the Authorization header themselves.
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly SessionService _sessionService;

    public AuthHeaderHandler(SessionService sessionService)
    {
        _sessionService = sessionService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _sessionService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
