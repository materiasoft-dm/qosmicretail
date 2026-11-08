using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Common;
using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Repo.Repositories.Auditing;
using Mercurius.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mercurius.Tests.Identity
{
    // Per-tenant roles (MercuriusRole.TenantId), wired the way Program.cs wires Identity: the role
    // filter, the one-time split of the old shared roles, and tenant provisioning of fixed roles.
    public class TenantRolesTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private MercuriusDbContext Db => _scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
        private RoleManager<MercuriusRole> RoleManager => _scope.ServiceProvider.GetRequiredService<RoleManager<MercuriusRole>>();
        private UserManager<MercuriusUser> UserManager => _scope.ServiceProvider.GetRequiredService<UserManager<MercuriusUser>>();
        private ITenantScopeOverride TenantScope => _scope.ServiceProvider.GetRequiredService<ITenantScopeOverride>();
        private TenantRoles TenantRoles => _scope.ServiceProvider.GetRequiredService<TenantRoles>();

        public TenantRolesTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<HttpContextCurrentTenantContext>();
            services.AddScoped<ICurrentTenantContext>(sp => sp.GetRequiredService<HttpContextCurrentTenantContext>());
            services.AddScoped<ITenantScopeOverride>(sp => sp.GetRequiredService<HttpContextCurrentTenantContext>());
            services.AddScoped<IAuditContextProvider, NullAuditContextProvider>();
            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddDbContext<MercuriusDbContext>((sp, options) =>
                options.UseSqlite(_connection).AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
            services.AddIdentityCore<MercuriusUser>(o => o.Stores.MaxLengthForKeys = 128)
                .AddRoles<MercuriusRole>()
                .AddEntityFrameworkStores<MercuriusDbContext>();
            services.AddScoped<TenantRoles>();

            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            Db.Database.EnsureCreated();
            Db.Tenants.AddRange(
                new Tenant { Id = 1, Name = "Tenant One", CreatedDate = DateTime.UtcNow },
                new Tenant { Id = 2, Name = "Tenant Two", CreatedDate = DateTime.UtcNow });
            Db.SaveChanges();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task EachTenantGetsItsOwnFixedRoles_AndCannotSeeTheOthers()
        {
            await TenantRoles.EnsurePlatformRolesAsync();
            await TenantRoles.EnsureForTenantAsync(1);
            await TenantRoles.EnsureForTenantAsync(2);

            string tenantTwoAdminId;
            using (TenantScope.UseTenant(2))
            {
                tenantTwoAdminId = (await RoleManager.FindByNameAsync(TenantRoles.Administrator))!.Id;
            }

            using (TenantScope.UseTenant(1))
            {
                var names = RoleManager.Roles.Select(r => r.Name).OrderBy(n => n).ToList();
                Assert.Equal(new[] { "Administrator", ModuleRegistry.DatabaseAdministratorRole, "Manager", "User" }, names);

                var admin = await RoleManager.FindByNameAsync(TenantRoles.Administrator);
                Assert.Equal(1, admin!.TenantId);
                Assert.NotEqual(tenantTwoAdminId, admin.Id);
                Assert.Null(await RoleManager.FindByIdAsync(tenantTwoAdminId));

                var pages = (await RoleManager.GetClaimsAsync(admin)).Select(c => c.Value).ToList();
                Assert.Contains(ModuleRegistry.Pages.PRODUCTS_INDEX, pages);
                Assert.DoesNotContain(ModuleRegistry.Pages.ADMIN_DATA_QUERY, pages);
            }
        }

        [Fact]
        public async Task AddToRole_ByName_UsesTheUsersOwnTenantsRole()
        {
            await TenantRoles.EnsureForTenantAsync(1);
            await TenantRoles.EnsureForTenantAsync(2);
            var user = await CreateUserAsync("two@example.com", tenantId: 2);

            using (TenantScope.UseTenant(2))
            {
                Assert.True((await UserManager.AddToRoleAsync(user, TenantRoles.Administrator)).Succeeded);
                Assert.Equal(new[] { "Administrator" }, await UserManager.GetRolesAsync(user));
            }

            var roleIds = Db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToList();
            var role = Db.Roles.IgnoreQueryFilters().Single(r => r.Id == roleIds.Single());
            Assert.Equal(2, role.TenantId);
        }

        [Fact]
        public async Task SplitLegacyRoles_GivesEachTenantACopy_AndKeepsEveryonesPermissions()
        {
            // The old shape: one shared set of roles with no tenant.
            var admin = new MercuriusRole("Administrator") { NormalizedName = "ADMINISTRATOR" };
            var cashier = new MercuriusRole("Cashier") { NormalizedName = "CASHIER" };
            var dbAdmin = new MercuriusRole(ModuleRegistry.DatabaseAdministratorRole) { NormalizedName = ModuleRegistry.DatabaseAdministratorRole.ToUpperInvariant() };
            Db.Roles.AddRange(admin, cashier, dbAdmin);
            Db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = admin.Id, ClaimType = MercuriusClaimTypes.AccessPages, ClaimValue = ModuleRegistry.Pages.PRODUCTS_INDEX });
            Db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = cashier.Id, ClaimType = MercuriusClaimTypes.AccessPages, ClaimValue = ModuleRegistry.Pages.NEWSALE_CREATE });
            await Db.SaveChangesAsync();

            var one = await CreateUserAsync("one@example.com", tenantId: 1);
            var two = await CreateUserAsync("two@example.com", tenantId: 2);
            Db.UserRoles.AddRange(
                new IdentityUserRole<string> { UserId = one.Id, RoleId = admin.Id },
                new IdentityUserRole<string> { UserId = one.Id, RoleId = dbAdmin.Id },
                new IdentityUserRole<string> { UserId = two.Id, RoleId = admin.Id },
                new IdentityUserRole<string> { UserId = two.Id, RoleId = cashier.Id });
            await Db.SaveChangesAsync();

            Assert.Equal(2, await TenantRoles.SplitLegacyRolesAsync(defaultTenantId: 1));

            var roles = Db.Roles.IgnoreQueryFilters().AsNoTracking().ToList();
            // Administrator: the original row stays with the default tenant, tenant 2 gets a copy.
            Assert.Equal(1, roles.Single(r => r.Id == admin.Id).TenantId);
            var copy = roles.Single(r => r.Name == "Administrator" && r.TenantId == 2);
            Assert.Equal(new[] { ModuleRegistry.Pages.PRODUCTS_INDEX },
                Db.RoleClaims.Where(c => c.RoleId == copy.Id).Select(c => c.ClaimValue).ToList());
            // A custom role only goes to the tenant whose users hold it.
            Assert.Equal(2, roles.Single(r => r.Name == "Cashier").TenantId);
            // The platform role stays outside every tenant.
            Assert.Null(roles.Single(r => r.Id == dbAdmin.Id).TenantId);

            Db.ChangeTracker.Clear();
            using (TenantScope.UseTenant(1))
            {
                Assert.Equal(new[] { "Administrator", ModuleRegistry.DatabaseAdministratorRole },
                    (await UserManager.GetRolesAsync(one)).OrderBy(r => r).ToArray());
            }
            using (TenantScope.UseTenant(2))
            {
                Assert.Equal(new[] { "Administrator", "Cashier" }, (await UserManager.GetRolesAsync(two)).OrderBy(r => r).ToArray());
                var tenantTwoAdmin = await RoleManager.FindByNameAsync("Administrator");
                Assert.Equal(copy.Id, tenantTwoAdmin!.Id);
            }

            Assert.Equal(0, await TenantRoles.SplitLegacyRolesAsync(defaultTenantId: 1));
        }

        private async Task<MercuriusUser> CreateUserAsync(string email, int tenantId)
        {
            var user = new MercuriusUser { UserName = email, Email = email, FirstName = "Test", LastName = "User", TenantId = tenantId, IsActive = true };
            Assert.True((await UserManager.CreateAsync(user)).Succeeded);
            return user;
        }

        private sealed class NullAuditContextProvider : IAuditContextProvider
        {
            public AuditRequestInfo GetRequestInfo(DbContext db) => new();
        }
    }
}
