using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Collapsing the button column, and the count of tasks the filters are hiding that the collapsed
// strip shows in their place.
[Collection(WpfCollection.Name)]
public sealed class SidebarTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static CardViewModel Add(MainViewModel board, string title, string priority = "Normal", DateTime? start = null) =>
        board.AddCard(title, board.Columns.First(), board.Projects.First(), priority, null, null, false, null, null, startDate: start);

    [Fact]
    public void ItStartsShowing_AndCollapsingIsRemembered() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.False(board.IsSidebarCollapsed);
        Assert.True(board.IsSidebarExpanded);

        var raised = new List<string?>();
        board.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        board.ToggleSidebar();

        Assert.True(board.IsSidebarCollapsed);
        Assert.False(board.IsSidebarExpanded);
        Assert.Contains(nameof(MainViewModel.IsSidebarCollapsed), raised);
        Assert.Contains(nameof(MainViewModel.IsSidebarExpanded), raised);
        Assert.True(OpenBoard().IsSidebarCollapsed);

        board.ToggleSidebar();
        Assert.False(OpenBoard().IsSidebarCollapsed);
    });

    [Fact]
    public void TheArrows_AndTheHideLink_FollowTheSideTheColumnIsOn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Equal(("«", "»", System.Windows.HorizontalAlignment.Right), (board.SidebarCollapseGlyph, board.SidebarExpandGlyph, board.SidebarHideAlignment));

        board.ToggleButtonPosition();

        Assert.Equal(("»", "«", System.Windows.HorizontalAlignment.Left), (board.SidebarCollapseGlyph, board.SidebarExpandGlyph, board.SidebarHideAlignment));
    });

    [Fact]
    public void TheHiddenCount_FollowsTheFilters_AndHideFuture_AndSaysNothingWhenNothingIsHidden() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "High one", "High");
        Add(board, "Normal one");
        Add(board, "Normal two");
        Add(board, "Starts next week", "High", DateTime.Today.AddDays(7));
        Assert.Equal(0, board.HiddenTaskCount);
        Assert.Equal(string.Empty, board.HiddenTasksLabel);

        var raised = 0;
        board.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.HiddenTasksLabel)) raised++; };
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();

        Assert.Equal(2, board.HiddenTaskCount);
        Assert.Equal("2 hidden by filters", board.HiddenTasksLabel);
        Assert.True(raised > 0);

        board.ToggleHideFutureTasks();
        Assert.Equal(3, board.HiddenTaskCount);

        board.ToggleHideFutureTasks();
        board.ClearFilters();
        board.ApplyFilters();
        Assert.Equal(0, board.HiddenTaskCount);
    });

    [Fact]
    public void ATaskAddedOrEditedOutOfSight_IsCountedStraightAway() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();

        Add(board, "Low one", "Low");

        Assert.Equal(1, board.HiddenTaskCount);
    });

    [Fact]
    public void CompactButtons_StartsOff_NarrowsTheColumn_StacksTheDates_AndIsRemembered() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.False(board.IsCompactButtons);
        Assert.Equal((MainViewModel.SidebarWidthNormal, 2), (board.SidebarWidth, board.DateRangeColumns));

        var raised = new List<string?>();
        board.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        board.SetCompactButtons(true);

        Assert.Equal((MainViewModel.SidebarWidthCompact, 1), (board.SidebarWidth, board.DateRangeColumns));
        Assert.True(MainViewModel.SidebarWidthCompact < MainViewModel.SidebarWidthNormal);
        Assert.Contains(nameof(MainViewModel.SidebarWidth), raised);
        Assert.Contains(nameof(MainViewModel.DateRangeColumns), raised);
        Assert.True(OpenBoard().IsCompactButtons);

        board.SetCompactButtons(false);
        Assert.False(OpenBoard().IsCompactButtons);
    });

    [Fact]
    public void CompactAndCollapsed_AreIndependent() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetCompactButtons(true);
        board.ToggleSidebar();

        var reopened = OpenBoard();
        Assert.True(reopened.IsCompactButtons);
        Assert.True(reopened.IsSidebarCollapsed);
    });
}
