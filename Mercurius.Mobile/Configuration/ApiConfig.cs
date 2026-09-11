namespace Mercurius.Mobile.Configuration;

public static class ApiConfig
{
    // Points at the live deployment so the app is testable on a real device/emulator without any
    // local-network setup. Switch to a LAN IP (e.g. http://192.168.x.x:5094) to test against a
    // locally running server instead — "localhost" from an emulator/device means the device
    // itself, not the dev machine.
    //
    // For Mercurius.Mobile.UITests specifically, point this at "http://10.0.2.2:5094" instead
    // (the Android emulator's host-loopback alias) so the suite runs against a local dev server
    // with known, controlled test data rather than live — see CLAUDE.md's E2E section.
    public const string BaseUrl = "http://darkmaster-002-site1.dtempurl.com";
}
