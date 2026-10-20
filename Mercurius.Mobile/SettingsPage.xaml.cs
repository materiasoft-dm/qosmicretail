using Mercurius.Mobile.Models;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class SettingsPage : ContentPage
{
    private readonly SyncApiService _syncApiService;
    private readonly DeviceSettingsService _deviceSettingsService;
    private readonly BirService _birService;
    private bool _loadingWidth;

    public SettingsPage(SyncApiService syncApiService, DeviceSettingsService deviceSettingsService, BirService birService)
    {
        InitializeComponent();
        _syncApiService = syncApiService;
        _deviceSettingsService = deviceSettingsService;
        _birService = birService;
        ReceiptWidthPicker.ItemsSource = DeviceSettingsService.ReceiptWidths.Select(w => $"{w} characters").ToList();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TerminalNameEntry.Text = _deviceSettingsService.GetTerminalName();
        RefreshCurrentClinicLabel();
        LoadReceiptWidth();
        await LoadBirRegistrationAsync(refreshFromServer: true);
        await LoadClinicsAsync();
    }

    // ---------------------------------------------------------------- BIR registration

    private async Task LoadBirRegistrationAsync(bool refreshFromServer)
    {
        var terminal = await _birService.GetTerminalAsync();
        if (refreshFromServer)
        {
            // So the permission and the server's copy of the MIN/PTU are current; offline, the cached copy does.
            try { terminal = await _birService.RefreshTerminalAsync(); }
            catch { /* offline */ }
        }
        ShowBirRegistration(terminal);
    }

    private void ShowBirRegistration(Data.LocalTerminal? t)
    {
        var registered = t is { TerminalId: > 0 };
        BirStatusLabel.Text = !registered
            ? "Not registered yet: connect and sync"
            : string.IsNullOrWhiteSpace(t!.PtuNumber) || string.IsNullOrWhiteSpace(t.Min)
                ? $"Terminal {t.TerminalCode}: waiting for its MIN and PTU"
                : $"Terminal {t.TerminalCode}: registered (MIN {t.Min})";
        BirSellerLabel.Text = !registered
            ? ""
            : $"{(string.IsNullOrWhiteSpace(t!.SellerRegisteredName) ? "Seller name not set" : t.SellerRegisteredName)} · " +
              $"{(string.IsNullOrWhiteSpace(t.SellerTin) ? "TIN not set" : "TIN " + Mercurius.Shared.Bir.BirFormat.Tin(t.SellerTin, t.SellerBranchCode))} · " +
              $"{(t.SellerIsVatRegistered ? "VAT-registered" : "Non-VAT")}" +
              (string.IsNullOrWhiteSpace(t.AccreditationNumber) ? "" : $" · Accreditation {t.AccreditationNumber}");

        MinEntry.Text = t?.Min;
        SerialEntry.Text = t?.SerialNumber;
        PtuEntry.Text = t?.PtuNumber;
        PtuDateCheckBox.IsChecked = t?.PtuDateIssued != null;
        PtuDatePicker.Date = t?.PtuDateIssued ?? DateTime.Today;

        var canEdit = registered && t!.CanEditRegistration;
        MinEntry.IsEnabled = SerialEntry.IsEnabled = PtuEntry.IsEnabled = PtuDateCheckBox.IsEnabled = canEdit;
        PtuDatePicker.IsEnabled = canEdit && PtuDateCheckBox.IsChecked;
        SaveBirButton.IsVisible = canEdit;
        BirHintLabel.Text = !registered
            ? "This device registers itself as a terminal on its next sync."
            : canEdit
                ? "Enter these exactly as they appear on the Permit to Use. Receipts say \"NOT A VALID INVOICE\" until the MIN and PTU are set. The seller's name, TIN and address are set per branch on the web (Settings, Locations)."
                : "Only an administrator can change these. The seller's name, TIN and address are set per branch on the web (Settings, Locations).";
    }

    private void OnPtuDateCheckedChanged(object? sender, CheckedChangedEventArgs e) =>
        PtuDatePicker.IsEnabled = e.Value && PtuDateCheckBox.IsEnabled;

    private async void OnSaveBirClicked(object? sender, EventArgs e)
    {
        SaveBirButton.IsEnabled = false;
        try
        {
            var (saved, error) = await _birService.SaveRegistrationAsync(
                Clean(MinEntry.Text), Clean(SerialEntry.Text), Clean(PtuEntry.Text),
                PtuDateCheckBox.IsChecked ? PtuDatePicker.Date : null);
            if (!saved)
            {
                await DisplayAlertAsync("Couldn't save", error ?? "The server couldn't be reached. Try again when online.", "OK");
                return;
            }
            await LoadBirRegistrationAsync(refreshFromServer: false);
            await DisplayAlertAsync("Registration saved", "Receipts from this terminal now carry these details.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Couldn't save", "The server couldn't be reached. Try again when online.", "OK");
        }
        finally
        {
            SaveBirButton.IsEnabled = true;
        }
    }

    private void LoadReceiptWidth()
    {
        _loadingWidth = true;
        ReceiptWidthPicker.SelectedIndex = Math.Max(0, Array.IndexOf(DeviceSettingsService.ReceiptWidths, _deviceSettingsService.GetReceiptWidth()));
        _loadingWidth = false;
    }

    private void OnReceiptWidthChanged(object? sender, EventArgs e)
    {
        if (_loadingWidth || ReceiptWidthPicker.SelectedIndex < 0) return;
        _deviceSettingsService.SetReceiptWidth(DeviceSettingsService.ReceiptWidths[ReceiptWidthPicker.SelectedIndex]);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
