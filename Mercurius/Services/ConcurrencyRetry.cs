using Microsoft.EntityFrameworkCore;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// Runs a request's work again when another request changed the same stock, batch or invoice
    /// at the same moment (DbUpdateConcurrencyException — see IVersioned). The work is run from the
    /// top, so it re-reads current values and prices; each attempt must therefore either finish or
    /// leave nothing behind, which every caller already guarantees with its transaction rollback.
    /// After the last attempt the exception propagates and the request fails with a plain error —
    /// the person (or the offline queue, or the phone's sync) tries again.
    /// </summary>
    public static class ConcurrencyRetry
    {
        private const int MaxAttempts = 4;

        public static async Task<T> RunAsync<T>(IUnitOfWork unitOfWork, Func<Task<T>> work, CancellationToken ct = default)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await work();
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
                {
                    unitOfWork.DiscardChanges();
                    // A short, growing pause so two requests that collided don't collide again in step.
                    await Task.Delay(TimeSpan.FromMilliseconds(40 * attempt * attempt), ct);
                }
            }
        }
    }
}
