using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

// End of Day: X- and Z-readings and the e-journal export for this terminal (BirService does the work).
public partial class ReadingsPage : ContentPage
{
    private readonly BirService _birService;
    private readonly LocalDatabase _localDatabase;
    private readonly SyncCoordinator _syncCoordinator;

    public ReadingsPage(BirService birService, LocalDatabase localDatabase, SyncCoordinator syncCoordinator)
    {
        InitializeComponent();
        _birService = birService;
        _localDatabase = localDatabase;
        _syncCoordinator = syncCoordinator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var terminal = await _birService.GetTerminalAsync();
        var registered = terminal is { TerminalId: > 0 };
        NotRegisteredLabel.IsVisible = !registered;
        XReadingButton.IsEnabled = ZReadingButton.IsEnabled = ExportJournalButton.IsEnabled = registered;
        HeaderSubtitleLabel.Text = registered ? $"Terminal {terminal!.TerminalCode} · {terminal.Name}" : "X- and Z-readings";

        if (registered)
        {
            var pending = await _localDatabase.GetSalesAwaitingZReadingAsync(terminal!.TerminalId);
            var net = pending.Sum(s => s.GrandTotal);
            PendingSummaryLabel.Text = pending.Count == 0
                ? "No sales yet"
                : $"{pending.Count} sale{(pending.Count == 1 ? "" : "s")} · ₱{net:N2}";
            GrandTotalLabel.Text = $"Z-counter {terminal.LastZCounter} · Accumulated grand total ₱{terminal.AccumulatedGrandTotal:N2}";
        }
        else
        {
            PendingSummaryLabel.Text = "-";
            GrandTotalLabel.Text = "";
        }

        var readings = await _localDatabase.GetZReadingsAsync();
        BindableLayout.SetItemsSource(ZReadingsList, readings);
        NoZReadingsLabel.IsVisible = readings.Count == 0;
    }

    private async void OnXReadingClicked(object? sender, EventArgs e)
    {
        var text = await _birService.BuildXReadingAsync();
        if (text == null) return;
        await this.ShowPopupAsync(new ReceiptPopup("X-reading", "Totals since the last Z-reading. Nothing was reset.", text));
    }

    private async void OnZReadingClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync("Close the day?",
            "The Z-reading totals every sale since the last one, adds them to the grand total and starts a new day. Do it once, after the day's last sale.",
            "Take Z-reading", "Cancel");
        if (!confirmed) return;

        ZReadingButton.IsEnabled = false;
        try
        {
            var reading = await _birService.TakeZReadingAsync();
            if (reading == null) return;
            await RefreshAsync();
            _ = _syncCoordinator.SyncNowAsync(); // uploads it; stays queued if offline
            await this.ShowPopupAsync(new ReceiptPopup($"Z-reading {reading.ZCounter}", reading.Subtitle, reading.ReportText ?? ""));
        }
        finally
        {
            ZReadingButton.IsEnabled = true;
        }
    }

    private async void OnZReadingTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not LocalZReading reading) return;
        await this.ShowPopupAsync(new ReceiptPopup($"Z-reading {reading.ZCounter}", reading.Subtitle, reading.ReportText ?? ""));
    }

    private async void OnExportJournalClicked(object? sender, EventArgs e)
    {
        var path = await _birService.ExportJournalAsync();
        if (path == null)
        {
            await DisplayAlertAsync("E-journal", "There are no invoices on this terminal yet.", "OK");
            return;
        }
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "E-journal", File = new ShareFile(path) });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("E-journal", $"Couldn't share the file: {ex.Message}", "OK");
        }
    }
}
