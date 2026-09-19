using ClosedXML.Excel;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The optional start ("not before") date, and Hide Future, which keeps tasks off the board until
// their start date arrives.
[Collection(WpfCollection.Name)]
public sealed class StartDateTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();
    private static readonly DateTime Tomorrow = DateTime.Today.AddDays(1);

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, DateTime? start, DateTime? due = null, string column = "To Do",
        bool recurring = false) =>
        board.AddCard(title, Column(board, column), board.Projects.First(), "Normal", due, null, recurring, recurring ? "Weekly" : null, null,
            startDate: start);

    [Fact]
    public void StartDate_IsSaved_OnAddAndOnEdit() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Plan trip", new DateTime(2026, 10, 3), new DateTime(2026, 10, 20));
        Assert.Equal(new DateTime(2026, 10, 3), Assert.Single(Column(OpenBoard(), "To Do").Cards).StartDate);

        board.EditCard(card, card.Title, Column(board, "To Do"), board.Projects.First(), "Normal", card.DueDate, null, false, null, null,
            card.Flags, card.SubTasks, null, card.Attachments, false, null, null, new DateTime(2026, 10, 10));
        Assert.Equal(new DateTime(2026, 10, 10), Assert.Single(Column(OpenBoard(), "To Do").Cards).StartDate);

        board.EditCard(card, card.Title, Column(board, "To Do"), board.Projects.First(), "Normal", card.DueDate, null, false, null, null,
            card.Flags, card.SubTasks, null, card.Attachments, false, null, null, null);
        Assert.Null(Assert.Single(Column(OpenBoard(), "To Do").Cards).StartDate);
    });

    [Fact]
    public void TheCardSaysWhenItStarts_OnlyWhileThatIsStillAhead() => wpf.Run(() =>
    {
        var board = OpenBoard();

        var future = Add(board, "Future", Tomorrow);
        Assert.True(future.IsNotStarted);
        Assert.Equal($"Starts {Tomorrow:MMM d, yyyy}", future.StartDateDisplay);

        foreach (var started in new[] { Add(board, "Today", DateTime.Today), Add(board, "Past", DateTime.Today.AddDays(-5)), Add(board, "None", null) })
        {
            Assert.False(started.IsNotStarted);
            Assert.Equal(string.Empty, started.StartDateDisplay);
        }
    });

    [Fact]
    public void HideFuture_HidesOnlyTasksThatHaveNotStarted_AndCountsThem() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var future = Add(board, "Future", Tomorrow);
        var today = Add(board, "Today", DateTime.Today);
        var none = Add(board, "None", null);
        var finishedEarly = Add(board, "Finished early", Tomorrow, column: "Done");
        Assert.Equal("Hide Future", board.HideFutureButtonLabel);

        board.ToggleHideFutureTasks();

        Assert.False(future.IsVisible);
        Assert.True(today.IsVisible);
        Assert.True(none.IsVisible);
        Assert.True(finishedEarly.IsVisible); // done is done, however early
        Assert.Equal("Show Future (1)", board.HideFutureButtonLabel);

        board.ToggleHideFutureTasks();
        Assert.True(future.IsVisible);
    });

    [Fact]
    public void HideFuture_IsRemembered_AndClearFiltersLeavesItAlone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "Future", Tomorrow);
        board.ToggleHideFutureTasks();

        board.ClearFilters();
        Assert.True(board.HideFutureTasks);
        Assert.False(Assert.Single(Column(board, "To Do").Cards).IsVisible);

        var reopened = OpenBoard();
        Assert.True(reopened.HideFutureTasks);
        Assert.False(Assert.Single(Column(reopened, "To Do").Cards).IsVisible);
    });

    [Fact]
    public void ATaskGivenAFutureStartWhileHiding_DropsOffTheBoard_AndANewOneNeverShows() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.ToggleHideFutureTasks();
        var card = Add(board, "Now", null);
        Assert.True(card.IsVisible);

        board.EditCard(card, card.Title, Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null,
            card.Flags, card.SubTasks, null, card.Attachments, false, null, null, Tomorrow);
        Assert.False(card.IsVisible);
        Assert.False(Add(board, "Later", Tomorrow).IsVisible);
        Assert.Equal("Show Future (2)", board.HideFutureButtonLabel);
    });

    [Fact]
    public void RecurringTask_NextOccurrenceKeepsTheSameLeadTime() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var weekly = Add(board, "Weekly", new DateTime(2026, 9, 18), new DateTime(2026, 9, 21), recurring: true);

        board.MoveCardCommand.Execute((weekly, Column(board, "Done")));

        var next = Assert.Single(Column(board, "To Do").Cards);
        Assert.Equal(new DateTime(2026, 9, 28), next.DueDate);
        Assert.Equal(new DateTime(2026, 9, 25), next.StartDate);
    });

    [Fact]
    public void DuplicateKeepsTheStartDate_AndAnEarlierQuickDueDatePullsTheStartBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "A", new DateTime(2026, 10, 10), new DateTime(2026, 10, 20));

        Assert.Equal(card.StartDate, board.DuplicateCard(card)!.StartDate);

        board.SetCardDueDate(card, new DateTime(2026, 10, 5));
        Assert.Equal(new DateTime(2026, 10, 5), card.StartDate);
        board.SetCardDueDate(card, null);
        Assert.Equal(new DateTime(2026, 10, 5), card.StartDate); // no due date, nothing to clash with
    });

    [Fact]
    public void CopyAsText_IncludesTheStartDate() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "A", new DateTime(2026, 10, 3), new DateTime(2026, 10, 20));

        var text = CardTextFormatter.Format(card, "To Do");

        Assert.Contains("Start: 03-Oct-2026\r\nDue: 20-Oct-2026", text);
        Assert.DoesNotContain("Start:", CardTextFormatter.Format(Add(board, "B", null), "To Do"));
    });

    [Fact]
    public void Import_ReadsAStartColumn_AndAStartAfterTheDueDateIsPulledBack() => wpf.Run(() =>
    {
        var path = _temp.File("start.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Task";
            sheet.Cell(1, 2).Value = "Start";
            sheet.Cell(1, 3).Value = "Due";
            sheet.Cell(2, 1).Value = "Sensible";
            sheet.Cell(2, 2).Value = new DateTime(2026, 10, 3);
            sheet.Cell(2, 3).Value = new DateTime(2026, 10, 20);
            sheet.Cell(3, 1).Value = "Backwards";
            sheet.Cell(3, 2).Value = "11/15/2026";
            sheet.Cell(3, 3).Value = new DateTime(2026, 11, 1);
            workbook.SaveAs(path);
        }

        var rows = ImportService.ReadTasks(path);
        Assert.Equal(new DateTime(2026, 10, 3), rows[0].StartDate);
        Assert.Equal(new DateTime(2026, 11, 15), rows[1].StartDate);

        var board = OpenBoard();
        var cards = board.ImportCards(rows);
        Assert.Equal(new DateTime(2026, 10, 3), cards[0].StartDate);
        Assert.Equal(new DateTime(2026, 11, 1), cards[1].StartDate);
    });

    [Fact]
    public void TheImportTemplate_HasAStartDateColumn() => wpf.Run(() =>
    {
        var path = _temp.File("template.xlsx");
        ImportService.SaveTemplate(path, ["To Do"], ["General"], [], []);

        using var workbook = new XLWorkbook(path);
        var headings = workbook.Worksheet(1).Row(2).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal("Start Date", headings.Last());
        Assert.Equal("Who", headings[^2]); // existing columns stay where they were
    });
}
