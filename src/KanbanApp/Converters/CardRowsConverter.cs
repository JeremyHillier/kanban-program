using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using KanbanApp.Views;

namespace KanbanApp.Converters;

// A column's cards as rows of that many side by side (CardRowList), for a block wide enough for more than one across.
// The values, in order: the column's CardsView, how many go across, and the column's own name. One across: the cards as
// they are.
public sealed class CardRowsConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 3 && values[0] is ICollectionView cards && values[1] is int perRow && perRow > 1 && values[2] is string name
            ? new CardRowList(cards, perRow, name)
            : values.Length > 0 ? values[0] : null;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
