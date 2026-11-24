using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mercurius.Shared.Sales;
using Microsoft.JSInterop;

namespace Mercurius.Client.Services
{
    /// <summary>What the top bar shows about this device's connection and unsent changes.</summary>
    public sealed record SyncStatus(
        bool BrowserOnline,
        bool ServerReachable,
        int Pending,
        int Failed,
        int Conflicts,
        bool AuthRequired,
        bool Syncing,
        DateTime? LastSyncUtc,
        DateTime? ShowingSavedDataFromUtc)
    {
        public bool IsOffline => !BrowserOnline || !ServerReachable;
        public int NeedsAttention => Failed + Conflicts;
    }

    /// <summary>
    /// Keeps this device and the server in step, so the app keeps working without a connection:
    ///   - sends the outbox (changes made on this device) in the order they were made, each with its
    ///     own Idempotency-Key so a retry after a lost response is applied once;
    ///   - keeps the till's product catalog fresh in IndexedDB;
    ///   - tracks whether the server can be reached, from the browser's online/offline signal and from
    ///     every request it makes, and publishes that as <see cref="Status"/>.
    /// It runs when the app starts, whenever the connection comes back, every 30 seconds while anything
    /// is waiting or the server is unreachable, and right after a change is queued.
    ///
    /// Its requests never use the service worker's saved responses (so "reachable" really means the
    /// server answered), and never bounce the page to the sign-in screen on a 401: an expired session
    /// becomes <see cref="SyncStatus.AuthRequired"/> and the changes wait until the person signs in.
    /// </summary>
    public sealed class SyncService : IAsyncDisposable
    {
        private static readonly TimeSpan CatalogRefreshEvery = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        private readonly HttpClient _http;
        private readonly OfflineStoreService _store;
        private readonly IJSRuntime _js;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private DotNetObjectReference<SyncService>? _selfRef;
        private CancellationTokenSource? _loop;
        private bool _started;
        private bool _keepCatalog;
        private DateTime? _catalogPulledUtc;

        public SyncService(HttpClient http, OfflineStoreService store, IJSRuntime js)
        {
            _http = http;
            _store = store;
            _js = js;
        }

        public SyncStatus Status { get; private set; } = new(true, true, 0, 0, 0, false, false, null, null);

        public event Action? Changed;

        /// <summary>Starts syncing; safe to call more than once. <paramref name="keepCatalog"/>: this user sells, so keep the till's catalog.</summary>
        public async Task StartAsync(bool keepCatalog, IEnumerable<string> pagesToSave)
        {
            if (_started) return;
            _started = true;
            _keepCatalog = keepCatalog;
            try
            {
                Status = Status with { BrowserOnline = await _js.InvokeAsync<bool>("mercuriusPwa.isOnline") };
                _selfRef = DotNetObjectReference.Create(this);
                await _js.InvokeVoidAsync("mercuriusPwa.watchConnectivity", _selfRef);
                await _js.InvokeAsync<bool>("mercuriusPwa.requestPersistentStorage");
                await _js.InvokeAsync<bool>("mercuriusPwa.warmOfflineCache", pagesToSave.ToArray());
            }
            catch (JSException)
            {
                // No storage/service worker support (private window): selling still works online.
            }

            await MoveLegacyQueueAsync();
            await RefreshCountsAsync();
            _ = SyncNowAsync();

            _loop = new CancellationTokenSource();
            _ = LoopAsync(_loop.Token);
        }

        [JSInvokable]
        public Task OnBrowserConnectivityChanged(bool online)
        {
            Status = Status with { BrowserOnline = online };
            Raise();
            return online ? SyncNowAsync() : Task.CompletedTask;
        }

        /// <summary>A page was shown the service worker's saved copy of a response (see OfflineDataHandler).</summary>
        public void NoteSavedDataShown(DateTime? savedAtUtc)
        {
            Status = Status with { ServerReachable = false, ShowingSavedDataFromUtc = savedAtUtc ?? Status.ShowingSavedDataFromUtc };
            Raise();
        }

        /// <summary>Raised when a change was saved to the outbox for later (so the layout can say so). Argument: the change's label.</summary>
        public event Action<string, bool>? ChangeQueued;

        /// <summary>Adds a change to the outbox and tries to send it straight away.</summary>
        public async Task QueueAsync(OutboxEntry entry, bool authRequired = false, bool announce = true)
        {
            await _store.PutOutboxEntryAsync(entry);
            if (authRequired) Status = Status with { AuthRequired = true };
            await RefreshCountsAsync();
            Raise();
            if (announce) ChangeQueued?.Invoke(entry.Label, authRequired);
            if (!authRequired) _ = SyncNowAsync();
        }

