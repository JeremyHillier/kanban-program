using System.Globalization;
using System.Windows.Data;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Converters;

// "Row" when a card's flag, email and calendar buttons fit at the right end of its foot row, "Side" when they stay
// down the card's right side (CardButtonLayout.SideButtonsJoinRow). The values, in order: the width the card's
// content has, the board's CardParts, ShowCardButtons and IsLargeCards, and the card's CanEmailCard.
public sealed class CardButtonPlacementConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 5 || values[0] is not double width || values[1] is not CardPartsShown parts
            || values[2] is not bool showButtons || values[3] is not bool isLargeCard) return "Side";

        var foot = showButtons ? CardButtonLayout.FootButtonCount(parts) : 0;
        var side = CardButtonLayout.SideButtonCount(parts, isLargeCard, values[4] is true);
        return CardButtonLayout.SideButtonsJoinRow(width, foot, side) ? "Row" : "Side";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
