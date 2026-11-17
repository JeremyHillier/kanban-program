using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The task list's sub-task summary on its own (Summary only), and the statistics report's Sub-tasks section.
[Collection(WpfCollection.Name)]
public sealed class SubTaskReportTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static ReportRow Row(int id, string title, DateTime? completed, params (string Title, bool IsDone)[] subTasks) => new()
    {
        CardId = id, Title = title, ColumnName = "Done", ProjectName = "Home", Priority = "Normal", PriorityRank = 2, GoalName = "No Goal",
        CompletedAt = completed, IsArchived = completed is not null, SubTasks = [.. subTasks]
    };

    private static List<ReportRow> Weekly() =>
    [
        Row(1, "Weekly review", new DateTime(2026, 11, 2), ("Inbox zero", true), ("Plan week", true)),
        Row(2, "Weekly review", new DateTime(2026, 11, 9), ("Inbox zero", false), ("Plan week", true)),
        Row(3, "Pay bills", new DateTime(2026, 11, 10))
    ];

    private static List<string> Texts(FixedDocument document) => document.Pages.SelectMany(p => TextsIn(p.Child)).ToList();

    // Every text on the page, wherever it sits in the page's tree.
    private static IEnumerable<string> TextsIn(DependencyObject element) =>
        element is TextBlock text ? [text.Text] : LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>().SelectMany(TextsIn);

    [Fact]
    public void SummaryOnly_LeavesOutTheTasks_AndKeepsTheSummary() => wpf.Run(() =>
    {
        var full = Texts(ReportService.BuildFixedDocument("Report", Weekly(), "None", true, true, includeSubTaskSummary: true));
        var only = Texts(ReportService.BuildFixedDocument("Report", Weekly(), "None", true, true, includeSubTaskSummary: true, subTaskSummaryOnly: true));

        Assert.Contains("Pay bills", full);
        Assert.DoesNotContain("Pay bills", only);
        foreach (var texts in new[] { full, only })
        {
            Assert.Contains("Sub-task Completion Summary", texts);
            Assert.Contains("Inbox zero: 1/2 completed (50%)", texts);
            Assert.Contains("Plan week: 2/2 completed (100%)", texts);
        }
    });

    [Fact]
    public void SummaryOnly_WithNoSubTasks_SaysSo() => wpf.Run(() =>
    {
        var texts = Texts(ReportService.BuildFixedDocument("Report", [Row(3, "Pay bills", null)], "None", true, true, subTaskSummaryOnly: true));

        Assert.Contains(ReportService.NoSubTasksText, texts);
        Assert.DoesNotContain("Pay bills", texts);
    });

    [Fact]
    public void SummaryOnly_AsAPdf_IsShorterThanTheFullReport() => wpf.Run(() =>
    {
        var full = _temp.File("full.pdf");
        var only = _temp.File("only.pdf");
        ReportService.SavePdf("Report", Weekly(), "None", true, true, full, includeSubTaskSummary: true);
        ReportService.SavePdf("Report", Weekly(), "None", true, true, only, includeSubTaskSummary: true, subTaskSummaryOnly: true);

        Assert.True(new FileInfo(only).Length > 0);
        Assert.True(new FileInfo(only).Length < new FileInfo(full).Length);
    });

    [Fact]
    public void Statistics_CountEachSubTask_OnTheTasksFinishedInThePeriod() => wpf.Run(() =>
    {
        var rows = Weekly();
        rows.Add(Row(4, "Weekly review", new DateTime(2026, 10, 1), ("Inbox zero", true))); // finished before the period
        rows.Add(Row(5, "Weekly review", null, ("Inbox zero", true)));                       // not finished

        var result = TaskStatistics.Compute(rows, new Dictionary<int, List<TaskEvent>>(), [("Done", "Done")],
            new StatisticsOptions(new DateTime(2026, 11, 1), new DateTime(2026, 11, 15), "None", "Week"), new DateTime(2026, 11, 15));

        Assert.Equal(
            [new StatisticsSubTask("Weekly review", "Inbox zero", 1, 2), new StatisticsSubTask("Weekly review", "Plan week", 2, 2)],
            result.SubTasks);
    });

    [Fact]
    public void TheStatisticsReport_ShowsTheSubTasksSection_OnlyWhenTicked() => wpf.Run(() =>
    {
        var result = TaskStatistics.Compute(Weekly(), new Dictionary<int, List<TaskEvent>>(), [("Done", "Done")],
            new StatisticsOptions(new DateTime(2026, 11, 1), new DateTime(2026, 11, 15), "None", "Week"), new DateTime(2026, 11, 15));

        foreach (var charts in new[] { false, true })
        {
            var with = Texts(ReportService.BuildStatisticsDocument("Stats", result,
                new ReportService.SavedStatisticsSections(true, false, false, false, charts, SubTasks: true), "None", "Week", false, null));
            var without = Texts(ReportService.BuildStatisticsDocument("Stats", result,
                new ReportService.SavedStatisticsSections(true, false, false, false, charts), "None", "Week", false, null));

            Assert.Contains("Sub-tasks", with);
            Assert.Contains("Inbox zero", with);
            Assert.Contains("3 of 4 sub-tasks on the finished tasks were ticked.", with);
            Assert.DoesNotContain("Sub-tasks", without);
            Assert.DoesNotContain("Inbox zero", without);
        }
    });

    [Fact]
    public void AViewSavedBefore_HasNeither_AndTheParametersLineNamesSummaryOnly()
    {
        var old = JsonSerializer.Deserialize<SavedReportView>("""{"Name":"Old","IncludeSubTaskSummary":true}""")!;
        Assert.False(old.SubTaskSummaryOnly);
        Assert.False(old.StatsShowSubTasks);
        Assert.DoesNotContain("Sub-task summary only", ReportRunner.Summarise(old, DateTime.Today));

        var view = new SavedReportView { IncludeSubTaskSummary = true, SubTaskSummaryOnly = true };
        Assert.Contains("Sub-task summary only", ReportRunner.Summarise(view, DateTime.Today));

        view.IncludeSubTaskSummary = false; // summary only needs the summary itself
        Assert.DoesNotContain("Sub-task summary only", ReportRunner.Summarise(view, DateTime.Today));
    }
}
