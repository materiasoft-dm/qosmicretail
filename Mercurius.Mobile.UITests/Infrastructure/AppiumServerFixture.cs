using System.Diagnostics;
using System.Net.Sockets;

namespace Mercurius.Mobile.UITests.Infrastructure;

// Launches the globally-installed Appium server (npm i -g appium; appium driver install
// uiautomator2 — see CLAUDE.md) as a subprocess for the duration of the test run, the same way
// Mercurius.E2ETests' TestServerFixture launches the web app's own process rather than assuming
// one is already running elsewhere.
public sealed class AppiumServerFixture : IAsyncDisposable
{
    private Process? _process;
    public int Port { get; private set; }
    public string LogPath { get; }

    public AppiumServerFixture()
    {
        LogPath = Path.Combine(Path.GetTempPath(), "mercurius-appium-server.log");
    }

    public async Task StartAsync()
    {
        Port = GetFreePort();

        var appiumCmd = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm", "appium.cmd");
        if (!File.Exists(appiumCmd))
        {
            throw new InvalidOperationException(
                $"Appium executable not found at {appiumCmd}. Install it with 'npm install -g appium' " +
                "and the driver with 'appium driver install uiautomator2' (see CLAUDE.md).");
        }

        var psi = new ProcessStartInfo
        {
            FileName = appiumCmd,
            Arguments = $"--port {Port} --log-level info",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            EnvironmentVariables =
            {
                ["ANDROID_HOME"] = @"C:\Users\markj\AppData\Local\Android\SdkExtra",
                ["JAVA_HOME"] = @"C:\Program Files\Android\openjdk\jdk-21.0.8"
            }
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var logWriter = new StreamWriter(LogPath, append: false) { AutoFlush = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) logWriter.WriteLine(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data != null) logWriter.WriteLine(e.Data); };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitUntilReadyAsync();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync($"http://127.0.0.1:{Port}/status");
                if (response.IsSuccessStatusCode) return;
            }
            catch
            {
                // Not up yet — keep polling.
            }
            await Task.Delay(500);
        }
        throw new TimeoutException(
            $"Appium server didn't become ready on port {Port} within 60s. Check {LogPath} for details.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public ValueTask DisposeAsync()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort — the process may have already exited.
            }
        }
        _process?.Dispose();
        return ValueTask.CompletedTask;
    }
}
