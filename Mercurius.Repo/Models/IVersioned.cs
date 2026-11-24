namespace Mercurius.Repo.Models;

// Rows that several requests change at once and where a lost update costs money or stock: a
// product's stock, a batch's remaining quantity, an invoice's paid amount and refund state.
// Version is an optimistic-concurrency token: MercuriusDbContext raises it on every update, and EF
// adds "WHERE Version = <what this request read>" to the UPDATE. If another request got there
// first, nothing is updated and SaveChanges throws DbUpdateConcurrencyException instead of
// silently overwriting the other change (two tills selling the last unit, a double-clicked
// refund). Callers that can simply try again do so through ConcurrencyRetry.
public interface IVersioned
{
    int Version { get; set; }
}
