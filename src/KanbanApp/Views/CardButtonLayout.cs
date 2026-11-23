using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// How the buttons left on a card are arranged, once Settings (Card buttons) has taken some off. The row along the
// foot holds the move buttons and Done / Delete; it uses as few rows as fit, as even as possible, so a narrow
// column never leaves one button on its own where that can be avoided. The flag, email and calendar buttons sit
// down the card's right side, unless they fit at the right end of a one-row foot: then they join it and the
// card's text has the full width. Pure, so the rules are tested without a window.
public static class CardButtonLayout
{
    // A quick button is 23 wide with 3 after it (QuickActionButtonStyle).
    public const double ButtonSlot = 26;

    // At least this much space between the foot's buttons and the side buttons when they share the row.
    public const double GroupGap = 6;

    // How many buttons go on each row: count buttons, at most perRow to a row, where groupStart is how many of
    // them come before Done / Delete (-1 when there is no such split). Two rows keep the move buttons and
    // Done / Delete apart when both fit that way and neither row is a single button; otherwise the rows are as
    // even as possible, the longer ones first.
    public static IReadOnlyList<int> SplitRows(int count, int perRow, int groupStart)
    {
        if (count <= 0) return [];
        perRow = Math.Max(1, perRow);
        if (count <= perRow) return [count];

        var rows = (count + perRow - 1) / perRow;
        var rest = count - groupStart;
        if (rows == 2 && groupStart > 1 && rest > 1 && groupStart <= perRow && rest <= perRow) return [groupStart, rest];

        var sizes = new List<int>();
        var left = count;
        for (var r = rows; r > 0; r--)
        {
            var size = (left + r - 1) / r;
            sizes.Add(size);
            left -= size;
        }
        return sizes;
    }

    public static int FootButtonCount(CardPartsShown parts) => CardPartsShown.FootParts.Count(parts.Shows);

    // The flag and calendar buttons are on large cards only; the email button needs someone to email.
    public static int SideButtonCount(CardPartsShown parts, bool isLargeCard, bool canEmail) =>
        (isLargeCard && parts.FlagButton ? 1 : 0) + (canEmail && parts.EmailButton ? 1 : 0) + (isLargeCard && parts.CalendarButton ? 1 : 0);

    // The side buttons join the foot when the foot has buttons and everything fits on one row of this width.
    public static bool SideButtonsJoinRow(double width, int footCount, int sideCount) =>
        width > 0 && footCount > 0 && sideCount > 0 && footCount * ButtonSlot + GroupGap + sideCount * ButtonSlot <= width;
}
