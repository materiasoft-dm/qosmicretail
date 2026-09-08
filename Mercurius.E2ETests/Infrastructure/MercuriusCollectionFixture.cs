using Xunit;

namespace Mercurius.E2ETests.Infrastructure;

/// <summary>Combines the server + browser fixtures into one so every test class shares a single
/// running instance of the app and a single browser process for the whole run.</summary>
public class MercuriusCollectionFixture : IAsyncLifetime
{
    public TestServerFixture Server { get; } = new();
    public BrowserFixture Browser { get; } = new();

    public async Task InitializeAsync()
    {
        await Server.InitializeAsync();
        await Browser.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        await Server.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class MercuriusCollection : ICollectionFixture<MercuriusCollectionFixture>
{
    public const string Name = "Mercurius E2E";
}
