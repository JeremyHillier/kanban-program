using System.Globalization;
using System.Windows;
using System.Windows.Data;
using KanbanApp.Views;

namespace KanbanApp.Converters;

// How many cards a column's list shows side by side (BoardBlocks.CardColumns). The values, in order: the list's
// width, the board's IsBlocksOn and MinFittedColumnWidth.
public sealed class CardColumnsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 3 && values[0] is double width && values[1] is bool isBlocks && values[2] is int minWidth
            ? BoardBlocks.CardColumns(width, isBlocks, minWidth, SystemParameters.VerticalScrollBarWidth)
            : 1;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// A card's width (BoardBlocks.CardWidth). The values: the list's viewport width and how many cards go side by side.
public sealed class CardWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 2 && values[0] is double width ? BoardBlocks.CardWidth(width, values[1] is int columns ? columns : 1) : double.NaN;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// True when a list shows more than one card side by side.
public sealed class MoreThanOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int count && count > 1;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
