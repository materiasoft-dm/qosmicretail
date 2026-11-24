using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Mercurius.Services
{
    /// <summary>
    /// Identity's sign-in manager, plus: a deactivated user (MercuriusUser.IsActive = false) can't
    /// sign in. Both password sign-ins — the web form (PasswordSignInAsync, which calls this) and
    /// the mobile app (Api/AuthController) — go through CheckPasswordSignInAsync, so a deactivated
    /// account gets SignInResult.NotAllowed on both.
    ///
    /// The check runs only AFTER the password is verified (unlike CanSignInAsync, which Identity
    /// calls before looking at the password): otherwise anyone who knew an email address could
    /// learn whether that account is deactivated. Existing web sessions end at the next
    /// security-stamp check, since deactivating also changes the stamp (UsersApiController);
    /// existing mobile tokens are checked per request in Program.cs's JwtBearer OnTokenValidated.
    /// </summary>
    public class MercuriusSignInManager : SignInManager<MercuriusUser>
    {
        public MercuriusSignInManager(
            UserManager<MercuriusUser> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<MercuriusUser> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<MercuriusUser>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<MercuriusUser> confirmation)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
        }

        public override async Task<SignInResult> CheckPasswordSignInAsync(MercuriusUser user, string password, bool lockoutOnFailure)
        {
            var result = await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
            return result.Succeeded && !user.IsActive ? SignInResult.NotAllowed : result;
        }
    }
}
