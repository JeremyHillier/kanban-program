using System.Globalization;
using System.Windows;
using System.Windows.Data;
using KanbanApp.Views;

namespace KanbanApp.Converters;

// Where the cross sits over the blocks: its Margin, so it is in the middle of the gap where four blocks meet
// (BoardBlocks.CrossLocation). The values, in order: the blocks area's width and height, the split across and down, and how
// many blocks there are.
public sealed class BlockCrossPlacementConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 5 || values[0] is not double width || values[1] is not double height || values[2] is not double splitX
            || values[3] is not double splitY || values[4] is not int count) return new Thickness(0);

        var at = BoardBlocks.CrossLocation(count, width, height, splitX, splitY);
        return new Thickness(at.X, at.Y, 0, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
