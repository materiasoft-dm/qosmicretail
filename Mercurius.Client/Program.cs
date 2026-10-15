using Mercurius.Client;
using Mercurius.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same-origin API: the browser sends the sign-in cookie with every call, so no token handling.
// A 401 means the session ended while the page was open — go back to the sign-in page.
builder.Services.AddScoped(sp => new HttpClient(new SessionExpiredHandler(sp.GetRequiredService<NavigationManager>())
{
    InnerHandler = new HttpClientHandler()
})
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// The signed-in user comes from the server's own sign-in (serialized into the page when it was
// rendered), so there's nothing to fetch at startup.
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddScoped<ICurrentUserProfileSource, HttpCurrentUserProfileSource>();
builder.Services.AddMercuriusClientServices();

await builder.Build().RunAsync();
