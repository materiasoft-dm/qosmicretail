using MudBlazor;

namespace Mercurius.Client
{
    // Emerald on warm stone neutrals, the same palette as IntelliMed. The sidebar (NavMenu.razor.css),
    // the shell (MainLayout.razor.css), the sign-in pages and the :root tokens in wwwroot/css/app.css
    // use the same values, so MudBlazor components look like part of the app.
    public static class MercuriusMudTheme
    {
        private static readonly string[] FontStack = { "Inter", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif" };

        public static MudTheme Theme { get; } = new()
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#10b981",
                PrimaryDarken = "#059669",
                PrimaryLighten = "#34d399",
                Secondary = "#064e3b",
                Tertiary = "#0d9488",
                AppbarBackground = "#ffffff",
                AppbarText = "#0c0a09",
                Background = "#f9f9f8",
                BackgroundGray = "#f5f5f4",
                Surface = "#ffffff",
                TextPrimary = "#0c0a09",
                TextSecondary = "#78716c",
                ActionDefault = "#78716c",
                LinesDefault = "#e7e5e4",
                LinesInputs = "#d6d3d1",
                TableLines = "#e7e5e4",
                TableHover = "#f5f5f4",
                TableStriped = "#fafaf9",
                Divider = "#e7e5e4",
                Success = "#16a34a",
                Error = "#dc2626",
                Warning = "#d97706",
                Info = "#2563eb"
            },
            PaletteDark = new PaletteDark
            {
                Primary = "#34d399",
                PrimaryDarken = "#10b981",
                Secondary = "#6ee7b7",
                AppbarBackground = "#1c1917",
                Background = "#0c0a09",
                Surface = "#1c1917",
                LinesDefault = "#292524",
                TableLines = "#292524",
                Info = "#60a5fa"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = FontStack, FontSize = ".875rem", LineHeight = "1.5" },
                H4 = new H4Typography { FontFamily = FontStack, FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "-.01em" },
                H5 = new H5Typography { FontFamily = FontStack, FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.35", LetterSpacing = "-.01em" },
                H6 = new H6Typography { FontFamily = FontStack, FontSize = "1rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "0" },
                // Subtitle1 is also the text style of every input and its label — keep it regular.
                Subtitle1 = new Subtitle1Typography { FontFamily = FontStack, FontWeight = "400" },
                Subtitle2 = new Subtitle2Typography { FontFamily = FontStack, FontWeight = "600" },
                Body1 = new Body1Typography { FontFamily = FontStack, FontSize = ".875rem", LineHeight = "1.55" },
                Body2 = new Body2Typography { FontFamily = FontStack, FontSize = ".82rem", LineHeight = "1.5" },
                Caption = new CaptionTypography { FontFamily = FontStack, FontSize = ".75rem", LetterSpacing = ".01em" },
                Overline = new OverlineTypography { FontFamily = FontStack, FontWeight = "600", LetterSpacing = ".08em" },
                // Sentence-case buttons read as a product, not a Material demo.
                Button = new ButtonTypography { FontFamily = FontStack, FontWeight = "500", TextTransform = "none", LetterSpacing = "0" }
            },
            // Flat surfaces: a hairline shadow on cards, real depth only on menus and dialogs.
            Shadows = new Shadow
            {
                Elevation = Enumerable.Range(0, 26).Select(ShadowFor).ToArray()
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "8px"
            }
        };

        private static string ShadowFor(int level) => level switch
        {
            0 => "none",
            1 or 2 => "0 1px 2px rgba(12, 10, 9, .05)",
            3 or 4 => "0 4px 12px rgba(12, 10, 9, .10)",
            <= 8 => "0 10px 30px -8px rgba(12, 10, 9, .22), 0 2px 6px rgba(12, 10, 9, .06)",
            _ => "0 18px 48px -12px rgba(12, 10, 9, .28), 0 4px 12px rgba(12, 10, 9, .08)"
        };
    }
}
