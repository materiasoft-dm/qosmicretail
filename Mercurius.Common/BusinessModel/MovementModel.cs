using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.BusinessModel
{
    public class MovementModel
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = "";
        public string MovementType { get; set; } = "";
        public DateTime TransactionDate { get; set; }
        public string Direction { get; set; } = "";
        public int CountBeforeTransaction { get; set; }
        public int Quantity { get; set; }
        public int CountAfterTransaction { get; set; }
        public string TransactionId { get; set; } = "";
        public int ProductId { get; set; }
        public int LocationId { get; set; }
    }
}
