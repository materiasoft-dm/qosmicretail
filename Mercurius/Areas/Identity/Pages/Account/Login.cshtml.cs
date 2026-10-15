// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Mercurius.Common;
using Mercurius.Models;
using System.Data;
using Mercurius.Repo.IdentityModel;

namespace Mercurius.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<MercuriusUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public LoginModel(SignInManager<MercuriusUser> signInManager, ILogger<LoginModel> logger, Mercurius.Services.Auditing.IAuditLogger auditLogger)
        {
            _signInManager = signInManager;
            _logger = logger;
            _auditLogger = auditLogger;
        }

        private async Task AuditLoginAsync(string action, string summary)
        {
            // HttpContext.User is still anonymous at this point, so name the actor explicitly —
            // or, for an unknown email, record just what was typed.
            var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
            var actor = new Mercurius.Services.Auditing.AuditActor(user?.Id, user?.Email ?? Input.Email, user?.TenantId);
            await _auditLogger.LogAsync(action, summary, new { email = Input.Email, channel = "Web" }, actor,
                entityType: user != null ? "MercuriusUser" : null, entityId: user?.Id);
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; } = default!;

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; } = string.Empty;

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {

            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Honor the user's "Remember me" choice and let repeated failures trigger
                // ASP.NET Identity's built-in lockout (configured by AddIdentity options).
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // Role claims are merged into the principal by MercuriusClaimsPrincipalFactory at sign-in.
                    _logger.LogInformation("User logged in.");
                    await AuditLoginAsync(Mercurius.Services.Auditing.AuditActions.LoginSucceeded, $"Signed in to the web app as {Input.Email}");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    await AuditLoginAsync(Mercurius.Services.Auditing.AuditActions.LockedOut, $"Web sign-in blocked — account locked out: {Input.Email}");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    await AuditLoginAsync(Mercurius.Services.Auditing.AuditActions.LoginFailed, $"Failed web sign-in attempt for {Input.Email}");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
