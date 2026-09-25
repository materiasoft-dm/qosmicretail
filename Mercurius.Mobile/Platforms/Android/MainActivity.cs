using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

// ConfigChanges includes Keyboard/KeyboardHidden/Navigation: a USB/Bluetooth barcode scanner
// presents itself as a hardware keyboard, and without these, plugging one in (or it reconnecting
// after sleep) is a configuration change that makes Android destroy and recreate the activity —
// restarting the app mid-sale.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public class MainActivity : MauiAppCompatActivity
{
    private HardwareScannerService? _scanner;

    // Routes hardware-scanner keystrokes to HardwareScannerService whenever no text field has
    // focus, so the Sell screen accepts a scan at any time without tapping the search box first
    // (which would also pop up the on-screen keyboard). When a text field DOES have focus, keys go
    // to it as normal — e.g. the Sell search box handles a scan itself via its Completed event.
    // Popups are separate dialog windows and don't pass through here at all.
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e != null && e.Action == KeyEventActions.Down && CurrentFocus is not EditText)
        {
            _scanner ??= IPlatformApplication.Current?.Services.GetService<HardwareScannerService>();
            if (_scanner != null)
            {
                var isEnter = e.KeyCode is Keycode.Enter or Keycode.NumpadEnter;
                char? character = e.UnicodeChar > 0 ? (char)e.UnicodeChar : null;
                if (_scanner.OnKey(character, isEnter))
                {
                    return true;
                }
            }
        }
        return base.DispatchKeyEvent(e);
    }
}
