using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// A change sent with an Idempotency-Key header happens once however many times it's sent (the device
/// outbox resends whatever it never got an answer for). Driven over the real HTTP API with the signed-in
/// browser session, using suppliers as a simple create endpoint.
/// </summary>
public class IdempotencyTests : BlazorTestBase
{
    public IdempotencyTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    private async Task<IAPIResponse> PostSupplierAsync(string name, string? key)
    {
        var headers = key == null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["Idempotency-Key"] = key };
        return await Context.APIRequest.PostAsync("/api/suppliers", new APIRequestContextOptions { DataObject = new { name }, Headers = headers });
    }

    private async Task<int> CountSuppliersAsync(string name)
    {
        var response = await Context.APIRequest.GetAsync($"/api/suppliers?search={Uri.EscapeDataString(name)}");
        return (await response.JsonAsync())!.Value.GetProperty("total").GetInt32();
    }

    [Fact]
    public async Task SameKeySentTwice_CreatesOnce_AndTheRepeatGetsTheFirstAnswer()
    {
        await LoginAsAdminAsync();
        var name = $"Idem Supplier {Guid.NewGuid():N}"[..28];
        var key = Guid.NewGuid().ToString("N");

        var first = await PostSupplierAsync(name, key);
        var second = await PostSupplierAsync(name, key);

        Assert.Equal(200, first.Status);
        Assert.Equal(200, second.Status);
        Assert.False(first.Headers.ContainsKey("idempotent-replayed"));
        Assert.Equal("true", second.Headers["idempotent-replayed"]);
        var firstId = (await first.JsonAsync())!.Value.GetProperty("id").GetInt32();
        var secondId = (await second.JsonAsync())!.Value.GetProperty("id").GetInt32();
        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await CountSuppliersAsync(name));
    }

    [Fact]
    public async Task DifferentKeys_AndNoKey_AreSeparateChanges()
    {
        await LoginAsAdminAsync();
        var name = $"Idem Supplier {Guid.NewGuid():N}"[..28];

        await PostSupplierAsync(name, Guid.NewGuid().ToString("N"));
        await PostSupplierAsync(name, Guid.NewGuid().ToString("N"));
        await PostSupplierAsync(name, null);

        Assert.Equal(3, await CountSuppliersAsync(name));
    }

    [Fact]
    public async Task ARefusal_IsReplayedToo_SoAResendGetsTheSameAnswer()
    {
        await LoginAsAdminAsync();
        var key = Guid.NewGuid().ToString("N");

        var first = await PostSupplierAsync("", key);
        var second = await PostSupplierAsync("", key);

        Assert.Equal(400, first.Status);
        Assert.Equal(400, second.Status);
        Assert.Equal("true", second.Headers["idempotent-replayed"]);
        Assert.Equal(await first.TextAsync(), await second.TextAsync());
    }

    [Fact]
    public async Task AnOverlongKey_IsRefused()
    {
        await LoginAsAdminAsync();
        var response = await PostSupplierAsync("Too Long Key Supplier", new string('k', 200));
        Assert.Equal(400, response.Status);
    }
}
