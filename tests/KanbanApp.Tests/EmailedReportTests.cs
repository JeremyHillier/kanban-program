using System.Text.Json;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Reporting on emailed tasks, for follow-up: the Emailed line under a task, and the Emailed tasks only filter.
[Collection(WpfCollection.Name)]
public sealed class EmailedReportTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private (MainViewModel Board, CardViewModel Sent, CardViewModel Kept) BoardWithOneEmailedTask()
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var toDo = board.Columns.Single(c => c.Name == "To Do");
        var sent = board.AddCard("Sent one", toDo, board.Projects.First(), "Normal", null, null, false, null, null);
        var kept = board.AddCard("Kept one", toDo, board.Projects.First(), "Normal", null, null, false, null, null);
        board.RecordCardEmailed(sent, "sam@example.com", "in Outlook");
        board.RecordCardEmailed(sent, "sam@example.com", "in Outlook");
        return (board, sent, kept);
    }

    private static List<ReportRow> Rows(MainViewModel board, bool emailedOnly) => ReportService.BuildRows(
        board.Columns, board.Columns.Select(c => c.Name).ToHashSet(),
        [], [], [], "All", "All", "All", null, null, false, null, "None", "None", "None",
        emailHistory: board.GetEmailHistory, emailedOnly: emailedOnly);

    [Fact]
    public void AnEmailedTask_GetsAnEmailedLine_AndEmailedOnlyKeepsJustThose() => wpf.Run(() =>
    {
        var (board, _, _) = BoardWithOneEmailedTask();

        var all = Rows(board, emailedOnly: false);
        Assert.Equal(2, all.Count);
        var emailed = all.Single(r => r.Title == "Sent one").Emailed;
        Assert.NotNull(emailed);
        Assert.Contains("sam@example.com", emailed);
        Assert.Contains("(2 times)", emailed);
        Assert.DoesNotContain("in Outlook", emailed); // the "how" is left to the task screen's tooltip
        Assert.Null(all.Single(r => r.Title == "Kept one").Emailed);

        Assert.Equal(["Sent one"], Rows(board, emailedOnly: true).Select(r => r.Title));
    });

    [Fact]
    public void WithoutTheLookup_NothingIsEmailed_AndEmailedOnlyFindsNothing() => wpf.Run(() =>
    {
        var (board, _, _) = BoardWithOneEmailedTask();

        var rows = ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(),
            [], [], [], "All", "All", "All", null, null, false, null, "None", "None", "None");
        Assert.All(rows, r => Assert.Null(r.Emailed));
    });

    [Fact]
    public void ASavedView_KeepsBothOptions_AndQuickReportHonoursThem() => wpf.Run(() =>
    {
        var (board, _, _) = BoardWithOneEmailedTask();
        var view = new SavedReportView { IncludedColumns = board.Columns.Select(c => c.Name).ToList(), IncludeEmailed = true, EmailedOnly = true };

        var back = JsonSerializer.Deserialize<SavedReportView>(JsonSerializer.Serialize(view))!;
        Assert.True(back.IncludeEmailed);
        Assert.True(back.EmailedOnly);
        Assert.Equal(["Sent one"], ReportRunner.BuildRows(board, back, DateTime.Today).Select(r => r.Title));

        // A view saved before the options existed has neither.
        var old = JsonSerializer.Deserialize<SavedReportView>("{\"Name\":\"Old\"}")!;
        Assert.False(old.IncludeEmailed);
        Assert.False(old.EmailedOnly);
    });
}
