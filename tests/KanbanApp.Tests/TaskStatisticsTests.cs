using System.Text.Json;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The statistics report: its periods, its figures from a small set of tasks worked out by hand,
// reading the history, saved views of both kinds, and the standard reports every task file gets.
[Collection(WpfCollection.Name)]
public sealed class TaskStatisticsTests(WpfDispatcherFixture wpf) : IDisposable
{
    private static readonly DateTime Today = new(2026, 10, 8); // a Thursday
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(StatisticsPeriods.Last7Days, "2026-10-02", "2026-10-08")]
    [InlineData(StatisticsPeriods.Last30Days, "2026-09-09", "2026-10-08")]
    [InlineData(StatisticsPeriods.ThisMonth, "2026-10-01", "2026-10-08")]
    [InlineData(StatisticsPeriods.LastMonth, "2026-09-01", "2026-09-30")]
    [InlineData(StatisticsPeriods.ThisQuarter, "2026-10-01", "2026-10-08")]
    [InlineData(StatisticsPeriods.LastQuarter, "2026-07-01", "2026-09-30")]
    [InlineData(StatisticsPeriods.ThisYear, "2026-01-01", "2026-10-08")]
    [InlineData(StatisticsPeriods.LastYear, "2025-01-01", "2025-12-31")]
    [InlineData("nonsense", "2026-09-09", "2026-10-08")] // an unknown key reads as the last 30 days
    public void EachPeriod_CoversTheRightDays(string key, string from, string to) =>
        Assert.Equal((DateTime.Parse(from), DateTime.Parse(to)), StatisticsPeriods.Resolve(key, Today));

    [Fact]
    public void AllTime_StartsAtTheFirstTask_AndCustomDatesAreTakenEitherWayRound()
    {
        Assert.Equal((new DateTime(2026, 8, 16), Today), StatisticsPeriods.Resolve(StatisticsPeriods.AllTime, Today, earliest: new DateTime(2026, 8, 16, 6, 6, 52)));
        Assert.Equal((Today, Today), StatisticsPeriods.Resolve(StatisticsPeriods.AllTime, Today)); // no tasks yet
        Assert.Equal((new DateTime(2026, 9, 1), new DateTime(2026, 9, 15)),
            StatisticsPeriods.Resolve(StatisticsPeriods.Custom, Today, new DateTime(2026, 9, 15), new DateTime(2026, 9, 1)));
    }

    [Theory]
    [InlineData("Created", "Added to To Do", "To Do")]
    [InlineData("Moved", "Moved from To Do to In Progress", "In Progress")]
    [InlineData("Moved", "Moved from In Progress to To Do", "To Do")]
    [InlineData("Moved", "Moved from Waiting to Done", "Done")]
    [InlineData("Edited", "Task details updated", null)]
    [InlineData("Moved", "something else", null)]
    public void TheHistory_SaysWhichColumnATaskEntered(string eventType, string details, string? column) =>
        Assert.Equal(column, TaskEvent.ColumnEntered(eventType, details));

    private static DateTime D(int month, int day, int hour = 9) => new(2026, month, day, hour, 0, 0);

    private static ReportRow Row(int id, string project, DateTime? completed, DateTime? due, string column, bool archived = false, params string[] people) => new()
    {
        CardId = id, Title = $"Task {id}", ColumnName = column, ColumnKey = archived ? null : column, ProjectName = project,
        Priority = "Normal", PriorityRank = 2, GoalName = "No Goal", CompletedAt = completed, DueDate = due, IsArchived = archived, People = [.. people]
    };

    // Period Sep 9 - Oct 8:
    //   1  added Sep 10, To Do 2 days, In Progress 3 days, done Sep 15 (due Sep 16: on time), 5 days, archived
    //   2  added Sep 1 (before the period), To Do 4 days, Waiting 15 days, done Sep 20 (due Sep 18: late), 19 days
    //   3  added Oct 1, still in To Do, due Oct 5: overdue now
    //   4  added Oct 2, in Waiting, no due date
    private static TaskStatisticsResult Example(string breakdown = "Project", string overTime = "Week")
    {
        var rows = new List<ReportRow>
        {
            Row(1, "Acme", D(9, 15), new DateTime(2026, 9, 16), "Done", archived: true, "Sam", "Rose"),
            Row(2, "Acme", D(9, 20), new DateTime(2026, 9, 18), "Done", false, "Sam"),
            Row(3, "No Project", null, new DateTime(2026, 10, 5), "To Do"),
            Row(4, "No Project", null, null, "Waiting", false, "Rose")
        };
        var history = new Dictionary<int, List<TaskEvent>>
        {
            [1] = [new(D(9, 10), "To Do", true), new(D(9, 12), "In Progress", false), new(D(9, 15), "Done", false)],
            [2] = [new(D(9, 1), "To Do", true), new(D(9, 5), "Waiting", false), new(D(9, 20), "Done", false)],
            [3] = [new(D(10, 1), "To Do", true)],
            [4] = [new(D(10, 2), "To Do", true), new(D(10, 3), "Waiting", false)]
        };
        var columns = new List<(string, string)> { ("To Do", "To Do"), ("In Progress", "Doing"), ("On Hold", "On Hold"), ("Waiting", "Waiting"), ("Done", "Done") };
        return TaskStatistics.Compute(rows, history, columns, new StatisticsOptions(new DateTime(2026, 9, 9), Today, breakdown, overTime), Today);
    }

    [Fact]
    public void TheSummary_CountsWhatHappenedInThePeriod_AndWhatIsOpenNow()
    {
        var s = Example().Summary;

        Assert.Equal(3, s.Added);               // 1, 3 and 4; 2 was added before the period
        Assert.Equal(2, s.Finished);
        Assert.Equal((2, 1), (s.FinishedWithDueDate, s.FinishedOnTime));
        Assert.Equal(12, s.TypicalDaysToFinish); // the middle of 5 and 19
        Assert.Equal((2, 1), (s.OpenNow, s.OverdueNow));
        Assert.Equal("50%", TaskStatistics.OnTimeShare(s.FinishedOnTime, s.FinishedWithDueDate));
    }

    [Fact]
    public void TheBreakdown_HasARowEach_WithTheNoneRowLast()
    {
        var groups = Example().Groups;
        Assert.Equal(["Acme", "No Project"], groups.Select(g => g.Name));
        Assert.Equal((1, 2, 0), (groups[0].Added, groups[0].Finished, groups[0].OpenNow));
        Assert.Equal((2, 0, 2, 1), (groups[1].Added, groups[1].Finished, groups[1].OpenNow, groups[1].OverdueNow));

        // A shared task counts for each of its people; nobody assigned is its own row, last.
        var people = Example("Who").Groups;
        Assert.Equal(["Rose", "Sam", "Unassigned"], people.Select(g => g.Name));
        Assert.Equal(2, people.Single(g => g.Name == "Sam").Finished);
        Assert.Equal((1, 1), (people.Single(g => g.Name == "Rose").Finished, people.Single(g => g.Name == "Rose").OpenNow));
    }

    [Fact]
    public void OverTime_GoesWeekByWeek_FromMonday_OrMonthByMonth()
    {
        var weeks = Example().Buckets;
        Assert.Equal(new DateTime(2026, 9, 7), weeks[0].Start); // the Monday of the week the period starts in
        Assert.Equal(new DateTime(2026, 10, 5), weeks[^1].Start);
        Assert.Equal((1, 0), (weeks[0].Added, weeks[0].Finished));                                    // 1 added Sep 10
        Assert.Equal((0, 2), (weeks.Single(w => w.Start == new DateTime(2026, 9, 14)).Added, weeks.Single(w => w.Start == new DateTime(2026, 9, 14)).Finished));
        var weekOfSep28 = weeks.Single(w => w.Start == new DateTime(2026, 9, 28));
        Assert.Equal((2, 0), (weekOfSep28.Added, weekOfSep28.Finished)); // 3 and 4, added Oct 1 and 2
        Assert.Equal((0, 0), (weeks[^1].Added, weeks[^1].Finished));
        Assert.Equal(5, weeks.Count);

        var months = Example(overTime: "Month").Buckets;
        Assert.Equal(["September 2026", "October 2026"], months.Select(m => m.Label));
        Assert.Equal((1, 2), (months[0].Added, months[0].Finished)); // 2 was added Sep 1, before the period started
        Assert.Equal((2, 0), (months[1].Added, months[1].Finished));
    }

    [Fact]
    public void TimeInEachColumn_IsForFinishedTasks_ByTheColumnsDisplayName_InBoardOrder()
    {
        var times = Example().ColumnTimes;
        Assert.Equal(["To Do", "Doing", "Waiting"], times.Select(t => t.Column)); // never Done; On Hold had nobody
        Assert.Equal((2, 3.0, 4.0), (times[0].Tasks, times[0].TypicalDays, times[0].LongestDays));
        Assert.Equal((1, 3.0), (times[1].Tasks, times[1].TypicalDays));
        Assert.Equal((1, 15.0), (times[2].Tasks, times[2].TypicalDays));
    }

    [Fact]
    public void HowLongTasksTook_IsCountedInBands_AllListed()
    {
        var bands = Example().FinishTimes;
        Assert.Equal(["Same day", "1-2 days", "3-7 days", "1-2 weeks", "2-4 weeks", "Over a month"], bands.Select(b => b.Label));
        Assert.Equal([0, 0, 1, 0, 1, 0], bands.Select(b => b.Tasks)); // 5 days and 19 days
    }

    [Fact]
    public void Charts_CanBeLeftOut_AndOlderViewsGetThem() => wpf.Run(() =>
    {
        var with = ReportService.BuildStatisticsDocument("S", Example(), new(true, true, true, true, Charts: true), "Project", "Week", false, null);
        var without = ReportService.BuildStatisticsDocument("S", Example(), new(true, true, true, true, Charts: false), "Project", "Week", false, null);
        Assert.True(with.Pages.Count >= without.Pages.Count);

        static int Shapes(System.Windows.Documents.FixedDocument d) =>
            d.Pages.Sum(p => ((System.Windows.Controls.Canvas)p.Child.Children[0]).Children.Count);
        Assert.True(Shapes(with) > Shapes(without) + 20);

        Assert.True(JsonSerializer.Deserialize<SavedReportView>("""{"Name":"Old","ReportType":"Statistics"}""")!.StatsShowCharts);
    });

    [Fact]
    public void Days_ReadPlainly() =>
        Assert.Equal(["under 1", "3", "12.5", "-"], new double?[] { 0.4, 3, 12.48, null }.Select(TaskStatistics.Days));

    [Fact]
    public void AViewSavedBeforeStatistics_ReadsAsATaskList()
    {
        var old = JsonSerializer.Deserialize<SavedReportView>("""{"Name":"Old","Title":"Kanban Task Report","GroupBy":"Who"}""")!;
        Assert.False(old.IsStatistics);
        Assert.Equal((StatisticsPeriods.Last30Days, "Project", "Week"), (old.StatsPeriod, old.StatsBreakdown, old.StatsOverTime));

        var saved = JsonSerializer.Serialize(new SavedReportView { Name = "S", ReportType = SavedReportView.StatisticsType });
        Assert.True(JsonSerializer.Deserialize<SavedReportView>(saved)!.IsStatistics);
        Assert.DoesNotContain("IsStatistics", saved);
    }

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void EveryTaskFile_GetsTheStandardReports_Once() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Equal(7, board.SavedReportViews.Count);
        Assert.Equal(3, board.SavedReportViews.Count(v => v.IsStatistics));
        Assert.Contains(board.SavedReportViews, v => v.Name == "Overdue and Due Today" && v.Due == "Today" && !v.IncludedColumns.Contains("Done"));

        board.DeleteReportView("Due Within a Week");
        Assert.DoesNotContain(OpenBoard().SavedReportViews, v => v.Name == "Due Within a Week"); // not put back
    });

    [Fact]
    public void AViewOfTheSameName_IsKept() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("old.db"));
        db.SetSetting("SavedReportViews", JsonSerializer.Serialize(new List<SavedReportView> { new() { Name = "Open Tasks by Person", Title = "Mine" } }));

        var board = new MainViewModel(db);
        Assert.Equal("Mine", board.SavedReportViews.Single(v => v.Name == "Open Tasks by Person").Title);
        Assert.Equal(7, board.SavedReportViews.Count);
    });

    // End to end on a real task file: moves recorded by the board, read back, counted.
    [Fact]
    public void AStatisticsView_RunsOnTheBoardsOwnHistory() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = board.Columns.Single(c => c.Name == "To Do");
        var done = board.Columns.Single(c => c.Name == "Done");
        var card = board.AddCard("Ship it", todo, board.Projects.First(), "High", DateTime.Today.AddDays(3), null, false, null, null);
        board.AddCard("Still open", todo, board.Projects.First(), "Normal", DateTime.Today.AddDays(-1), null, false, null, null);
        board.MoveCardCommand.Execute((card, done));

        var view = board.SavedReportViews.Single(v => v.Name == "Statistics: Last 30 Days");
        var result = ReportRunner.BuildStatistics(board, view, DateTime.Today);

        Assert.Equal((2, 1, 1, 1, 1), (result.Summary.Added, result.Summary.Finished, result.Summary.FinishedOnTime, result.Summary.OpenNow, result.Summary.OverdueNow));
        Assert.Empty(result.ColumnTimes); // added and finished within the same second here: no time in any column

        var document = ReportRunner.BuildDocument(board, view, DateTime.Today);
        Assert.True(document.Pages.Count >= 1);
        Assert.StartsWith("Statistics   |   Period: Last 30 days", ReportRunner.Summarise(view, DateTime.Today));
    });
}
