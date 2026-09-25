using System.Diagnostics;
using System.Net.Sockets;

namespace Mercurius.Mobile.UITests.Windows.Infrastructure;

// Launches WinAppDriver.exe (Microsoft's WebDriver-protocol server for WinUI3/UWP/WPF/Win32 apps
// — the Windows counterpart to Appium's UiAutomator2 server for Android) as a subprocess for the
// duration of the test run. Mirrors Mercurius.Mobile.UITests' AppiumServerFixture exactly: one
// server process per run, a free port so parallel runs on the same machine don't collide, and a
// poll-until-ready startup instead of a fixed delay.
//
// One-time machine setup this depends on (see the "Windows mobile end-to-end tests" section this
// was added to CLAUDE.md alongside):
//   1. Install WinAppDriver: `winget install --id Microsoft.WindowsApplicationDriver`. The
//      installer needs an interactive UAC consent — if it hangs, that's why; approve the prompt
//      or run it from an already-elevated shell.
//   2. Enable Windows "Developer Mode" (Settings > Privacy & security > For developers) — WinAppDriver
//      refuses to drive apps without it, and this setting itself needs admin rights to flip.
public sealed class WinAppDriverServerFixture : IAsyncDisposable
{
    // Default install location for both the winget package and the standalone MSI.
    private static readonly string[] CandidatePaths =
    {
        @"C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe",
        @"C:\Program Files\Windows Application Driver\WinAppDriver.exe"
    };

    private Process? _process;
    public int Port { get; private set; }
    public string LogPath { get; }

    public WinAppDriverServerFixture()
    {
        LogPath = Path.Combine(Path.GetTempPath(), "mercurius-winappdriver-server.log");
    }

    public async Task StartAsync()
    {
        var exePath = CandidatePaths.FirstOrDefault(File.Exists);
        if (exePath == null)
        {
            throw new InvalidOperationException(
                "WinAppDriver.exe not found. Install it with " +
                "'winget install --id Microsoft.WindowsApplicationDriver' (needs interactive UAC " +
                "approval), and make sure Windows Developer Mode is enabled " +
                "(Settings > Privacy & security > For developers) — see WinAppDriverServerFixture's " +
                "class comment.");
        }

        Port = GetFreePort();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"127.0.0.1 {Port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // WinAppDriver.exe blocks on a console ReadLine() ("Press ENTER to exit.") to know
            // when to shut down. Without this, it inherits the parent process's stdin — which,
            // under a non-interactive test runner, is already at EOF — so that read returns
            // immediately and WinAppDriver exits right after printing "listening for requests",
            // before WaitUntilReadyAsync's first poll ever lands. Redirecting gives it its own
            // pipe that we simply never write to or close, so the read blocks for the process's
            // entire lifetime instead.
            RedirectStandardInput = true,
            CreateNoWindow = true
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
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"WinAppDriver exited immediately (code {_process.ExitCode}). Check {LogPath} — " +
                    "this usually means Developer Mode isn't enabled, or another WinAppDriver " +
                    $"instance already owns port {Port}.");
            }

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
            $"WinAppDriver didn't become ready on port {Port} within 30s. Check {LogPath} for details.");
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
