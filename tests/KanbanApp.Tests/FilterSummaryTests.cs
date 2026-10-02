using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The line under the banner's title that says what is filtering the board.
public class FilterSummaryTests
{
    [Fact]
    public void NothingFiltering_SaysNothing()
    {
        Assert.Empty(FilterSummary.Parts(new CustomFilter(), hideFuture: false));
        Assert.Equal("", FilterSummary.Line(new CustomFilter(), false, null, 40, 40));
    }

    [Fact]
    public void EveryKindOfFilter_IsNamed_InButtonColumnOrder()
    {
        var state = new CustomFilter
        {
            Due = "Today", Project = ["Website", "Admin"], Priority = ["High"], Who = ["Sam Lee"],
            Goal = "Grow", Flag = "Urgent", WaitingOn = "Supplier", Keyword = " bank "
        };

        Assert.Equal(
            ["Today", "Project: Website, Admin", "Priority: High", "Who: Sam Lee", "Goal: Grow", "Flag: Urgent",
             "Waiting on: Supplier", "Keyword: \"bank\"", "Future tasks hidden"],
            FilterSummary.Parts(state, hideFuture: true));
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-15", "Due Oct 1 to Oct 15")]
    [InlineData("2026-10-01", null, "Due from Oct 1")]
    [InlineData(null, "2026-10-15", "Due up to Oct 15")]
    public void ADueDateRange_ReadsNaturally(string? from, string? to, string expected)
    {
        var year = DateTime.Today.Year;
        var parts = FilterSummary.Parts(new CustomFilter { DueFrom = from?.Replace("2026", year.ToString()), DueTo = to?.Replace("2026", year.ToString()) }, false);
        Assert.Equal([expected], parts);
    }

    [Fact]
    public void WaitingFilters_AreWorded_AndNotSaidTwice()
    {
        Assert.Equal(["Waiting on anything"], FilterSummary.Parts(new CustomFilter { Due = "Waiting On" }, false));
        Assert.Equal(["Waiting on anything"], FilterSummary.Parts(new CustomFilter { Due = "Waiting On", WaitingOn = "Any" }, false));
        Assert.Equal(["Not waiting"], FilterSummary.Parts(new CustomFilter { WaitingOn = "Unassigned" }, false));
    }

    [Fact]
    public void HideFutureAlone_StillCounts()
    {
        Assert.Equal("Future tasks hidden  ·  37 of 40 tasks", FilterSummary.Line(new CustomFilter(), true, null, 37, 40));
    }

    [Fact]
    public void TheLine_NamesASavedFilter_AndCountsWhatIsShowing()
    {
        var state = new CustomFilter { Due = "Today", Priority = ["High"] };

        Assert.Equal("Today  ·  Priority: High  ·  3 of 40 tasks", FilterSummary.Line(state, false, null, 3, 40));
        Assert.Equal("\"Hot ones\" (Alt+3): Today  ·  Priority: High  ·  1 of 1 task", FilterSummary.Line(state, false, "\"Hot ones\" (Alt+3)", 1, 1));
    }

    [Fact]
    public void TwoFilters_AreTheSame_WhateverOrderTheirListsAreIn()
    {
        var a = new CustomFilter { Name = "A", Project = ["Website", "Admin"], Keyword = "bank " };
        var b = new CustomFilter { Name = "B", Project = ["Admin", "Website"], Keyword = "bank" };

        Assert.True(FilterSummary.Same(a, b));
        Assert.False(FilterSummary.Same(a, new CustomFilter { Project = ["Admin"] }));
        Assert.False(FilterSummary.Same(a, new CustomFilter { Project = ["Website", "Admin"], Keyword = "bank", Due = "Today" }));
    }
}

[Collection(WpfCollection.Name)]
public sealed class FilterSummaryBoardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static CardViewModel Add(MainViewModel board, string title, string priority = "Normal", DateTime? due = null) =>
        board.AddCard(title, board.Columns.First(), board.Projects.First(), priority, due, null, false, null, null);

    [Fact]
    public void TheBanner_FollowsTheFilters_AndClearsWithThem() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "a", "High"); Add(board, "b"); Add(board, "c", "High", DateTime.Today);
        var changed = new List<string?>();
        board.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.False(board.HasFilterSummary);
        Assert.Equal("", board.FilterSummaryText);

        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();

        Assert.True(board.HasFilterSummary);
        Assert.Equal("Priority: High  ·  2 of 3 tasks", board.FilterSummaryText);
        Assert.Contains("• Priority: High", board.FilterSummaryToolTip);
        Assert.Contains(nameof(MainViewModel.FilterSummaryText), changed);

        board.KeywordFilter = "c";
        Assert.Equal("Priority: High  ·  Keyword: \"c\"  ·  1 of 3 tasks", board.FilterSummaryText);

        board.ClearFilters();
        Assert.False(board.HasFilterSummary);
    });

    [Fact]
    public void TheDayButtons_AndHideFuture_AreNamed() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "today", due: DateTime.Today); Add(board, "later", due: DateTime.Today.AddDays(40));

        board.ShowDueFilterOnly("Today");
        Assert.Equal("Today  ·  1 of 2 tasks", board.FilterSummaryText);

        board.ClearFilters();
        board.ToggleHideFutureTasks();
        Assert.Equal("Future tasks hidden  ·  2 of 2 tasks", board.FilterSummaryText);
        Assert.Contains("Show Future", board.FilterSummaryToolTip);
    });

    [Fact]
    public void ASavedFilterTheBoardMatches_IsNamedWithItsKey() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "a", "High"); Add(board, "b");
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();
        board.CaptureCustomFilter(3, "Hot ones");

        Assert.Equal("\"Hot ones\" (Alt+3): Priority: High  ·  1 of 2 tasks", board.FilterSummaryText);

        board.KeywordFilter = "zzz"; // no longer the saved filter
        Assert.StartsWith("Priority: High", board.FilterSummaryText);

        board.ClearFilters();
        Assert.True(board.ApplyCustomFilter(3));
        Assert.StartsWith("\"Hot ones\" (Alt+3): ", board.FilterSummaryText);
    });
}
