using LiteDB;
using Microsoft.AspNetCore.Identity;
using Mercurius.Repo.IdentityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Mercurius.Repo.LiteDB
{
    public class LiteDbUserStore :
        IUserStore<MercuriusUser>,
        IUserPasswordStore<MercuriusUser>,
        IUserEmailStore<MercuriusUser>,
        IUserRoleStore<MercuriusUser>,
        IUserClaimStore<MercuriusUser>,
        IUserSecurityStampStore<MercuriusUser>,
        IUserLockoutStore<MercuriusUser>,
        IUserTwoFactorStore<MercuriusUser>,
        IUserPhoneNumberStore<MercuriusUser>,
        IUserLoginStore<MercuriusUser>,
        IUserAuthenticatorKeyStore<MercuriusUser>,
        IUserAuthenticationTokenStore<MercuriusUser>,
        IQueryableUserStore<MercuriusUser>,
        IDisposable
    {
        private readonly ILiteCollection<MercuriusUser> _users;
        private readonly ILiteCollection<IdentityRole> _roles;
        private readonly ILiteCollection<IdentityUserClaim<string>> _userClaims;
        private readonly ILiteCollection<IdentityUserRole<string>> _userRoles;
        private readonly ILiteCollection<IdentityUserLogin<string>> _userLogins;
        private readonly ILiteCollection<IdentityUserToken<string>> _userTokens;

        public IQueryable<MercuriusUser> Users => _users.FindAll().AsQueryable();

        public LiteDbUserStore(ILiteDatabase database)
        {
            _users = database.GetCollection<MercuriusUser>("identity_users");
            _roles = database.GetCollection<IdentityRole>("identity_roles");
            _userClaims = database.GetCollection<IdentityUserClaim<string>>("identity_user_claims");
            _userRoles = database.GetCollection<IdentityUserRole<string>>("identity_user_roles");
            _userLogins = database.GetCollection<IdentityUserLogin<string>>("identity_user_logins");
            _userTokens = database.GetCollection<IdentityUserToken<string>>("identity_user_tokens");
            // NOTE: indexes are created once at startup by LiteDbContext.EnsureCoreIndexes.
            // Calling EnsureIndex here races with concurrent cookie-validation requests
            // under Connection=shared and throws "Object synchronization method was called
            // from an unsynchronized block of code" — see LiteDB.SharedEngine.CloseDatabase.
        }

        public Task<IdentityResult> CreateAsync(MercuriusUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(user.Id)) user.Id = Guid.NewGuid().ToString();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            _users.Insert(user);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(MercuriusUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            _users.Update(user);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(MercuriusUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _users.Delete(user.Id);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<MercuriusUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult(_users.FindById(userId));
        public Task<MercuriusUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => Task.FromResult(_users.FindOne(u => u.NormalizedUserName == normalizedUserName));

        public Task<string?> GetUserIdAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.Id);
        public Task<string?> GetUserNameAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.UserName);
        public Task SetUserNameAsync(MercuriusUser u, string? n, CancellationToken ct) { u.UserName = n; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(MercuriusUser u, string? n, CancellationToken ct) { u.NormalizedUserName = n; return Task.CompletedTask; }

        public Task SetPasswordHashAsync(MercuriusUser u, string? h, CancellationToken ct) { u.PasswordHash = h; return Task.CompletedTask; }
        public Task<string?> GetPasswordHashAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
        public Task<bool> HasPasswordAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash != null);

        public Task SetEmailAsync(MercuriusUser u, string? e, CancellationToken ct) { u.Email = e; return Task.CompletedTask; }
        public Task<string?> GetEmailAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.Email);
        public Task<bool> GetEmailConfirmedAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.EmailConfirmed);
        public Task SetEmailConfirmedAsync(MercuriusUser u, bool c, CancellationToken ct) { u.EmailConfirmed = c; return Task.CompletedTask; }
        public Task<MercuriusUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(_users.FindOne(u => u.NormalizedEmail == normalizedEmail));
        public Task<string?> GetNormalizedEmailAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.NormalizedEmail);
        public Task SetNormalizedEmailAsync(MercuriusUser u, string? n, CancellationToken ct) { u.NormalizedEmail = n; return Task.CompletedTask; }

        public Task AddToRoleAsync(MercuriusUser user, string roleName, CancellationToken ct)
        {
            var role = _roles.FindOne(r => r.Name == roleName) ?? throw new InvalidOperationException($"Role '{roleName}' not found.");
            if (!_userRoles.Exists(ur => ur.UserId == user.Id && ur.RoleId == role.Id))
                _userRoles.Insert(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(MercuriusUser user, string roleName, CancellationToken ct)
        {
            var role = _roles.FindOne(r => r.Name == roleName);
            if (role != null) _userRoles.DeleteMany(ur => ur.UserId == user.Id && ur.RoleId == role.Id);
            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(MercuriusUser user, CancellationToken ct)
        {
            var roleIds = _userRoles.Find(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToList();
            var names = _roles.Find(r => roleIds.Contains(r.Id)).Select(r => r.Name!).ToList();
            return Task.FromResult<IList<string>>(names);
        }

        public Task<bool> IsInRoleAsync(MercuriusUser user, string roleName, CancellationToken ct)
        {
            var role = _roles.FindOne(r => r.Name == roleName);
            return Task.FromResult(role != null && _userRoles.Exists(ur => ur.UserId == user.Id && ur.RoleId == role.Id));
        }

        public Task<IList<MercuriusUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
        {
            var role = _roles.FindOne(r => r.Name == roleName);
            if (role == null) return Task.FromResult<IList<MercuriusUser>>(Array.Empty<MercuriusUser>());
            var userIds = _userRoles.Find(ur => ur.RoleId == role.Id).Select(ur => ur.UserId).ToList();
            return Task.FromResult<IList<MercuriusUser>>(_users.Find(u => userIds.Contains(u.Id)).ToList());
        }

        public Task<IList<Claim>> GetClaimsAsync(MercuriusUser user, CancellationToken ct)
        {
            var claims = _userClaims.Find(c => c.UserId == user.Id).Select(c => new Claim(c.ClaimType!, c.ClaimValue!)).ToList();
            return Task.FromResult<IList<Claim>>(claims);
        }

        public Task AddClaimsAsync(MercuriusUser user, IEnumerable<Claim> claims, CancellationToken ct)
        {
            foreach (var c in claims) _userClaims.Insert(new IdentityUserClaim<string> { UserId = user.Id, ClaimType = c.Type, ClaimValue = c.Value });
            return Task.CompletedTask;
        }

        public Task ReplaceClaimAsync(MercuriusUser user, Claim claim, Claim newClaim, CancellationToken ct)
        {
            _userClaims.DeleteMany(c => c.UserId == user.Id && c.ClaimType == claim.Type && c.ClaimValue == claim.Value);
            _userClaims.Insert(new IdentityUserClaim<string> { UserId = user.Id, ClaimType = newClaim.Type, ClaimValue = newClaim.Value });
            return Task.CompletedTask;
        }

        public Task RemoveClaimsAsync(MercuriusUser user, IEnumerable<Claim> claims, CancellationToken ct)
        {
            foreach (var claim in claims) _userClaims.DeleteMany(c => c.UserId == user.Id && c.ClaimType == claim.Type && c.ClaimValue == claim.Value);
            return Task.CompletedTask;
        }

        public Task<IList<MercuriusUser>> GetUsersForClaimAsync(Claim claim, CancellationToken ct)
        {
            var userIds = _userClaims.Find(c => c.ClaimType == claim.Type && c.ClaimValue == claim.Value).Select(c => c.UserId).Distinct().ToList();
            return Task.FromResult<IList<MercuriusUser>>(_users.Find(u => userIds.Contains(u.Id)).ToList());
        }

        public Task SetSecurityStampAsync(MercuriusUser u, string s, CancellationToken ct) { u.SecurityStamp = s; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);
        public Task<DateTimeOffset?> GetLockoutEndDateAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
        public Task SetLockoutEndDateAsync(MercuriusUser u, DateTimeOffset? e, CancellationToken ct) { u.LockoutEnd = e; return Task.CompletedTask; }
        public Task<int> IncrementAccessFailedCountAsync(MercuriusUser u, CancellationToken ct) { u.AccessFailedCount++; return Task.FromResult(u.AccessFailedCount); }
        public Task ResetAccessFailedCountAsync(MercuriusUser u, CancellationToken ct) { u.AccessFailedCount = 0; return Task.CompletedTask; }
        public Task<int> GetAccessFailedCountAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
        public Task<bool> GetLockoutEnabledAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnabled);
        public Task SetLockoutEnabledAsync(MercuriusUser u, bool e, CancellationToken ct) { u.LockoutEnabled = e; return Task.CompletedTask; }
        public Task SetTwoFactorEnabledAsync(MercuriusUser u, bool e, CancellationToken ct) { u.TwoFactorEnabled = e; return Task.CompletedTask; }
        public Task<bool> GetTwoFactorEnabledAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.TwoFactorEnabled);
        public Task SetPhoneNumberAsync(MercuriusUser u, string? p, CancellationToken ct) { u.PhoneNumber = p; return Task.CompletedTask; }
        public Task<string?> GetPhoneNumberAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.PhoneNumber);
        public Task<bool> GetPhoneNumberConfirmedAsync(MercuriusUser u, CancellationToken ct) => Task.FromResult(u.PhoneNumberConfirmed);
        public Task SetPhoneNumberConfirmedAsync(MercuriusUser u, bool c, CancellationToken ct) { u.PhoneNumberConfirmed = c; return Task.CompletedTask; }

        public Task AddLoginAsync(MercuriusUser user, UserLoginInfo login, CancellationToken ct)
        {
            _userLogins.Insert(new IdentityUserLogin<string> { UserId = user.Id, LoginProvider = login.LoginProvider, ProviderKey = login.ProviderKey, ProviderDisplayName = login.ProviderDisplayName });
            return Task.CompletedTask;
        }

        public Task RemoveLoginAsync(MercuriusUser user, string lp, string pk, CancellationToken ct)
        {
            _userLogins.DeleteMany(l => l.UserId == user.Id && l.LoginProvider == lp && l.ProviderKey == pk);
            return Task.CompletedTask;
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(MercuriusUser user, CancellationToken ct)
        {
            var logins = _userLogins.Find(l => l.UserId == user.Id).Select(l => new UserLoginInfo(l.LoginProvider, l.ProviderKey, l.ProviderDisplayName)).ToList();
            return Task.FromResult<IList<UserLoginInfo>>(logins);
        }

        public Task<MercuriusUser?> FindByLoginAsync(string lp, string pk, CancellationToken ct)
        {
            var login = _userLogins.FindOne(l => l.LoginProvider == lp && l.ProviderKey == pk);
            return Task.FromResult(login == null ? null : _users.FindById(login.UserId));
        }

        public Task SetAuthenticatorKeyAsync(MercuriusUser user, string key, CancellationToken ct)
        {
            _userTokens.DeleteMany(t => t.UserId == user.Id && t.LoginProvider == "[Authenticator]" && t.Name == "AuthenticatorKey");
            _userTokens.Insert(new IdentityUserToken<string> { UserId = user.Id, LoginProvider = "[Authenticator]", Name = "AuthenticatorKey", Value = key });
            return Task.CompletedTask;
        }

        public Task<string?> GetAuthenticatorKeyAsync(MercuriusUser user, CancellationToken ct)
        {
            var token = _userTokens.FindOne(t => t.UserId == user.Id && t.LoginProvider == "[Authenticator]" && t.Name == "AuthenticatorKey");
            return Task.FromResult(token?.Value);
        }

        // IUserAuthenticationTokenStore
        public Task SetTokenAsync(MercuriusUser user, string loginProvider, string name, string? value, CancellationToken ct)
        {
            _userTokens.DeleteMany(t => t.UserId == user.Id && t.LoginProvider == loginProvider && t.Name == name);
            if (value != null) _userTokens.Insert(new IdentityUserToken<string> { UserId = user.Id, LoginProvider = loginProvider, Name = name, Value = value });
            return Task.CompletedTask;
        }

        public Task RemoveTokenAsync(MercuriusUser user, string loginProvider, string name, CancellationToken ct)
        {
            _userTokens.DeleteMany(t => t.UserId == user.Id && t.LoginProvider == loginProvider && t.Name == name);
            return Task.CompletedTask;
        }

        public Task<string?> GetTokenAsync(MercuriusUser user, string loginProvider, string name, CancellationToken ct)
        {
            var token = _userTokens.FindOne(t => t.UserId == user.Id && t.LoginProvider == loginProvider && t.Name == name);
            return Task.FromResult(token?.Value);
        }

        public void Dispose() { }
    }
}
