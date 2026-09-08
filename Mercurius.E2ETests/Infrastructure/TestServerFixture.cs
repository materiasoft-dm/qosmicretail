using System.Diagnostics;
using System.Net.Sockets;
using Xunit;

namespace Mercurius.E2ETests.Infrastructure;

/// <summary>
/// Launches a real instance of the Mercurius web app (already-built output, not a rebuild) on a
/// free port, pointed at a throwaway SQLite database unique to this test run — never the
/// developer's own mercurius.sqlite, and never the live site. ASPNETCORE_ENVIRONMENT is forced to
/// Development so appsettings.Development.json's seed admin credentials (admin@mercurius.com /
/// Admin@123) are actually loaded — the base appsettings.json ships those blank on purpose (see
/// CLAUDE.md's "do not touch live database data" / secrets-splitting notes).
/// </summary>
public class TestServerFixture : IAsyncLifetime
{
    public string BaseUrl { get; private set; } = "";
    public const string AdminEmail = "admin@mercurius.com";
    public const string AdminPassword = "Admin@123";

    private Process? _process;
    private string _dbName = "";
    private string _mercuriusDir = "";

    public async Task InitializeAsync()
    {
        _mercuriusDir = FindMercuriusProjectDir();
        var dllPath = Path.Combine(_mercuriusDir, "bin", "Debug", "net9.0", "Mercurius.dll");
        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"Mercurius.dll not found at '{dllPath}'. Build the Mercurius project first: " +
                "cd Mercurius && dotnet build");
        }

        var port = GetFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _dbName = $"e2e_{Guid.NewGuid():N}";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" --urls {BaseUrl}",
            WorkingDirectory = _mercuriusDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["MERCURIUS_DB"] = _dbName;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Mercurius process.");

        var readyTcs = new TaskCompletionSource();
        var outputLog = new System.Text.StringBuilder();
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            outputLog.AppendLine(e.Data);
            if (e.Data.Contains("Now listening")) readyTcs.TrySetResult();
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) outputLog.AppendLine("[stderr] " + e.Data);
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var timeout = Task.Delay(TimeSpan.FromSeconds(60));
        var completed = await Task.WhenAny(readyTcs.Task, timeout);
        if (completed == timeout)
        {
            throw new InvalidOperationException(
                $"Mercurius server did not start within 60s. Output so far:\n{outputLog}");
        }

        // A little extra headroom for startup seeding (roles, admin user, categories) to finish
        // before the first test tries to log in.
        await Task.Delay(500);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch { /* best-effort cleanup */ }
        _process?.Dispose();

        foreach (var pattern in new[] { $"{_dbName}.sqlite", $"{_dbName}.sqlite-shm", $"{_dbName}.sqlite-wal" })
        {
            var path = Path.Combine(_mercuriusDir, pattern);
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }

        await Task.CompletedTask;
    }

    private static string FindMercuriusProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Mercurius");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Mercurius.csproj")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate the Mercurius project directory by walking up from '{AppContext.BaseDirectory}'.");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
