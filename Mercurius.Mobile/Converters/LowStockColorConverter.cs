using System.Globalization;

namespace Mercurius.Mobile.Converters;

public class LowStockColorConverter : IValueConverter
{
    private static readonly Color LowStockColor = Color.FromArgb("#D32F2F");
    private static readonly Color NormalStockColor = Color.FromArgb("#2E7D32");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isLow = value is bool b && b;
        return isLow ? LowStockColor : NormalStockColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
