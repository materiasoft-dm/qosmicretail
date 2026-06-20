namespace Mercurius.Models
{
    public class ItemSelectorModel
    {
        public int ProductId { get; set; }
        public string Category { get; set; } = null!;
        public bool IsSelected { get; set; }
        public string Name { get; set; } = null!;
        public string PartCode { get; set; } = null!;
        public bool Visible { get; set; }
        public bool isFilteredOut { get; set; }
        public decimal SalesPrice { get; set; }
    }
}
