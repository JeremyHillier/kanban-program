using System.Windows.Data;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Blocks (Settings, Board tab, off at first): instead of one row of columns, the columns before Done are laid out two
// to a row - To Do and In Progress across the top, On Hold and Waiting below, then any the user added - with Done down
// the right at an ordinary column's width. Each block shows its cards in as many columns as fit (Views/BoardBlocks).
// Swimlanes keep their own layout. A preference, so it is in the Settings snapshot. The cross where the blocks meet
// is dragged to resize them, and where it was left is kept in the file.
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
        if (value && _blocksSetAside)
        {
            _blocksSetAside = false; // switched on again in Settings: try the blocks once more
            NotifyBoardLayoutChanged();
        }
        ShowColumnsAsBlocks = value;
        _db.SetFlag("ShowColumnsAsBlocks", value);
    }

    // Where the blocks meet, dragged with the cross in the middle: the left blocks' share of the width and the top row's
    // share of the height. Kept in the task file when a drag ends (SaveBlocksSplit); the panel keeps every block big enough.
    private double _blocksSplitX = 0.5;
    private double _blocksSplitY = 0.5;

    public double BlocksSplitX
    {
        get => _blocksSplitX;
        set => SetField(ref _blocksSplitX, Math.Clamp(value, 0.05, 0.95));
    }

    public double BlocksSplitY
    {
        get => _blocksSplitY;
        set => SetField(ref _blocksSplitY, Math.Clamp(value, 0.05, 0.95));
    }

    private void LoadBlocksSplit()
    {
        _blocksSplitX = ParseSplit(_db.GetSetting("BlocksSplitX"));
        _blocksSplitY = ParseSplit(_db.GetSetting("BlocksSplitY"));
    }

    public void SaveBlocksSplit()
    {
        _db.SetSetting("BlocksSplitX", FormatSplit(BlocksSplitX));
        _db.SetSetting("BlocksSplitY", FormatSplit(BlocksSplitY));
    }

    // Double-clicking the cross: the blocks even again.
    public void ResetBlocksSplit()
    {
        BlocksSplitX = 0.5;
        BlocksSplitY = 0.5;
        SaveBlocksSplit();
    }

    // Anything that is not a share between 5% and 95% is the even split.
    internal static double ParseSplit(string? text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && value is >= 0.05 and <= 0.95 ? value : 0.5;

    internal static string FormatSplit(double split) => split.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    public bool IsBlocksOn => ShowColumnsAsBlocks && !_blocksSetAside && IsSwimlanesOff;
    public bool IsPlainBoardOn => (!ShowColumnsAsBlocks || _blocksSetAside) && IsSwimlanesOff;

    // After the blocks failed to lay out (App's error handler sees the same layout error again and again, which froze
    // the board), the board is drawn as ordinary columns until the app is next started. The setting itself is kept.
    private bool _blocksSetAside;

    public void SetAsideBlocksForThisSession()
    {
        if (_blocksSetAside) return;
        _blocksSetAside = true;
        NotifyBoardLayoutChanged();
    }

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
