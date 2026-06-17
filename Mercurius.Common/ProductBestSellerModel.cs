using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common
{
    public class ProductBestSellerModel
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? PartCode { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public int Rank { get; set; }
    }
}
