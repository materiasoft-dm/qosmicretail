using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class MainPage : ContentPage
{
    private readonly SessionService _sessionService;
    private readonly LocalDatabase _localDatabase;

    public MainPage(SessionService sessionService, LocalDatabase localDatabase)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _localDatabase = localDatabase;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var fullName = await _sessionService.GetFullNameAsync();
        GreetingLabel.Text = string.IsNullOrWhiteSpace(fullName) ? "Welcome" : $"Welcome, {fullName.Split(',').Last().Trim()}";

        var products = await _localDatabase.GetProductsAsync();
        ProductCountLabel.Text = products.Count.ToString("N0");

        var lastSynced = await _localDatabase.GetLastSyncedAsync("products");
        LastSyncedLabel.Text = lastSynced.HasValue ? lastSynced.Value.ToLocalTime().ToString("MMM d, h:mm tt") : "Never";
    }

    private async void OnGoToProductsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ProductsPage");
    }
}
