namespace Mercurius.Shared.Common
{
    public class ListResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class ProductOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
    }

    public class SupplierOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
