namespace Mercurius.Mobile.Services;

// The one place a full sync runs from. Every trigger — sign-in, checkout, the Receipts page, a
// periodic timer, coming back online, the app returning to the foreground — calls SyncNowAsync,
// and concurrent calls share a single in-flight pass instead of racing each other (two passes
// pushing the same queued sale at once is exactly how duplicates and cursor corruption happen).
//
// Order within a pass matters:
//   1. push products  — a sale can reference a product created offline; the server can only
//                       record that line once it knows the product.
//   2. push sales     — before pulling, so our own new sales come back in the same pass already
//                       carrying their server invoice numbers.
//   3. pull products  — fresh stock/prices (the server's stock already reflects step 2).
//   4. pull receipts  — every terminal's and the web's sales.
// Each step is independent: one failing (e.g. a single bad sale, or the receipts endpoint not
// deployed yet) doesn't stop the others, and nothing is lost — push queues and pull cursors are
// persisted, so the next pass resumes exactly where this one stopped.
public class SyncCoordinator
{
    // Frequent enough that another terminal's sale shows up "soon" without anyone pressing a
    // button; cheap when nothing changed (small incremental pulls).
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromMinutes(2);

    private readonly SyncService _syncService;
    private readonly object _gate = new();
    private Task<SyncReport>? _inFlight;
    private IDispatcherTimer? _timer;
    private bool _started;

    public SyncCoordinator(SyncService syncService)
    {
        _syncService = syncService;
    }

    public DateTime? LastSuccessfulSyncUtc { get; private set; }
    public SyncReport? LastReport { get; private set; }

    // Raised on the main thread after every pass, so open pages can refresh what they show.
    public event Action<SyncReport>? SyncCompleted;

    public Task<SyncReport> SyncNowAsync()
    {
        lock (_gate)
        {
            if (_inFlight is { IsCompleted: false }) return _inFlight;
            _inFlight = RunPassAsync();
            return _inFlight;
        }
    }

    // Called once a user is signed in (AppShell). Idempotent.
    public void Start()
    {
        if (_started) return;
        _started = true;

        _timer = Application.Current?.Dispatcher.CreateTimer();
        if (_timer != null)
        {
            _timer.Interval = PeriodicInterval;
            _timer.Tick += (_, _) => _ = SyncNowAsync();
            _timer.Start();
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _ = SyncNowAsync();
    }

    // Called on sign-out, so a signed-out device stops calling the API.
    public void Stop()
    {
        if (!_started) return;
        _started = false;
        _timer?.Stop();
        _timer = null;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    // App came back to the foreground (App.xaml.cs's window Resumed event).
    public void OnAppResumed()
    {
        if (_started) _ = SyncNowAsync();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        // Back online: drain whatever queued up while offline, right away.
        if (e.NetworkAccess == NetworkAccess.Internet) _ = SyncNowAsync();
    }

    private async Task<SyncReport> RunPassAsync()
    {
        var report = new SyncReport();

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            report.Offline = true;
            Finish(report);
            return report;
        }

        // Records the server turned down (rather than failed to reach) stay queued; say why, so
        // "N sales pending sync" that never goes down has a visible reason.
        var productRejections = new List<string>();
        var saleRejections = new List<string>();
        await Step(report, "products upload", async () => report.ProductsPushed = await _syncService.PushProductsAsync(rejections: productRejections));
        await Step(report, "sales upload", async () => report.SalesPushed = await _syncService.SyncSalesAsync(rejections: saleRejections));
        report.Errors.AddRange(productRejections.Select(r => $"products upload: {r}"));
        report.Errors.AddRange(saleRejections.Select(r => $"sales upload: {r}"));
        report.SaleRejections.AddRange(saleRejections);
        await Step(report, "products download", async () => report.ProductsPulled = await _syncService.SyncProductsAsync());
        await Step(report, "receipts download", async () => report.ReceiptsPulled = await _syncService.PullReceiptsAsync());

        if (report.Errors.Count == 0) LastSuccessfulSyncUtc = DateTime.UtcNow;
        Finish(report);
        return report;
    }

    private static async Task Step(SyncReport report, string name, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            report.Errors.Add($"{name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Sync step '{name}' failed: {ex}");
        }
    }

    private void Finish(SyncReport report)
    {
        LastReport = report;
        MainThread.BeginInvokeOnMainThread(() => SyncCompleted?.Invoke(report));
    }
}

public class SyncReport
{
    public bool Offline { get; set; }
    public int ProductsPushed { get; set; }
    public int SalesPushed { get; set; }
    public int ProductsPulled { get; set; }
    public int ReceiptsPulled { get; set; }
    public List<string> Errors { get; } = new();
    // Why the server refused queued sales this pass (not connection failures), one per reason.
    public List<string> SaleRejections { get; } = new();
    public bool Succeeded => !Offline && Errors.Count == 0;
}
