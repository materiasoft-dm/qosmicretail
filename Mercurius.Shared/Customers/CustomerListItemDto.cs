namespace Mercurius.Shared.Customers
{
    public class CustomerListItemDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CustomerListResultDto
    {
        public List<CustomerListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    // A customer (patient) record, as edited on the Customers page.
    public class CustomerDetailDto : SaveCustomerRequest
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class SaveCustomerRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        // TIN / government ID number.
        public string? Tinnumber { get; set; }
        public string? Allergies { get; set; }
        public string? MedicalHistory { get; set; }
        // Soft delete: an inactive customer stays on record (and on past invoices) but isn't offered at checkout.
        public bool IsActive { get; set; } = true;
    }
}
