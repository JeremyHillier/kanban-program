using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Relative dates in saved report views ("today", "today+7"), and running a saved view straight
// from its settings as Quick Report does.
[Collection(WpfCollection.Name)]
public sealed class QuickReportTests(WpfDispatcherFixture wpf) : IDisposable
{
    private static readonly DateTime Today = new(2026, 9, 21);
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Theory]
    [InlineData("today", 0)]
    [InlineData("TODAY", 0)]
    [InlineData(" today ", 0)]
    [InlineData("today+7", 7)]
    [InlineData("today + 7", 7)]
    [InlineData("today-30", -30)]
    [InlineData("today+1", 1)]
    public void RelativeDates_ResolveAgainstTheDayTheReportRuns(string saved, int days)
    {
        Assert.Equal(Today.AddDays(days), RelativeDate.Resolve(saved, Today));
        Assert.True(RelativeDate.IsRelative(saved));
    }

    [Fact]
    public void FixedDates_StayFixed_AndRubbishIsNothing()
    {
        Assert.Equal(new DateTime(2026, 12, 25), RelativeDate.Resolve("2026-12-25", Today));
        Assert.False(RelativeDate.IsRelative("2026-12-25"));
        Assert.Null(RelativeDate.Resolve(null, Today));
        Assert.Null(RelativeDate.Resolve("  ", Today));
        Assert.Null(RelativeDate.Resolve("tomorrow", Today));
        Assert.Null(RelativeDate.Resolve("today+abc", Today));
        Assert.Null(RelativeDate.Resolve("today+99999", Today));
    }

    [Fact]
    public void RelativeDates_AreWrittenAndDescribed_Plainly()
    {
        Assert.Equal("today", RelativeDate.Relative(0));
        Assert.Equal("today+7", RelativeDate.Relative(7));
        Assert.Equal("today-14", RelativeDate.Relative(-14));

        Assert.Equal("today", RelativeDate.Describe("today"));
        Assert.Equal("today + 7 days", RelativeDate.Describe("today+7"));
        Assert.Equal("today - 1 day", RelativeDate.Describe("today-1"));
        Assert.Equal("Dec 25, 2026", RelativeDate.Describe("2026-12-25"));
        Assert.Equal("any", RelativeDate.Describe(null));
    }

    private static CardViewModel Add(MainViewModel board, string title, DateTime? due, string column = "To Do") =>
        board.AddCard(title, board.Columns.Single(c => c.Name == column), board.Projects.First(), "Normal", due, null, false, null, null);

    [Fact]
    public void ASavedView_RunsFromItsSettings_WithTodayWorkedOutOnTheDay() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "Yesterday", Today.AddDays(-1));
        Add(board, "Today", Today);
        Add(board, "In three days", Today.AddDays(3));
        Add(board, "Next month", Today.AddDays(30));
        Add(board, "Done one", Today, "Done");
        var view = new SavedReportView
        {
            Name = "This week", Title = "This Week's Tasks",
            IncludedColumns = ["To Do", "In Progress"],
            DueFrom = "today", DueTo = "today+7",
            SortLevel1 = "Due Date"
        };

        var rows = ReportRunner.BuildRows(board, view, Today);
        Assert.Equal(["Today", "In three days"], rows.Select(r => r.Title));

        // The same view a month later picks up different tasks - nothing to edit.
        var later = ReportRunner.BuildRows(board, view, Today.AddDays(28));
        Assert.Equal(["Next month"], later.Select(r => r.Title));

        var summary = ReportRunner.Summarise(view, Today);
        Assert.Contains("Due date range: Sep 21, 2026 to Sep 28, 2026", summary);
        Assert.Contains("Sort order: Due Date", summary);
    });

    [Fact]
    public void ASavedView_UsesTheBoardsCustomFilters_ByName() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "High one", null);
        board.Columns.First().Cards[0].Priority = "High";
        Add(board, "Normal one", null);
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.CaptureCustomFilter(1, "Highs");
        board.ClearFilters();

        var view = new SavedReportView { Name = "v", IncludedColumns = ["To Do"], CustomFilterNames = ["Highs"] };

        Assert.Equal(["High one"], ReportRunner.BuildRows(board, view, Today).Select(r => r.Title));
        Assert.Contains("Custom filters: Highs", ReportRunner.Summarise(view, Today));
    });

    [Fact]
    public void QuickReport_RemembersTheLastViewRun() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Null(board.QuickReportLastView);
        board.SetQuickReportLastView("This week");
        Assert.Equal("This week", OpenBoard().QuickReportLastView);
    });

    [Fact]
    public void AViewSavedByAnOlderVersion_WithFixedDates_StillRuns() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "Christmas", new DateTime(2026, 12, 25));
        var view = new SavedReportView { Name = "v", IncludedColumns = ["To Do"], DueFrom = "2026-12-01", DueTo = "2026-12-31" };

        Assert.Equal(["Christmas"], ReportRunner.BuildRows(board, view, Today).Select(r => r.Title));
    });
}
