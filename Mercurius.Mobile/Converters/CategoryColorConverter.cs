using System.Globalization;

namespace Mercurius.Mobile.Converters;

// Gives every category a stable, distinct tile color (deterministic from the category name) so
// the product grid reads like Loyverse's colorful item tiles even without product photos synced
// yet — a real photo per product is a natural next step once the sync API carries image URLs.
public class CategoryColorConverter : IValueConverter
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb("#F2994A"), // Primary orange
        Color.FromArgb("#2D9CDB"), // blue
        Color.FromArgb("#27AE60"), // green
        Color.FromArgb("#9B51E0"), // purple
        Color.FromArgb("#EB5757"), // red
        Color.FromArgb("#F2C94C"), // yellow
        Color.FromArgb("#56CCF2"), // sky
        Color.FromArgb("#BB6BD9"), // orchid
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text)) return Color.FromArgb("#6E6E6E");
        var index = Math.Abs(text.GetHashCode()) % Palette.Length;
        return Palette[index];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
