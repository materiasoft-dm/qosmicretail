using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.Constants
{
    public class StatusCollection
    {
        public enum InvoiceStatus
        {
            Draft = 1,
            Completed = 2,
            Finalized = 3,
            Refunded = 4,
            Deleted = 5,
            DeferredPayment = 6,
            PartiallyPaid = 7
        }
    }
}
