namespace KanbanApp.ViewModels;

// Collapsing the button column to a thin strip, to give the board the whole width. It is a view
// choice like Hide Future, remembered between sessions and not part of the Settings screen (so it
// isn't in the Settings snapshot). Every keyboard shortcut keeps working while it is collapsed.
public partial class MainViewModel
{
    public bool IsSidebarCollapsed { get; private set; }

    public bool IsSidebarExpanded => !IsSidebarCollapsed;

    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        _db.SetFlag("SidebarCollapsed", IsSidebarCollapsed);
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(IsSidebarExpanded));
    }

    private void LoadSidebarState() => IsSidebarCollapsed = _db.GetFlag("SidebarCollapsed", false);

    // The arrows point the way the column will move, which depends on the side it is docked to.
    public string SidebarCollapseGlyph => IsButtonsOnRight ? "»" : "«";
    public string SidebarExpandGlyph => IsButtonsOnRight ? "«" : "»";

    // The Hide link sits on the edge of the column nearest the board.
    public System.Windows.HorizontalAlignment SidebarHideAlignment => IsButtonsOnRight ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Right;

    // With the filters out of sight, the strip has to say when they are hiding something -
    // otherwise a task that seems to have vanished has no explanation on screen. Counts everything
    // not showing, whichever filter (or Hide Future) is the reason.
    public int HiddenTaskCount => Columns.SelectMany(c => c.Cards).Count(c => !c.IsVisible);

    public string HiddenTasksLabel => HiddenTaskCount == 0 ? string.Empty : $"{HiddenTaskCount} hidden by filters";

    private void NotifyHiddenTasksChanged()
    {
        OnPropertyChanged(nameof(HiddenTaskCount));
        OnPropertyChanged(nameof(HiddenTasksLabel));
    }
}

// Compact buttons: a tighter button column for smaller screens. The text stays the same size (some
// labels are already at the limit of what reads comfortably); what shrinks is button height, the
// large lower buttons, the padding and the column's width. Unlike collapsing, this one is a
// preference set in Settings, so it is in the Settings snapshot.
public partial class MainViewModel
{
    public const double SidebarWidthNormal = 344;
    public const double SidebarWidthCompact = 300;

    public bool IsCompactButtons { get; private set; }

    public double SidebarWidth => IsCompactButtons ? SidebarWidthCompact : SidebarWidthNormal;

    public System.Windows.Thickness SidebarPadding => new(IsCompactButtons ? 8 : 12);

    // From and To sit side by side normally and stack when compact, where one row is too narrow
    // for two full dates.
    public int DateRangeColumns => IsCompactButtons ? 1 : 2;
    public System.Windows.Thickness DateRangeFromMargin => IsCompactButtons ? new(0, 0, 0, 4) : new(0, 0, 4, 4);
    public System.Windows.Thickness DateRangeToMargin => IsCompactButtons ? new(0, 0, 0, 4) : new(4, 0, 0, 4);

    public void SetCompactButtons(bool value)
    {
        if (IsCompactButtons == value) return;
        IsCompactButtons = value;
        _db.SetFlag("CompactButtons", value);
        OnPropertyChanged(nameof(IsCompactButtons));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarPadding));
        OnPropertyChanged(nameof(DateRangeColumns));
        OnPropertyChanged(nameof(DateRangeFromMargin));
        OnPropertyChanged(nameof(DateRangeToMargin));
    }

    private void LoadCompactButtons() => IsCompactButtons = _db.GetFlag("CompactButtons", false);
}
