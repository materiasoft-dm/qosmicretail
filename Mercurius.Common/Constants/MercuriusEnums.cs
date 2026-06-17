using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.Constants
{
    public class MercuriusEnums
    {
        public enum ShipmentStatusCollection
        {
            Received = 1,
            Draft = 2,
            Deleted = 3
        }

        // InvoiceStatusCollection deleted 2026-05-28 — was [Obsolete] with zero call sites.
        // Use Mercurius.Common.Constants.StatusCollection.InvoiceStatus for the canonical
        // invoice status enum.
    }
}
