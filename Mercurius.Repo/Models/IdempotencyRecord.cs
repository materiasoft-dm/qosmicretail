using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

// One change a client sent with an Idempotency-Key header (Mercurius/Filters/IdempotencyFilter.cs).
// The first request claims the key (StatusCode 0 while it runs) and, if it succeeds, stores its
// response here; a repeat of the same key gets that stored response back instead of doing the change
// again. That's what lets a device's outbox resend a change whose answer it never received (a dropped
// connection, a closed laptop) without making it twice. Kept 30 days. Not tenant-scoped: keys are
// random GUIDs, and the row is matched to the user who sent it.
public class IdempotencyRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = "";

    [StringLength(450)]
    public string? UserId { get; set; }

    public int? TenantId { get; set; }

    [StringLength(10)]
    public string Method { get; set; } = "";

    [StringLength(400)]
    public string Path { get; set; } = "";

    // 0 while the first request is still running.
    public int StatusCode { get; set; }

    public string? ResponseBody { get; set; }

    [StringLength(200)]
    public string? ContentType { get; set; }

    public DateTime CreatedUtc { get; set; }
}