        /// <summary>A sale the server couldn't be reached for. Kept on this device until it's sent.</summary>
        public Task QueueSaleAsync(OfflineSaleRequest sale, decimal total)
        {
            var units = sale.Items.Sum(i => i.Quantity);
            return QueueAsync(new OutboxEntry
            {
                Id = (sale.SyncId ?? Guid.NewGuid()).ToString("N"),
                Kind = "sale",
                Label = $"Sale · {units} item{(units == 1 ? "" : "s")} · {Money.Format(total)}",
                Url = "api/sales/offline-sync",
                Body = JsonSerializer.Serialize(sale, JsonOptions),
                CreatedAtUtc = sale.CreatedAtUtc
            }, announce: false);   // the till says so itself
        }

        public async Task<List<OutboxEntry>> GetOutboxAsync() => await _store.GetOutboxAsync();

        /// <summary>Puts a refused or conflicting change back in the queue (after the person fixed the cause).</summary>
        public async Task RetryAsync(OutboxEntry entry)
        {
            // A new key: the server remembers its answer to the old one (a refusal is replayed too), so
            // reusing it would just bring the same refusal back.
            await _store.RemoveOutboxEntryAsync(entry.Id);
            entry.Id = Guid.NewGuid().ToString("N");
            entry.Status = OutboxEntry.Pending;
            entry.Error = null;
            entry.ConflictVersion = null;
            await _store.PutOutboxEntryAsync(entry);
            await RefreshCountsAsync();
            Raise();
            await SyncNowAsync();
        }

        /// <summary>
        /// "Overwrite with mine": the record was changed elsewhere first and the person decided their
        /// edit should win. The edit is retargeted at the record's current version and sent again.
        /// </summary>
        public async Task OverwriteAsync(OutboxEntry entry)
        {
            if (entry.ConflictVersion is int version && !string.IsNullOrEmpty(entry.Body))
            {
                var body = System.Text.Json.Nodes.JsonNode.Parse(entry.Body)?.AsObject();
                if (body != null)
                {
                    body["baseDetailsVersion"] = version;
                    entry.Body = body.ToJsonString();
                }
            }
            await RetryAsync(entry);
        }

        /// <summary>Drops a change for good. The page asks the person first.</summary>
        public async Task DiscardAsync(OutboxEntry entry)
        {
            await _store.RemoveOutboxEntryAsync(entry.Id);
            await RefreshCountsAsync();
            Raise();
        }

        /// <summary>Sends what's waiting and refreshes the catalog. Returns how many changes went through.</summary>
        public async Task<int> SyncNowAsync()
        {
            if (!await _gate.WaitAsync(0)) return 0;   // one pass at a time
            var sent = 0;
            try
            {
                Status = Status with { Syncing = true };
                Raise();

                var reachedServer = false;
                var stopped = false;
                foreach (var entry in (await _store.GetOutboxAsync()).Where(e => e.Status == OutboxEntry.Pending))
                {
                    var outcome = await SendAsync(entry);
                    if (outcome == SendOutcome.Unreachable) { stopped = true; break; }
                    reachedServer = true;
                    if (outcome == SendOutcome.AuthRequired) { stopped = true; break; }
                    if (outcome == SendOutcome.Sent) sent++;
                }

                if (!stopped)
                {
                    // Nothing (more) to send: still find out whether the server is there, and keep the
                    // catalog fresh while it is.
                    var due = _keepCatalog && (sent > 0 || _catalogPulledUtc is null || DateTime.UtcNow - _catalogPulledUtc > CatalogRefreshEvery);
                    var reachable = due ? await PullCatalogAsync() : await PingAsync();
                    if (reachable is bool ok)
                    {
                        reachedServer = ok;
                        if (!ok) stopped = true;
                    }
                }

                Status = Status with
                {
                    ServerReachable = reachedServer || (!stopped && Status.ServerReachable),
                    LastSyncUtc = reachedServer ? DateTime.UtcNow : Status.LastSyncUtc,
                    ShowingSavedDataFromUtc = reachedServer ? null : Status.ShowingSavedDataFromUtc
                };
                return sent;
            }
            finally
            {
                await RefreshCountsAsync();
                Status = Status with { Syncing = false };
                _gate.Release();
                Raise();
            }
        }

        private enum SendOutcome { Sent, Refused, Unreachable, AuthRequired }

        private async Task<SendOutcome> SendAsync(OutboxEntry entry)
        {
            entry.Attempts++;
            entry.LastAttemptUtc = DateTime.UtcNow;
            HttpResponseMessage response;
            try
            {
                using var request = NewRequest(new HttpMethod(entry.Method), entry.Url);
                request.Headers.Add("Idempotency-Key", entry.Id);
                request.Content = new StringContent(entry.Body, Encoding.UTF8, "application/json");
                using var timeout = new CancellationTokenSource(RequestTimeout);
                response = await _http.SendAsync(request, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await _store.PutOutboxEntryAsync(entry);
                Status = Status with { ServerReachable = false };
                return SendOutcome.Unreachable;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    await _store.RemoveOutboxEntryAsync(entry.Id);
                    Status = Status with { ServerReachable = true, AuthRequired = false };
                    return SendOutcome.Sent;
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _store.PutOutboxEntryAsync(entry);
                    Status = Status with { ServerReachable = true, AuthRequired = true };
                    return SendOutcome.AuthRequired;
                }
                if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // The server is there but failing right now: keep it waiting, try again later.
                    await _store.PutOutboxEntryAsync(entry);
                    Status = Status with { ServerReachable = false };
                    return SendOutcome.Unreachable;
                }

                // Refused (validation, permission, a conflicting change): it won't succeed by retrying
                // alone, so it waits for a person on the Outbox page instead of blocking the queue.
                entry.Status = response.StatusCode == HttpStatusCode.Conflict ? OutboxEntry.Conflict : OutboxEntry.Failed;
                var text = await response.Content.ReadAsStringAsync();
                (entry.Error, entry.ConflictVersion) = ReadRefusal(text, (int)response.StatusCode);
                await _store.PutOutboxEntryAsync(entry);
                Status = Status with { ServerReachable = true, AuthRequired = false };
                return SendOutcome.Refused;
            }
        }

