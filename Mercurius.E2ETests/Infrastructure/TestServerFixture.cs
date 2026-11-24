using System.Diagnostics;
using System.Net.Http.Json;
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
        // Same target framework folder as this test assembly (bin/Debug/<tfm>/) — both projects
        // move to a new .NET together.
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
        var dllPath = Path.Combine(_mercuriusDir, "bin", "Debug", targetFramework, "Mercurius.dll");
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
        var logPath = Path.Combine(Path.GetTempPath(), "mercurius-e2e-server.log");
        var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            outputLog.AppendLine(e.Data);
            logWriter.WriteLine(e.Data);
            if (e.Data.Contains("Now listening")) readyTcs.TrySetResult();
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            outputLog.AppendLine("[stderr] " + e.Data);
            logWriter.WriteLine("[stderr] " + e.Data);
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var timeout = Task.Delay(TimeSpan.FromSeconds(60));
        var completed = await Task.WhenAny(readyTcs.Task, timeout);
        if (completed == timeout)
        {
            // xUnit only calls DisposeAsync when InitializeAsync succeeded, so a failed start would
            // otherwise leave this process running — holding Mercurius.dll open (which then fails the
            // next `dotnet build` with MSB3027) and the day's log file locked. Kill it here.
            await StopAsync();
            throw new InvalidOperationException(
                $"Mercurius server did not start within 60s. Output so far:\n{outputLog}");
        }

        // A little extra headroom for startup seeding (roles, admin user, categories) to finish
        // before the first test tries to log in.
        await Task.Delay(500);

        await WarmUpAsync();
    }

    /// <summary>
    /// The first sign-in and the first product save on a freshly started server are slow (everything is
    /// compiled on first use: Identity's password hashing, JWT, EF queries and inserts, the audit
    /// interceptor). Whichever test ran first used to spend that on a 15-second UI timeout and fail on
    /// a cold run, then pass when run alone. This pays that cost here instead, over plain HTTP. It is
    /// best-effort: if any step fails the tests still run, just without the head start.
    /// </summary>
    private async Task WarmUpAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(120) };
            await http.GetAsync("account/login");

            var login = await http.PostAsJsonAsync("api/auth/login", new { email = AdminEmail, password = AdminPassword });
            if (!login.IsSuccessStatusCode) return;
            var token = (await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("token").GetString();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            await http.GetAsync("api/sync/products/pull");
            // One inactive product, so the insert path is warm; inactive so it never shows up in the Sell
            // page or the offline catalog.
            await http.PostAsJsonAsync("api/sync/products/push", new[]
            {
                new
                {
                    syncId = Guid.NewGuid(), productCode = "E2E-WARMUP", name = "E2E warm-up (ignore)", description = "",
                    currentCostPrice = 1m, currentSalePrice = 2m, currentStock = 0m, lowStockCount = 0m, markUpPercentage = 0m, isActive = false
                }
            });
        }
        catch
        {
            // Warm-up only: never fail a test run over it.
        }
    }

    public async Task DisposeAsync()
    {
        await StopAsync();

        foreach (var pattern in new[] { $"{_dbName}.sqlite", $"{_dbName}.sqlite-shm", $"{_dbName}.sqlite-wal" })
        {
            var path = Path.Combine(_mercuriusDir, pattern);
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }

        await Task.CompletedTask;
    }

    /// <summary>Terminates the test server, if it's still running.</summary>
    private async Task StopAsync()
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
        finally
        {
            _process?.Dispose();
            _process = null;
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
