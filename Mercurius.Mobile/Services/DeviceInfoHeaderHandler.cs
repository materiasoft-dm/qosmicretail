namespace Mercurius.Mobile.Services;

// Identifies this terminal on every request (login included) so the server's audit trail records
// which device and which clinic an action came from — see the server's HttpAuditContextProvider,
// which reads these exact header names.
public class DeviceInfoHeaderHandler : DelegatingHandler
{
    private readonly DeviceSettingsService _deviceSettings;

    public DeviceInfoHeaderHandler(DeviceSettingsService deviceSettings)
    {
        _deviceSettings = deviceSettings;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-Client", "MercuriusMobile");
        request.Headers.TryAddWithoutValidation("X-Device-Id", _deviceSettings.GetDeviceId());
        // Name and hardware kept apart: the name is what staff see on receipts ("Front Counter"),
        // the model is investigation detail the audit trail appends to it.
        request.Headers.TryAddWithoutValidation("X-Device-Name", Ascii(_deviceSettings.GetTerminalName()));
        request.Headers.TryAddWithoutValidation("X-Device-Model", Ascii($"{DeviceInfo.Platform} {DeviceInfo.VersionString}, {DeviceInfo.Manufacturer} {DeviceInfo.Model}"));
        request.Headers.TryAddWithoutValidation("X-App-Version", Ascii($"{AppInfo.VersionString} ({AppInfo.BuildString})"));

        var locationId = _deviceSettings.GetSelectedLocationId();
        if (locationId > 0)
        {
            request.Headers.TryAddWithoutValidation("X-Location-Id", locationId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }

    // HttpClient refuses to send non-ASCII header values (it throws, failing the whole request) —
    // and user-chosen device names often contain them, e.g. the curly apostrophe in "Mark’s iPhone".
    private static string Ascii(string value) =>
        new(value.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
}
