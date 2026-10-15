using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mercurius.Repo.Repositories.Auditing
{
    // Keeps Invoice.UpdatedDate honest, because the mobile Receipts sync pulls invoices by
    // "changed since my last pull" and a change it can't see is a change devices never get.
    //
    // Before this existed, a refund (RefundsController, InvoicesApiController.CreateRefund) set
    // HasRefund/StatusId but never touched UpdatedDate — so every terminal kept showing the sale as
    // un-refunded forever. Rather than remember to stamp it at every call site (and every future
    // one), any save that modifies an Invoice, or adds/changes/removes one of its items or refund
    // rows, stamps that invoice here.
    public class InvoiceChangeStampInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Stamp(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Stamp(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void Stamp(DbContext? context)
        {
            if (context == null) return;
            context.ChangeTracker.DetectChanges();

            var now = DateTime.UtcNow;
            var touchedInvoiceIds = new HashSet<int>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

                switch (entry.Entity)
                {
                    case Invoice invoice when entry.State == EntityState.Modified:
                        invoice.UpdatedDate = now;
                        break;
                    case InvoiceItem item when item.InvoiceId > 0:
                        touchedInvoiceIds.Add(item.InvoiceId);
                        break;
                    case InvoiceItemRefund refund when refund.InvoiceId > 0:
                        touchedInvoiceIds.Add(refund.InvoiceId);
                        break;
                    case InvoiceRefund refundHeader when refundHeader.InvoiceId > 0:
                        touchedInvoiceIds.Add(refundHeader.InvoiceId);
                        break;
                }
            }

            foreach (var invoiceId in touchedInvoiceIds)
            {
                // Find returns the tracked instance when there is one (the common case — the
                // caller usually loaded the invoice itself), else loads it.
                var invoice = context.Set<Invoice>().Find(invoiceId);
                if (invoice == null) continue;
                var entry = context.Entry(invoice);
                if (entry.State == EntityState.Added) continue; // brand-new: CreatedDate already covers it
                invoice.UpdatedDate = now;
            }
        }
    }
}
