using LiteDB;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Mercurius.Repo.LiteDB
{
    public class LiteDbRoleStore :
        IRoleStore<IdentityRole>,
        IQueryableRoleStore<IdentityRole>,
        IRoleClaimStore<IdentityRole>,
        IDisposable
    {
        private readonly ILiteCollection<IdentityRole> _roles;
        private readonly ILiteCollection<IdentityRoleClaim<string>> _roleClaims;

        public IQueryable<IdentityRole> Roles => _roles.FindAll().AsQueryable();

        public LiteDbRoleStore(ILiteDatabase database)
        {
            _roles = database.GetCollection<IdentityRole>("identity_roles");
            _roleClaims = database.GetCollection<IdentityRoleClaim<string>>("identity_role_claims");
            // NOTE: index creation moved to LiteDbContext.EnsureCoreIndexes (startup-only)
            // to avoid SharedEngine sync races on every cookie-validation request.
        }

        public Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(role.Id)) role.Id = Guid.NewGuid().ToString();
            role.ConcurrencyStamp = Guid.NewGuid().ToString();
            _roles.Insert(role);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ConcurrencyStamp = Guid.NewGuid().ToString();
            _roles.Update(role);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _roles.Delete(role.Id);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) => Task.FromResult(_roles.FindById(roleId));
        public Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult(_roles.FindOne(r => r.NormalizedName == normalizedRoleName));

        public Task<string?> GetRoleIdAsync(IdentityRole role, CancellationToken cancellationToken) => Task.FromResult(role.Id);
        public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken cancellationToken) => Task.FromResult(role.Name);
        public Task SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken cancellationToken) { role.Name = roleName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken cancellationToken) => Task.FromResult(role.NormalizedName);
        public Task SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken cancellationToken) { role.NormalizedName = normalizedName; return Task.CompletedTask; }

        public Task<IList<Claim>> GetClaimsAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            var claims = _roleClaims.Find(c => c.RoleId == role.Id).Select(c => new Claim(c.ClaimType!, c.ClaimValue!)).ToList();
            return Task.FromResult<IList<Claim>>(claims);
        }

        public Task AddClaimAsync(IdentityRole role, Claim claim, CancellationToken cancellationToken)
        {
            _roleClaims.Insert(new IdentityRoleClaim<string> { RoleId = role.Id, ClaimType = claim.Type, ClaimValue = claim.Value });
            return Task.CompletedTask;
        }

        public Task RemoveClaimAsync(IdentityRole role, Claim claim, CancellationToken cancellationToken)
        {
            _roleClaims.DeleteMany(c => c.RoleId == role.Id && c.ClaimType == claim.Type && c.ClaimValue == claim.Value);
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}
