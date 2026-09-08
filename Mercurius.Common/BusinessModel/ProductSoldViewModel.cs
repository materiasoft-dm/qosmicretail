using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.BusinessModel
{
    public class ProductSoldViewModel
    {
        public string ProductDisplayName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string ProductCode { get; set; } = "";
        public string ItemName { get; set; } = "";
    }
}
