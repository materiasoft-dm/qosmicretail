using CommunityToolkit.Maui.Views;

namespace Mercurius.Mobile;

// Shows a BIR document (an invoice, a reprint, an X- or Z-reading) exactly as it prints.
// "Print / Share" hands the text to the OS share sheet: Bluetooth thermal printer apps on
// Android (e.g. RawBT) and Windows print targets accept it there, so no printer driver is
// built into the app.
public partial class ReceiptPopup : Popup
{
    private readonly string _text;
    private readonly string _title;

    public ReceiptPopup(string title, string? subtitle, string text)
    {
        InitializeComponent();
        _title = title;
        _text = text;
        TitleLabel.Text = title;
        SubtitleLabel.Text = subtitle;
        SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subtitle);
        ReceiptLabel.Text = text;
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest { Title = _title, Text = _text });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sharing the receipt failed: {ex.Message}");
        }
    }

    private void OnDoneClicked(object? sender, EventArgs e) => Close(null);
}
