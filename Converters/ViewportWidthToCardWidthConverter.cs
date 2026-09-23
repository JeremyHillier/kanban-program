using System.Globalization;
using System.Windows.Data;

namespace KanbanApp.Converters;

// A column's card list lays every card out at least as wide as the widest card it has ever
// measured - a VirtualizingStackPanel remembers that width even for cards scrolled out of view. So
// once the column's scrollbar appears (or the column is made narrower), cards measured before it
// stay too wide and run under the scrollbar. Setting each card to the width the list actually
// shows keeps them inside it. Before the list has a size (0), the card is left to size itself.
public sealed class ViewportWidthToCardWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width && width > 0 ? width : double.NaN;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
