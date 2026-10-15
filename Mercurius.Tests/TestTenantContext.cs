using Mercurius.Repo.Repositories;

namespace Mercurius.Tests
{
    // Every test acts as a single signed-in tenant, so the tenant query filter and the
    // repository's auto-stamping of TenantId behave the way they do for a real request.
    public sealed class TestTenantContext : ICurrentTenantContext
    {
        public const int DefaultTenantId = 1;

        public static readonly TestTenantContext Instance = new();

        public int TenantId => DefaultTenantId;
    }
}
