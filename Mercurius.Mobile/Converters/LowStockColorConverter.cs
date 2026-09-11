using System.Globalization;

namespace Mercurius.Mobile.Converters;

// Doubles as both the stock-status text color converter and, via ConverterParameter="Background",
// the matching light "badge" tint — mirrors the web admin's badge-light-danger/badge-light-success
// styling so the mobile Products table reads consistently with it.
public class LowStockColorConverter : IValueConverter
{
    private static readonly Color LowStockColor = Color.FromArgb("#D32F2F");
    private static readonly Color NormalStockColor = Color.FromArgb("#2E7D32");
    private static readonly Color LowStockBackground = Color.FromArgb("#FBE9E9");
    private static readonly Color NormalStockBackground = Color.FromArgb("#E8F5EA");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isLow = value is bool b && b;
        if (string.Equals(parameter as string, "Background", StringComparison.OrdinalIgnoreCase))
        {
            return isLow ? LowStockBackground : NormalStockBackground;
        }
        return isLow ? LowStockColor : NormalStockColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
