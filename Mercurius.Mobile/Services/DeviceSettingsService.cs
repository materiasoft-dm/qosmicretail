namespace Mercurius.Mobile.Services;

// Settings that belong to this physical device, not to whichever user happens to be signed in on
// it — the opposite of SessionService, which is wiped on logout (see AppShell.xaml.cs's
// OnLogoutClicked). Uses Preferences rather than SecureStorage for exactly that reason: nothing
// here is a secret, and it needs to survive a logout/re-login by a different cashier on the same
// shared POS terminal. The clinic assignment (SelectedLocationId/Name) is the first thing this
// holds — see SettingsPage.
public class DeviceSettingsService
{
    private const string LocationIdKey = "Device_ClinicLocationId";
    private const string LocationNameKey = "Device_ClinicLocationName";

    // 0 = not yet assigned to a clinic. SyncController.PushInvoices falls back to the old
    // per-user location resolution when it sees this, so an unconfigured device doesn't silently
    // fail to sell — it just behaves as it did before this setting existed.
    public int GetSelectedLocationId() => Preferences.Default.Get(LocationIdKey, 0);

    public string GetSelectedLocationName() => Preferences.Default.Get(LocationNameKey, string.Empty);

    public void SetSelectedLocation(int id, string name)
    {
        Preferences.Default.Set(LocationIdKey, id);
        Preferences.Default.Set(LocationNameKey, name);
    }

    private const string TerminalNameKey = "Device_TerminalName";

    // What staff call this terminal ("Front Counter", "Back Office Tablet"). Shown on every
    // terminal's Receipts list next to the sales it made, and recorded in the server's audit
    // trail. Defaults to the OS device name, which on Android is usually just the model name.
    public string GetTerminalName()
    {
        var name = Preferences.Default.Get(TerminalNameKey, string.Empty);
        return string.IsNullOrWhiteSpace(name) ? DeviceInfo.Name : name;
    }

    public void SetTerminalName(string name) => Preferences.Default.Set(TerminalNameKey, name.Trim());

    private const string DeviceIdKey = "Device_Id";

    // A stable, random per-install identifier, sent with every request so the server's audit
    // trail can tell terminals apart even when the same cashier account is used on several.
    // Generated once and kept across logouts (it identifies the device, not the user).
    public string GetDeviceId()
    {
        var id = Preferences.Default.Get(DeviceIdKey, string.Empty);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(DeviceIdKey, id);
        }
        return id;
    }
}
