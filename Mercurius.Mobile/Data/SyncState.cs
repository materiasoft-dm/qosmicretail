using SQLite;

namespace Mercurius.Mobile.Data;

// One row per synced entity type, tracking the pull cursor so re-syncing only asks the server for
// what changed since last time rather than the whole catalog every time.
[Table("SyncState")]
public class SyncState
{
    [PrimaryKey]
    public string EntityName { get; set; } = "";

    public DateTime LastSyncedUtc { get; set; }
}
