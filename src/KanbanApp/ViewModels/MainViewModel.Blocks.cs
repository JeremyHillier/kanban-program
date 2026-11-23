using System.Windows.Data;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Blocks (Settings, Board tab, off at first): instead of one row of columns, the columns before Done are laid out two
// to a row - To Do and In Progress across the top, On Hold and Waiting below, then any the user added - with Done down
// the right at an ordinary column's width. Each block shows its cards in as many columns as fit (Views/BoardBlocks).
// Swimlanes keep their own layout. A preference, so it is in the Settings snapshot.
public partial class MainViewModel
{
    private bool _showColumnsAsBlocks;
    public bool ShowColumnsAsBlocks
    {
        get => _showColumnsAsBlocks;
        private set
        {
            if (SetField(ref _showColumnsAsBlocks, value)) NotifyBoardLayoutChanged();
        }
    }

    public void SetShowColumnsAsBlocks(bool value)
    {
        ShowColumnsAsBlocks = value;
        _db.SetFlag("ShowColumnsAsBlocks", value);
    }

    public bool IsBlocksOn => ShowColumnsAsBlocks && IsSwimlanesOff;
    public bool IsPlainBoardOn => !ShowColumnsAsBlocks && IsSwimlanesOff;

    // Every column but Done, in board order, following columns added, moved or removed.
    private ListCollectionView? _blockColumns;
    public ListCollectionView BlockColumns => _blockColumns ??= new ListCollectionView(Columns) { Filter = c => ((ColumnViewModel)c).Name != "Done" };

    // The width the column template draws a column at: an ordinary column's on the plain board and in swimlanes; in
    // blocks the column fills the block (or, for Done, the strip down the right).
    public double ColumnTemplateWidth => IsBlocksOn ? double.NaN : EffectiveColumnWidth;

    // Done, down the right: as wide as a column on a board of the five standard columns.
    public double BlocksDoneWidth => FittedColumnWidth(IsFitColumnsToWindow, _boardWidth, KanbanColumn.RoleNames.Length, ColumnWidth, MinFittedColumnWidth);
    public double BlocksDoneSlotWidth => BlocksDoneWidth + ColumnGap;

    // The blocks share what is left, two to a row, but never come out narrower than one narrowest column each
    // (the board scrolls sideways instead). Not fitted to the window, each block is two fixed-width columns wide.
    public double BlocksWidth => IsFitColumnsToWindow
        ? Math.Max(2 * (MinFittedColumnWidth + ColumnGap), Math.Floor(_boardWidth - 1 - BlocksDoneSlotWidth))
        : 4 * (ColumnWidth + ColumnGap);

    private void NotifyBoardLayoutChanged()
    {
        OnPropertyChanged(nameof(IsBlocksOn));
        OnPropertyChanged(nameof(IsPlainBoardOn));
        NotifyColumnWidthChanged();
    }
}
