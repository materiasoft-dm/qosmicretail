using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mercurius.Models
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<MercuriusUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roleNames = { "Super Admin", "CASHIER" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            await CreateUser(userManager, "directorremj@gmail.com", "Mercurius123!", "Super Admin", "Pugoy", "Sonsona");
            await CreateUser(userManager, "markjaysonmendoza@gmail.com", "Mercurius123!", "Super Admin", "Mark Jayson", "Mendoza");
            await CreateUser(userManager, "williamlawrencejr06@gmail.com", "Mercurius123!", "CASHIER", "William", "Lawrence");
        }

        private static async Task CreateUser(UserManager<MercuriusUser> userManager, string email, string password, string role, string firstName, string lastName)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new MercuriusUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}
