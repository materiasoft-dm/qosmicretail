namespace Mercurius.Mobile.Configuration;

public static class ApiConfig
{
    // Points at the live deployment so the app is testable on a real device/emulator without any
    // local-network setup. Switch to a LAN IP (e.g. http://192.168.x.x:5094) to test against a
    // locally running server instead — "localhost" from an emulator/device means the device
    // itself, not the dev machine.
    public const string BaseUrl = "http://darkmaster-002-site1.dtempurl.com";
}
