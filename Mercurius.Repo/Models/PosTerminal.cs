using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

// One point-of-sale machine (a mobile/Windows till), as BIR sees it. A device registers itself
// the first time it syncs (Api/SyncController.RegisterTerminal), keyed by the random per-install
// id it sends in X-Device-Id. The BIR fields come from the machine's Permit to Use, which the
// pharmacy gets from its RDO per machine; until they're filled in, the till's receipts say
// "NOT A VALID INVOICE".
public class PosTerminal : ITenantScoped
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required]
    [StringLength(100)]
    public string DeviceId { get; set; } = "";
    [StringLength(200)]
    public string? Name { get; set; }
    // The branch the device was last assigned to (its own Settings page).
    public int? LocationId { get; set; }

    // Machine Identification Number, printed on every invoice.
    [StringLength(50)]
    public string? MachineIdentificationNumber { get; set; }
    [StringLength(100)]
    public string? SerialNumber { get; set; }
    [StringLength(100)]
    public string? PermitToUseNumber { get; set; }
    public DateTime? PermitToUseDateIssued { get; set; }

    // The terminal's counters, as last reported by the device. The device is the authority (it
    // issues the numbers offline); the server keeps them so a re-registered device carries on
    // from the same place instead of restarting at 1.
    public long LastInvoiceSequence { get; set; }
    public int LastZCounter { get; set; }
    public decimal AccumulatedGrandTotal { get; set; }
    public int ResetCounter { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public virtual Location? Location { get; set; }

    // "T12": prefixes this terminal's invoice serials in Invoice.InvoiceNumber.
    public static string CodeFor(int id) => $"T{id}";
}
