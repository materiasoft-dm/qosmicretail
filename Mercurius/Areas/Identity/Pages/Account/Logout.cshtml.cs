// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Mercurius.Models;
using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Mercurius.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<MercuriusUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public LogoutModel(SignInManager<MercuriusUser> signInManager, ILogger<LogoutModel> logger, Mercurius.Services.Auditing.IAuditLogger auditLogger)
        {
            _signInManager = signInManager;
            _logger = logger;
            _auditLogger = auditLogger;
        }

        // Recorded BEFORE signing out, while HttpContext.User is still the person leaving.
        private Task AuditLogoutAsync() =>
            User.Identity?.IsAuthenticated == true
                ? _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.Logout, $"Signed out of the web app: {User.Identity.Name}")
                : Task.CompletedTask;

        public async Task<IActionResult> OnGet(string returnUrl = null)
        {
            await AuditLogoutAsync();
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return LocalRedirect("/");
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            await AuditLogoutAsync();
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out via POST.");
            
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return LocalRedirect("/");
        }
    }
}
