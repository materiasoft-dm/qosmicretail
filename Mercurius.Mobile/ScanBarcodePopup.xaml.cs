using CommunityToolkit.Maui.Views;
using ZXing.Net.Maui;

namespace Mercurius.Mobile;

// Camera barcode scanner (phones/tablets). Returns the first barcode read via Close(value), or
// null if cancelled. Use BarcodeScanner.ScanAsync rather than showing this directly — it handles
// the platform check and camera permission first.
public partial class ScanBarcodePopup : Popup
{
    private bool _done;

    public ScanBarcodePopup()
    {
        InitializeComponent();
        CameraView.Options = new BarcodeReaderOptions
        {
            // Retail barcodes are 1D (EAN-13, UPC-A, Code 128...); QR/DataMatrix included for
            // pharma packs that carry a 2D code instead.
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false,
            TryHarder = true
        };
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        // Raised on a camera thread, repeatedly while a code stays in view — take the first only.
        var value = e.Results?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Value))?.Value;
        if (value == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_done) return;
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* not all devices */ }
            Finish(value.Trim());
        });
    }

    // Closing the popup alone does NOT release the camera — confirmed on the Android emulator:
    // `dumpsys media.camera` still listed this app as an active camera client after Cancel, i.e.
    // the camera (and its indicator/battery drain) stayed on. Disconnecting the view's handler is
    // what makes ZXing unbind the camera.
    private void Finish(string? result)
    {
        if (_done) return;
        _done = true;
        CameraView.IsDetecting = false;
        CameraView.IsTorchOn = false;
        try { CameraView.Handler?.DisconnectHandler(); } catch { /* best-effort */ }
        Close(result);
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        CameraView.IsTorchOn = !CameraView.IsTorchOn;
        TorchButton.Text = CameraView.IsTorchOn ? "Flashlight off" : "Flashlight";
    }

    private void OnCancelClicked(object? sender, EventArgs e) => Finish(null);

    // Typed barcodes go through the exact same lookup as a camera read — the caller can't tell
    // (and doesn't need to know) which one it was.
    private void OnManualEntrySubmitted(object? sender, EventArgs e)
    {
        var typed = ManualBarcodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(typed)) return;
        Finish(typed);
    }
}

// Entry point for camera scanning from any page or popup.
public static class BarcodeScanner
{
    // Camera scanning is for phones/tablets. The Windows POS build uses a USB/Bluetooth scanner
    // (keyboard input) instead, and ZXing has no camera support there.
    public static bool IsCameraScanAvailable =>
        DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS;

    public static async Task<string?> ScanAsync()
    {
        if (!IsCameraScanAvailable) return null;

        var host = Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (host == null) return null;

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }
        if (status != PermissionStatus.Granted)
        {
            await host.DisplayAlertAsync("Camera needed",
                "Allow camera access in your device's Settings to scan barcodes.", "OK");
            return null;
        }

        // Shown from the current page even when called from inside another popup — a
        // CommunityToolkit popup stacks above whatever is already open.
        var result = await host.ShowPopupAsync(new ScanBarcodePopup());
        return result as string;
    }
}
