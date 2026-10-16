using MudBlazor;

namespace Mercurius.Client
{
    // Deep navy and light ("sky") blue — the same palette as the sidebar (NavMenu.razor.css), the
    // shell (MainLayout.razor.css) and the sign-in pages — so MudBlazor components look like part
    // of the app rather than MudBlazor's default purple Material palette.
    public static class MercuriusMudTheme
    {
        private static readonly string[] FontStack = { "Inter", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif" };

        public static MudTheme Theme { get; } = new()
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#0284c7",
                PrimaryDarken = "#0369a1",
                PrimaryLighten = "#38bdf8",
                Secondary = "#0f2847",
                Tertiary = "#6366f1",
                AppbarBackground = "#0f2847",
                Background = "#f3f7fb",
                BackgroundGray = "#eef3f8",
                Surface = "#ffffff",
                TextPrimary = "#0f1e33",
                TextSecondary = "#5b6b82",
                ActionDefault = "#5b6b82",
                LinesDefault = "#e2e9f1",
                LinesInputs = "#cbd6e2",
                TableLines = "#edf1f6",
                TableHover = "#f2f8fd",
                TableStriped = "#f8fafc",
                Divider = "#e2e9f1",
                Success = "#16a34a",
                Error = "#dc2626",
                Warning = "#d97706",
                Info = "#0ea5e9"
            },
            PaletteDark = new PaletteDark
            {
                Primary = "#38bdf8",
                PrimaryDarken = "#0ea5e9",
                Secondary = "#1e3a5f",
                AppbarBackground = "#0b1730",
                Background = "#0b1220",
                Surface = "#111b2e",
                LinesDefault = "#1f2c44",
                TableLines = "#1f2c44",
                Info = "#38bdf8"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = FontStack, FontSize = ".9rem", LineHeight = "1.5" },
                H4 = new H4Typography { FontFamily = FontStack, FontSize = "1.75rem", FontWeight = "700", LineHeight = "1.25", LetterSpacing = "-.02em" },
                H5 = new H5Typography { FontFamily = FontStack, FontSize = "1.3rem", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "-.01em" },
                H6 = new H6Typography { FontFamily = FontStack, FontSize = "1.05rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "0" },
                // Subtitle1 is also the text style of every input and its label — keep it regular.
                Subtitle1 = new Subtitle1Typography { FontFamily = FontStack, FontWeight = "400" },
                Subtitle2 = new Subtitle2Typography { FontFamily = FontStack, FontWeight = "600" },
                Body1 = new Body1Typography { FontFamily = FontStack, FontSize = ".9rem", LineHeight = "1.55" },
                Body2 = new Body2Typography { FontFamily = FontStack, FontSize = ".84rem", LineHeight = "1.5" },
                Caption = new CaptionTypography { FontFamily = FontStack, FontSize = ".76rem", LetterSpacing = ".01em" },
                Overline = new OverlineTypography { FontFamily = FontStack, FontWeight = "600", LetterSpacing = ".08em" },
                // Sentence-case buttons read as a product, not a Material demo.
                Button = new ButtonTypography { FontFamily = FontStack, FontWeight = "600", TextTransform = "none", LetterSpacing = ".01em" }
            },
            // Soft, blue-tinted shadows instead of Material's grey ones.
            Shadows = new Shadow
            {
                Elevation = Enumerable.Range(0, 26).Select(ShadowFor).ToArray()
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "10px"
            }
        };

        private static string ShadowFor(int level) => level switch
        {
            0 => "none",
            1 => "0 1px 2px rgba(15, 40, 71, .05), 0 1px 3px rgba(15, 40, 71, .07)",
            2 => "0 1px 2px rgba(15, 40, 71, .05), 0 4px 12px rgba(15, 40, 71, .07)",
            3 or 4 => "0 2px 4px rgba(15, 40, 71, .05), 0 8px 20px rgba(15, 40, 71, .09)",
            <= 8 => "0 4px 8px rgba(15, 40, 71, .06), 0 14px 32px rgba(15, 40, 71, .12)",
            _ => "0 8px 16px rgba(15, 40, 71, .08), 0 24px 56px rgba(15, 40, 71, .18)"
        };
    }
}
