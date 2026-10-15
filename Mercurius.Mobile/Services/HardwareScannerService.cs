using System.Text;

namespace Mercurius.Mobile.Services;

// Lets a USB/Bluetooth barcode scanner work on the Sell screen without anyone tapping into a text
// field first. Such scanners are "keyboard wedges": they type the code as a very fast burst of
// keystrokes, then press Enter. The platform layer (Android: MainActivity.DispatchKeyEvent) feeds
// key presses here whenever no text field has focus; a fast burst ending in Enter is a scan.
// A human typing is far slower than the MaxGap, so stray key presses never assemble into a code.
public class HardwareScannerService
{
    // Scanners emit keys a few ms apart; people type 100ms+ apart.
    private static readonly TimeSpan MaxGapBetweenKeys = TimeSpan.FromMilliseconds(80);
    private const int MinCodeLength = 3;

    private readonly StringBuilder _buffer = new();
    private DateTime _lastKeyUtc = DateTime.MinValue;

    // Raised on the main thread with the scanned code.
    public event Action<string>? Scanned;

    // True only while a screen that accepts scans is visible (SalesPage sets it on appear/disappear).
    public bool IsListening { get; set; }

    // Returns true if the key press was part of a scan and should be swallowed.
    public bool OnKey(char? character, bool isEnter)
    {
        if (!IsListening) return false;

        var now = DateTime.UtcNow;
        if (now - _lastKeyUtc > MaxGapBetweenKeys)
        {
            _buffer.Clear(); // too slow to be the same scan — start over
        }
        _lastKeyUtc = now;

        if (isEnter)
        {
            if (_buffer.Length < MinCodeLength)
            {
                _buffer.Clear();
                return false;
            }
            var code = _buffer.ToString().Trim();
            _buffer.Clear();
            MainThread.BeginInvokeOnMainThread(() => Scanned?.Invoke(code));
            return true;
        }

        if (character is { } c && !char.IsControl(c))
        {
            _buffer.Append(c);
            return true;
        }
        return false;
    }

    // Whether a hardware keyboard-type device (which is how scanners present themselves) is
    // attached. Windows POS terminals are assumed to have one; Android checks for a physical,
    // non-built-in keyboard device.
    public bool IsScannerLikelyConnected()
    {
#if ANDROID
        foreach (var id in Android.Views.InputDevice.GetDeviceIds() ?? Array.Empty<int>())
        {
            var device = Android.Views.InputDevice.GetDevice(id);
            if (device == null || device.IsVirtual) continue;
            if (!device.Sources.HasFlag(Android.Views.InputSourceType.Keyboard)) continue;
            if (device.KeyboardType != Android.Views.InputKeyboardType.Alphabetic) continue;
            if (OperatingSystem.IsAndroidVersionAtLeast(29) && !device.IsExternal) continue;
            return true;
        }
        return false;
#elif WINDOWS
        return true;
#else
        return false;
#endif
    }
}
