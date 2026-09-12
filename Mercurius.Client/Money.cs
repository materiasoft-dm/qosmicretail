namespace Mercurius.Client
{
    // Blazor WASM defaults to invariant/en-US culture regardless of the server's or browser's
    // locale (loading full ICU data just to get a PHP culture would bloat the download for no
    // real benefit) — ToString("C2") on a bare decimal renders "$", not "₱", silently
    // mismatching every other currency display in the app (server-rendered MVC pages, the
    // mobile app's JS, and this app's own dashboard/product prices). Format explicitly instead
    // of depending on culture.
    public static class Money
    {
        public static string Format(decimal value) => "₱" + value.ToString("N2");
        public static string Format(decimal? value) => value.HasValue ? Format(value.Value) : "-";
    }
}
