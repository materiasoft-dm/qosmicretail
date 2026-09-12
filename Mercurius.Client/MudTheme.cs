using MudBlazor;

namespace Mercurius.Client
{
    // Matches the dark-navy/orange palette already used by the sidebar (NavMenu.razor) and the
    // mobile app's branding — so MudBlazor components picked up page-by-page look consistent with
    // the rest of the shell instead of MudBlazor's default purple Material palette.
    public static class MercuriusMudTheme
    {
        public static MudTheme Theme { get; } = new()
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#f97316",
                Secondary = "#0f172a",
                AppbarBackground = "#0f172a",
                Background = "#f7f7f7",
                Success = "#16a34a",
                Error = "#dc2626",
                Warning = "#f59e0b",
                Info = "#0ea5e9"
            },
            PaletteDark = new PaletteDark
            {
                Primary = "#fb923c",
                Secondary = "#1e1b3a",
                AppbarBackground = "#0f172a",
                Background = "#111827",
                Surface = "#1e293b"
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "8px"
            }
        };
    }
}
