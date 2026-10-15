namespace Mercurius.Shared.Suppliers
{
    public class SupplierListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SupplierListResultDto
    {
        public List<SupplierListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    // IsActive=false is the soft delete (the MVC Suppliers "Delete" did exactly that).
    public class UpdateSupplierRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateSupplierRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
