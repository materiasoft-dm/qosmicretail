using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mercurius.Repo.LiteDB
{
    public static class LiteDbIdentityBuilderExtensions
    {
        public static IdentityBuilder AddLiteDbStores(this IdentityBuilder builder)
        {
            builder.Services.TryAddScoped<IUserStore<IdentityModel.MercuriusUser>, LiteDbUserStore>();
            builder.Services.TryAddScoped<IRoleStore<IdentityRole>, LiteDbRoleStore>();
            builder.Services.TryAddScoped<IQueryableUserStore<IdentityModel.MercuriusUser>>(
                sp => (LiteDbUserStore)sp.GetRequiredService<IUserStore<IdentityModel.MercuriusUser>>());
            builder.Services.TryAddScoped<IQueryableRoleStore<IdentityRole>>(
                sp => (LiteDbRoleStore)sp.GetRequiredService<IRoleStore<IdentityRole>>());
            return builder;
        }
    }
}
