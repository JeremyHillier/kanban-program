using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KanbanApp.Converters;

public class PriorityToBrushConverter : IValueConverter
{
    private static readonly Brush High = new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x4F));
    private static readonly Brush Medium = new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x3E));
    private static readonly Brush Normal = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            "High" => High,
            "Medium" => Medium,
            _ => Normal
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
