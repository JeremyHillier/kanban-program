using System.IO;
using System.Windows;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Short reports, grouping by when tasks are due, the This Week due choice, and the standard Today and This Week report.
public sealed class ReportDueGroupingTests
{
    private static ReportRow Row(string title, DateTime? due, string bucket = "", string priority = "Normal") => new()
    {
        Title = title, ColumnName = "To Do", ProjectName = "General", Priority = priority, GoalName = "No Goal", DueDate = due, DueBucket = bucket
    };

    [Fact]
    public void TheEndOfTheWeek_FollowsTheDayTheWeekStartsOn()
    {
        var wednesday = new DateTime(2026, 11, 25);
        var sunday = new DateTime(2026, 11, 29);

        Assert.Equal(sunday, ReportService.EndOfWeek(wednesday, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 11, 28), ReportService.EndOfWeek(wednesday, DayOfWeek.Sunday));
        Assert.Equal(sunday, ReportService.EndOfWeek(sunday, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 12, 5), ReportService.EndOfWeek(sunday, DayOfWeek.Sunday));
    }

    [Fact]
    public void ADueDate_FallsInOverdueTodayThisWeekLaterOrNone()
    {
        var today = new DateTime(2026, 11, 25);
        string Of(DateTime? due) => ReportService.DueBucketOf(due, today, DayOfWeek.Monday);

        Assert.Equal("Overdue", Of(today.AddDays(-1)));
        Assert.Equal("Today", Of(today));
        Assert.Equal("This Week", Of(new DateTime(2026, 11, 29)));
        Assert.Equal("Later", Of(new DateTime(2026, 11, 30)));
        Assert.Equal("No Due Date", Of(null));
    }

    [Fact]
    public void GroupingByDueDate_ListsTheGroupsInDateOrder()
    {
        var rows = new List<ReportRow> { Row("a", null, "No Due Date"), Row("b", null, "This Week"), Row("c", null, "Overdue"), Row("d", null, "Today") };

        Assert.Equal(["Overdue", "Today", "This Week", "No Due Date"], ReportService.GroupRows(rows, "Due").Select(g => g.Key));
    }

    [Fact]
    public void AShortRow_SaysJustThePriorityAndWhenItIsDue()
    {
        var row = Row("Cancel Disney?", new DateTime(2026, 12, 11), priority: "High");
        row.Brief = true;
        Assert.Equal(["Priority: High", "Due Dec 11, 2026"], ReportService.BuildMetaParts(row));

        var undated = Row("Call the bank", null);
        undated.Brief = true;
        Assert.Equal(["Priority: Normal", "No due date"], ReportService.BuildMetaParts(undated));

        Assert.Contains("Status: To Do", ReportService.BuildMetaParts(Row("Long", null)));   // the full form as before
    }

    [Fact]
    public void ThisWeek_IsAReportChoice_AfterWithinAWeek() =>
        Assert.Equal(["All", "Today", "Tomorrow", "Within a Week", "This Week", "No Due Date"],
            ReportService.ReportDueOptions(["All", "Today", "Tomorrow", "Within a Week", "No Due Date"]));

    [Fact]
    public void TheHeading_SaysShortAndNamesTheGrouping()
    {
        var view = new SavedReportView { GroupBy = "Due", Brief = true, Due = "This Week" };
        var summary = ReportRunner.Summarise(view, new DateTime(2026, 11, 25));

        Assert.Contains("Grouped by: Due date", summary);
        Assert.Contains(ReportRunner.ShortLabel, summary);
        Assert.Contains("Due: This Week", summary);
    }

    [Fact]
    public void TheReportBuilder_CanBeResized() =>
        Assert.Contains("ResizeMode=\"CanResizeWithGrip\"", File.ReadAllText(SourcePath("Views", "ReportBuilderWindow.xaml")));

    internal static string SourcePath(params string[] path)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return Path.Combine([dir!.FullName, "src", "KanbanApp", .. path]);
    }
}

[Collection(WpfCollection.Name)]
public sealed class TodayAndThisWeekReportTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    // The tests' application has none of App.xaml's styles, which a window needs (as SimplifiedTaskWindowTests does).
    private static void EnsureAppResources()
    {
        if (Application.Current.Resources.Contains("HoverTextBoxStyle")) return;

        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, typeof(MainViewModel).Assembly);

        var source = File.ReadAllText(ReportDueGroupingTests.SourcePath("App.xaml"));
        var from = source.IndexOf("<Application.Resources>", StringComparison.Ordinal) + "<Application.Resources>".Length;
        var inside = source[from..source.IndexOf("</Application.Resources>", StringComparison.Ordinal)];
        var assembly = typeof(MainViewModel).Assembly.GetName().Name;
        var text = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" "
            + $"xmlns:local=\"clr-namespace:KanbanApp;assembly={assembly}\" xmlns:views=\"clr-namespace:KanbanApp.Views;assembly={assembly}\">{inside}</ResourceDictionary>";
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(text));
    }

    [Fact]
    public void ANewTaskFile_HasTheShortReport_ReadyToRun() => wpf.Run(() =>
    {
        var view = OpenBoard().SavedReportViews.Single(v => v.Name == MainViewModel.TodayAndThisWeekReport);

        Assert.True(view.Brief);
        Assert.Equal("This Week", view.Due);
        Assert.Equal("Due", view.GroupBy);
        Assert.DoesNotContain("Done", view.IncludedColumns);
        Assert.False(view.IncludeNotes);
    });

    [Fact]
    public void AnOlderTaskFile_GetsItOnce_AndADeletedOneStaysDeleted() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        db.SetFlag("StandardReportViewsAdded", true);              // a file from before the report existed
        var board = new MainViewModel(db);
        Assert.Equal([MainViewModel.TodayAndThisWeekReport], board.SavedReportViews.Select(v => v.Name));

        board.DeleteReportView(MainViewModel.TodayAndThisWeekReport);
        Assert.Empty(OpenBoard().SavedReportViews);
    });

    [Fact]
    public void TheReport_TakesWhatIsDueByTheEndOfTheWeek_InShortForm() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var today = DateTime.Today;
        var endOfWeek = ReportService.EndOfWeek(today, board.FirstDayOfWeek);
        void Add(string title, DateTime? due) => board.AddCard(title, board.ToDoColumn!, null, "Normal", due, null, false, null, null);
        Add("Late", today.AddDays(-1));
        Add("Now", today);
        Add("End of the week", endOfWeek);
        Add("Next week", endOfWeek.AddDays(1));
        Add("Some day", null);

        var view = board.SavedReportViews.Single(v => v.Name == MainViewModel.TodayAndThisWeekReport);
        var rows = ReportRunner.BuildRows(board, view, today);

        Assert.Equal(["Late", "Now", "End of the week"], rows.Select(r => r.Title));
        Assert.All(rows, r => Assert.True(r.Brief));
        Assert.Equal("Overdue", rows[0].DueBucket);
    });

    [Fact]
    public void QuickReport_OpensOnIt_UntilAnotherViewHasBeenRun() => wpf.Run(() =>
    {
        EnsureAppResources();
        var board = OpenBoard();

        var window = new QuickReportWindow(board);
        Assert.Equal(MainViewModel.TodayAndThisWeekReport, ((SavedReportView)window.ViewComboBox.SelectedItem).Name);

        board.SetQuickReportLastView("Open Tasks by Person");
        Assert.Equal("Open Tasks by Person", ((SavedReportView)new QuickReportWindow(board).ViewComboBox.SelectedItem).Name);
    });
}
