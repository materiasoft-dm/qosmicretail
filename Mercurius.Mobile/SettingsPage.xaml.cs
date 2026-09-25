using Mercurius.Mobile.Models;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class SettingsPage : ContentPage
{
    private readonly SyncApiService _syncApiService;
    private readonly DeviceSettingsService _deviceSettingsService;

    public SettingsPage(SyncApiService syncApiService, DeviceSettingsService deviceSettingsService)
    {
        InitializeComponent();
        _syncApiService = syncApiService;
        _deviceSettingsService = deviceSettingsService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TerminalNameEntry.Text = _deviceSettingsService.GetTerminalName();
        RefreshCurrentClinicLabel();
        await LoadClinicsAsync();
    }

    private async void OnSaveTerminalNameClicked(object? sender, EventArgs e)
    {
        var name = TerminalNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlertAsync("Terminal name", "Enter a name, e.g. \"Front Counter\".", "OK");
            return;
        }
        _deviceSettingsService.SetTerminalName(name);
        TerminalNameEntry.Unfocus();
        // Applies to sales made from now on; ones already uploaded keep the name they were made under.
        await DisplayAlertAsync("Terminal name saved", $"New sales from this terminal will show as \"{name}\".", "OK");
    }

    private void RefreshCurrentClinicLabel()
    {
        var name = _deviceSettingsService.GetSelectedLocationName();
        CurrentClinicLabel.Text = string.IsNullOrWhiteSpace(name) ? "Not assigned" : name;
    }

    private async Task LoadClinicsAsync()
    {
        SetBusy(true);
        try
        {
            var clinics = await _syncApiService.GetLocationsAsync();
            ClinicsCollectionView.ItemsSource = clinics;
            EmptyClinicListLayout.IsVisible = clinics.Count == 0;
            ClinicListErrorLayout.IsVisible = false;
        }
        catch
        {
            // No connection, or the server rejected the call — offer a retry rather than leaving
            // an unexplained empty list (this page's whole job depends on reaching the server, so
            // there's no offline/cached fallback the way SalesPage/ProductsPage have one).
            ClinicListErrorLayout.IsVisible = true;
            ClinicsCollectionView.ItemsSource = null;
            EmptyClinicListLayout.IsVisible = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        ClinicListBusyIndicator.IsVisible = busy;
        ClinicListBusyIndicator.IsRunning = busy;
        ClinicsCollectionView.IsVisible = !busy;
    }

    private async void OnRetryLoadClinicsClicked(object? sender, EventArgs e) => await LoadClinicsAsync();

    private async void OnClinicRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not LocationDto clinic) return;

        _deviceSettingsService.SetSelectedLocation(clinic.Id, clinic.Name);
        RefreshCurrentClinicLabel();
        await DisplayAlertAsync("Clinic Updated", $"This device is now assigned to {clinic.Name}.", "OK");
    }
}
