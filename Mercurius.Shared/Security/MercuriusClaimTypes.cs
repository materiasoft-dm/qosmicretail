namespace Mercurius.Common.Constants
{
    // Claim types the server issues at sign-in (MercuriusClaimsPrincipalFactory). In Mercurius.Shared,
    // keeping its original namespace, so the Blazor client checks exactly the same types.
    public static class MercuriusClaimTypes
    {
        public const string AccessPages = "access.pages";
        public const string TenantId = "tenant_id";
        public const string IsPlatformAdmin = "is_platform_admin";
    }
}
