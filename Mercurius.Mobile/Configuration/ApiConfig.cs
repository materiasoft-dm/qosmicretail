namespace Mercurius.Mobile.Configuration;

public static class ApiConfig
{
    public const string LiveBaseUrl = "http://darkmaster-002-site1.dtempurl.com";

#if DEBUG
    // Debug builds: points at the live deployment so the app is testable on a real device/emulator
    // without any local-network setup. Switch to a LAN IP (e.g. http://192.168.x.x:5094) to test
    // against a locally running server instead — "localhost" from an emulator/device means the
    // device itself, not the dev machine.
    //
    // For Mercurius.Mobile.UITests specifically, point this at "http://10.0.2.2:5094" instead
    // (the Android emulator's host-loopback alias) so the suite runs against a local dev server
    // with known, controlled test data rather than live — see CLAUDE.md's E2E section.
    //
    // TEMPORARY — Mercurius.Mobile.UITests.Windows run in progress: the Windows build runs
    // natively on the same machine as the dev server, so this points straight at "localhost"
    // (no emulator alias needed, see CLAUDE.md's Windows E2E section). REVERT to LiveBaseUrl
    // before committing.
    public const string BaseUrl = "http://localhost:5094";
#else
    // Release builds — the downloadable Windows app and any store build — always talk to the live
    // site, whatever the Debug value above is temporarily switched to for local testing. A
    // distributed build can never ship pointing at a developer's machine.
    public const string BaseUrl = LiveBaseUrl;
#endif
}
