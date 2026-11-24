namespace Mercurius.Common.Constants
{
    // Claim types the server issues at sign-in (MercuriusClaimsPrincipalFactory). In Mercurius.Shared,
    // keeping its original namespace, so the Blazor client checks exactly the same types.
    public static class MercuriusClaimTypes
    {
        public const string AccessPages = "access.pages";
        public const string TenantId = "tenant_id";
        public const string IsPlatformAdmin = "is_platform_admin";
        // In mobile tokens (Api/AuthController): the user's security stamp at sign-in. A password
        // reset or deactivation changes the stamp, which invalidates the token (Program.cs).
        public const string SecurityStamp = "sstamp";
    }
}
