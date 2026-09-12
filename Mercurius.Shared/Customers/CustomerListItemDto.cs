namespace Mercurius.Shared.Customers
{
    public class CustomerListItemDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
    }

    public class CustomerListResultDto
    {
        public List<CustomerListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class CreateCustomerRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
    }
}
