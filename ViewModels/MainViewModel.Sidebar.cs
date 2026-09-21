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
        _db.SetSetting("SidebarCollapsed", IsSidebarCollapsed ? "True" : "False");
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(IsSidebarExpanded));
    }

    private void LoadSidebarState() => IsSidebarCollapsed = _db.GetSetting("SidebarCollapsed") == "True";

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
