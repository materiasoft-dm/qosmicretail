using System;
using System.Linq;
using System.Threading.Tasks;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mercurius.Tests.Identity
{
    /// <summary>
    /// A deactivated user can't sign in (web or mobile — both go through CheckPasswordSignInAsync),
    /// and the refusal only comes once the password is right, so it can't be used to find out which
    /// accounts exist or are inactive.
    /// </summary>
    public class DeactivatedUserSignInTests : IDisposable
    {
        private const string Password = "Correct-Horse-1!";
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public DeactivatedUserSignInTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
            services.AddScoped<ICurrentTenantContext>(_ => TestTenantContext.Instance);
            services.AddDbContext<MercuriusDbContext>(options => options.UseSqlite(_connection));
            services.AddAuthentication().AddIdentityCookies();
            services.AddIdentityCore<MercuriusUser>(o =>
                {
                    o.Stores.MaxLengthForKeys = 128;
                    o.Lockout.MaxFailedAccessAttempts = 5;
                    o.Lockout.AllowedForNewUsers = true;
                })
                .AddRoles<MercuriusRole>()
                .AddEntityFrameworkStores<MercuriusDbContext>()
                .AddSignInManager<MercuriusSignInManager>();

            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            _scope.ServiceProvider.GetRequiredService<MercuriusDbContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
            _connection.Dispose();
        }

        private SignInManager<MercuriusUser> SignIn => _scope.ServiceProvider.GetRequiredService<SignInManager<MercuriusUser>>();
        private UserManager<MercuriusUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<MercuriusUser>>();

        private async Task<MercuriusUser> AddUserAsync(bool active)
        {
            var user = new MercuriusUser
            {
                UserName = "cashier@test.com", Email = "cashier@test.com", FirstName = "Cash", LastName = "Ier",
                TenantId = TestTenantContext.DefaultTenantId, IsActive = active
            };
            var created = await Users.CreateAsync(user, Password);
            Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(e => e.Description)));
            return user;
        }

        [Fact]
        public async Task ActiveUser_WithRightPassword_CanSignIn()
        {
            var user = await AddUserAsync(active: true);
            var result = await SignIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task DeactivatedUser_WithRightPassword_IsRefused()
        {
            var user = await AddUserAsync(active: false);
            var result = await SignIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
            Assert.False(result.Succeeded);
            Assert.True(result.IsNotAllowed);
        }

        [Fact]
        public async Task DeactivatedUser_WithWrongPassword_LooksLikeAnyWrongPassword()
        {
            var user = await AddUserAsync(active: false);
            var result = await SignIn.CheckPasswordSignInAsync(user, "not-the-password", lockoutOnFailure: true);
            Assert.False(result.Succeeded);
            Assert.False(result.IsNotAllowed);   // so "deactivated" isn't revealed to someone guessing
        }

        [Fact]
        public async Task WrongPasswords_CountTowardsLockout_ThenEvenTheRightPasswordIsRefused()
        {
            var user = await AddUserAsync(active: true);
            for (var i = 0; i < 5; i++)
            {
                await SignIn.CheckPasswordSignInAsync(user, "wrong-" + i, lockoutOnFailure: true);
            }

            var result = await SignIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
            Assert.True(result.IsLockedOut);
        }

        [Fact]
        public async Task ChangingTheSecurityStamp_ChangesWhatAMobileTokenWouldCarry()
        {
            var user = await AddUserAsync(active: true);
            var before = await Users.GetSecurityStampAsync(user);
            await Users.UpdateSecurityStampAsync(user);
            Assert.NotEqual(before, await Users.GetSecurityStampAsync(user));
        }
    }
}