        // The server's message (its "error" text) and, for an edit that lost to someone else's, the version it's at now.
        private static (string Error, int? ConflictVersion) ReadRefusal(string json, int status)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                int? version = root.TryGetProperty("currentDetailsVersion", out var v) && v.TryGetInt32(out var n) ? n : null;
                return (error ?? $"The server refused this ({status}).", version);
            }
            catch (JsonException)
            {
                return ($"The server refused this ({status}).", null);
            }
        }

        /// <returns>True/false: the server answered or not. Null: not checked.</returns>
        private async Task<bool?> PullCatalogAsync()
        {
            try
            {
                using var request = NewRequest(HttpMethod.Get, "api/sales/offline-catalog");
                using var timeout = new CancellationTokenSource(RequestTimeout);
                using var response = await _http.SendAsync(request, timeout.Token);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Status = Status with { AuthRequired = true };
                    return true;
                }
                if (!response.IsSuccessStatusCode) return (int)response.StatusCode < 500;
                var products = await response.Content.ReadFromJsonAsync<List<OfflineProductDto>>(timeout.Token);
                if (products != null)
                {
                    await _store.ReplaceProductsAsync(products);
                    _catalogPulledUtc = DateTime.UtcNow;
                }
                Status = Status with { AuthRequired = false };
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return false;
            }
        }

        private async Task<bool?> PingAsync()
        {
            try
            {
                using var request = NewRequest(HttpMethod.Get, "api/account/me");
                using var timeout = new CancellationTokenSource(RequestTimeout);
                using var response = await _http.SendAsync(request, timeout.Token);
                return (int)response.StatusCode < 500;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        private static HttpRequestMessage NewRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Options.Set(ApiRequestOptions.Suppress401Redirect, true);
            // These resend the outbox: they must reach the server, not be queued again.
            request.Options.Set(OfflineWriteHandler.Bypass, true);
            // Never the service worker's saved copy: this has to be the server itself.
            request.Headers.Add("X-Mercurius-Offline-Fallback", "none");
            return request;
        }

        private async Task LoopAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            var quietTicks = 0;
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    // Every 30 s while something is waiting or the server is unreachable; otherwise
                    // every 2 minutes, just to keep the catalog and the status fresh.
                    var busy = Status.Pending > 0 || Status.IsOffline || Status.AuthRequired;
                    if (busy || ++quietTicks >= 4)
                    {
                        quietTicks = 0;
                        await SyncNowAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Sales queued by app versions before the outbox existed.
        private async Task MoveLegacyQueueAsync()
        {
            try
            {
                foreach (var sale in await _store.GetLegacyPendingSalesAsync())
                {
                    if (sale.SyncId is not Guid syncId) continue;
                    var total = sale.Items.Sum(i => (i.UnitPrice ?? 0m) * i.Quantity);
                    var units = sale.Items.Sum(i => i.Quantity);
                    await _store.PutOutboxEntryAsync(new OutboxEntry
                    {
                        Id = syncId.ToString("N"),
                        Kind = "sale",
                        Label = $"Sale · {units} item{(units == 1 ? "" : "s")} · {Money.Format(total)}",
                        Url = "api/sales/offline-sync",
                        Body = JsonSerializer.Serialize(sale, JsonOptions),
                        CreatedAtUtc = sale.CreatedAtUtc
                    });
                    await _store.RemoveLegacyPendingSaleAsync(syncId);
                }
            }
            catch (JSException)
            {
            }
        }

        private async Task RefreshCountsAsync()
        {
            try
            {
                var entries = await _store.GetOutboxAsync();
                Status = Status with
                {
                    Pending = entries.Count(e => e.Status == OutboxEntry.Pending),
                    Failed = entries.Count(e => e.Status == OutboxEntry.Failed),
                    Conflicts = entries.Count(e => e.Status == OutboxEntry.Conflict)
                };
            }
            catch (JSException)
            {
            }
        }

        private void Raise() => Changed?.Invoke();

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async ValueTask DisposeAsync()
        {
            _loop?.Cancel();
            _selfRef?.Dispose();
            await Task.CompletedTask;
        }
    }
}
