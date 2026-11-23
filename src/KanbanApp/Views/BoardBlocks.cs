using System.Windows;

namespace KanbanApp.Views;

// The board as blocks (Settings, Board tab): how many cards a block shows side by side, how wide each one is, and
// where a dropped card lands among them. Pure, so the rules are tested without a window.
public static class BoardBlocks
{
    // The space between cards side by side.
    public const double CardGap = 6;

    // A column's padding (10 a side) and border (1 a side): a card is this much narrower than its column, before
    // the list's scroll bar.
    public const double ColumnChrome = 22;

    // How many cards go side by side in a list this wide: as many as fit at the width a card has in the narrowest
    // fitted column (Settings), so cards in a block are never narrower than they can be on the plain board.
    // One when the board is not in blocks.
    public static int CardColumns(double listWidth, bool isBlocks, int minColumnWidth, double scrollBarWidth = 17)
    {
        if (!isBlocks || listWidth <= 0) return 1;
        var area = listWidth - scrollBarWidth;
        var cardWidth = Math.Max(120, minColumnWidth - ColumnChrome - scrollBarWidth);
        return Math.Max(1, (int)((area + CardGap) / (cardWidth + CardGap)));
    }

    // A card's width in a list showing this many side by side: the whole width for one (as before), otherwise an
    // equal share less the gap. Before the list has a size (0), the card is left to size itself.
    public static double CardWidth(double viewportWidth, int columns) =>
        viewportWidth <= 0 ? double.NaN : columns <= 1 ? viewportWidth : Math.Floor(viewportWidth / columns) - CardGap;

    // Where a dragged card goes among the others. In one column of cards it goes before the first card whose middle
    // is below the pointer; with cards side by side it is reading order: before a card in a later row, or before
    // one in the pointer's row whose middle is to its right.
    public static bool DropsBefore(Point pointer, Rect card, bool sideBySide) => sideBySide
        ? pointer.Y < card.Top || (pointer.Y < card.Bottom && pointer.X < card.Left + card.Width / 2)
        : pointer.Y < card.Top + card.Height / 2;
}
