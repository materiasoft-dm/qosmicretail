using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mercurius.Services
{
    /// <summary>
    /// Takes a daily snapshot of the SQLite database into the "backups" folder (content-root
    /// sibling of the DB file, outside wwwroot) and prunes anything older than the configured
    /// retention, hard-capped at 10 days.
    /// </summary>
    public class DatabaseBackupService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        private const int MaxRetentionDays = 10;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        private readonly IOptionsMonitor<DatabaseBackupOptions> _options;
        private readonly ILogger<DatabaseBackupService> _logger;

        public DatabaseBackupService(
            IServiceScopeFactory scopeFactory,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
            IOptionsMonitor<DatabaseBackupOptions> options,
            ILogger<DatabaseBackupService> logger)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            _options = options;
            _logger = logger;
        }

        public string BackupDirectory => Path.Combine(_env.ContentRootPath, "backups");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    RunOnce();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database backup cycle failed.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Shutting down.
                }
            }
        }

        /// <summary>
        /// Runs one backup+prune cycle. Public so it can be triggered on demand rather than
        /// only waiting for the 24h loop (e.g. at startup, or from a test).
        /// </summary>
        public void RunOnce()
        {
            Directory.CreateDirectory(BackupDirectory);
            CreateBackup();
            PruneOldBackups();
        }

        private void CreateBackup()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
            var connectionString = context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("No SQLite connection string available; skipping backup.");
                return;
            }

            var fileName = $"mercurius_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sqlite";
            var backupPath = Path.Combine(BackupDirectory, fileName);

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            // VACUUM INTO takes a consistent snapshot of a live database (correct even under
            // WAL mode), unlike a plain file copy which could grab an in-progress write.
            command.CommandText = "VACUUM INTO $path";
            command.Parameters.AddWithValue("$path", backupPath);
            command.ExecuteNonQuery();

            _logger.LogInformation("Created database backup: {FileName}", fileName);
        }

        private void PruneOldBackups()
        {
            var retentionDays = Math.Min(_options.CurrentValue.RetentionDays, MaxRetentionDays);
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            foreach (var file in Directory.GetFiles(BackupDirectory, "*.sqlite"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                        _logger.LogInformation("Deleted expired database backup: {FileName}", Path.GetFileName(file));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to evaluate/delete backup file {File}", file);
                }
            }
        }
    }
}
