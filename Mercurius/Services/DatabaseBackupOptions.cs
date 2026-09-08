namespace Mercurius.Services
{
    /// <summary>
    /// Bound from the "DatabaseBackup" configuration section.
    /// </summary>
    public class DatabaseBackupOptions
    {
        /// <summary>
        /// How many days a backup is retained. Hard-capped at 10 regardless of this value —
        /// see DatabaseBackupService.EffectiveRetentionDays.
        /// </summary>
        public int RetentionDays { get; set; } = 7;
    }
}
