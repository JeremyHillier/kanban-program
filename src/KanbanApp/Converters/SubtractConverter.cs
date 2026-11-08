using System.Globalization;
using System.Windows.Data;

namespace KanbanApp.Converters;

// A width less a fixed amount (the parameter): lets one element take the visible width of another whose last
// child carries a trailing margin. Until the other has been measured (zero) it gives NaN, so the element keeps its
// natural width for that moment.
public class SubtractConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width || width <= 0) return double.NaN;
        var amount = parameter is not null && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0;
        return Math.Max(0, width - amount);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
