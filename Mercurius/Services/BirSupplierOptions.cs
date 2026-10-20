namespace Mercurius.Services
{
    /// <summary>
    /// The POS software's own BIR accreditation ("Bir:Supplier" in configuration), printed in the
    /// footer of every invoice a till issues. One set for the whole app: it describes the
    /// software and its developer, not any one pharmacy. Blank until the accreditation is granted.
    /// </summary>
    public class BirSupplierOptions
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Tin { get; set; }
        public string? AccreditationNumber { get; set; }
        public DateTime? AccreditationDateIssued { get; set; }
        public DateTime? AccreditationValidUntil { get; set; }
    }
}
