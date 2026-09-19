using ClosedXML.Excel;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// "Waiting On": who or what is holding a task up, and the button that lists every such task.
[Collection(WpfCollection.Name)]
public sealed class WaitingOnTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, string? waitingOn = null, string column = "To Do", bool recurring = false) =>
        board.AddCard(title, Column(board, column), board.Projects.First(), "Normal", recurring ? new DateTime(2026, 9, 21) : null, null,
            recurring, recurring ? "Weekly" : null, null, waitingOn: waitingOn);

    [Fact]
    public void WaitingOn_IsSaved_Trimmed_AndBlankMeansNotWaiting() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts", "  Sam's quote  ");
        var blank = Add(board, "Nothing", "   ");

        Assert.Equal("Sam's quote", card.WaitingOn);
        Assert.True(card.IsWaiting);
        Assert.Equal("Waiting on: Sam's quote", card.WaitingOnDisplay);
        Assert.False(blank.IsWaiting);
        Assert.Equal(string.Empty, blank.WaitingOnDisplay);

        var saved = Column(OpenBoard(), "To Do").Cards;
        Assert.Equal("Sam's quote", saved.Single(c => c.Title == "Order parts").WaitingOn);
        Assert.Null(saved.Single(c => c.Title == "Nothing").WaitingOn);
    });

    [Fact]
    public void SettingAndClearing_FromTheCard_IsSaved_AndCanBeUndone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts");

        board.SetCardWaitingOn(card, "the client's sign-off");
        Assert.Equal("the client's sign-off", Assert.Single(Column(OpenBoard(), "To Do").Cards).WaitingOn);

        board.SetCardWaitingOn(card, "");
        Assert.False(card.IsWaiting);
        Assert.Null(Assert.Single(Column(OpenBoard(), "To Do").Cards).WaitingOn);

        Assert.Equal("Clear waiting-on of \"Order parts\"", board.Undo());
        Assert.Equal("the client's sign-off", card.WaitingOn);
    });

    [Fact]
    public void TheWaitingOnButton_ShowsOnlyWaitingTasks_AndCountsThem() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var waiting = Add(board, "Order parts", "Sam's quote");
        var alsoWaiting = Add(board, "Book room", "facilities", column: "In Progress");
        var free = Add(board, "Write report");
        Assert.Equal("Waiting On (2)", board.WaitingOnButtonLabel);

        board.ShowDueFilterOnly(MainViewModel.WaitingOnFilter);

        Assert.True(waiting.IsVisible);
        Assert.True(alsoWaiting.IsVisible);
        Assert.False(free.IsVisible);

        board.SetCardWaitingOn(waiting, null); // no longer waiting, so it drops out of the view
        Assert.False(waiting.IsVisible);
        Assert.Equal("Waiting On (1)", board.WaitingOnButtonLabel);

        board.ClearFilters();
        Assert.True(free.IsVisible);
    });

    [Fact]
    public void Keyword_FindsWhatATaskIsWaitingOn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var waiting = Add(board, "Order parts", "Sam's quote");
        var other = Add(board, "Write report");

        board.KeywordFilter = "quote";

        Assert.True(waiting.IsVisible);
        Assert.False(other.IsVisible);
    });

    [Fact]
    public void FinishingATask_ClearsWhatItWasWaitingOn_AndUndoBringsItBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts", "Sam's quote");

        board.MoveCardCommand.Execute((card, Column(board, "Done")));

        Assert.False(card.IsWaiting);
        Assert.Null(Assert.Single(Column(OpenBoard(), "Done").Cards).WaitingOn);

        board.Undo();
        Assert.Equal("Sam's quote", card.WaitingOn);
        Assert.Equal("Sam's quote", Assert.Single(Column(OpenBoard(), "To Do").Cards).WaitingOn);
    });

    [Fact]
    public void MovingBetweenOtherColumns_KeepsIt_AndDuplicateCopiesIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts", "Sam's quote");

        board.MoveCardCommand.Execute((card, Column(board, "Waiting")));
        Assert.Equal("Sam's quote", card.WaitingOn);

        Assert.Equal("Sam's quote", board.DuplicateCard(card)!.WaitingOn);
    });

    [Fact]
    public void ARecurringTasksNextOccurrence_StartsOutNotWaiting() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var weekly = Add(board, "Weekly", "last week's numbers", recurring: true);

        board.MoveCardCommand.Execute((weekly, Column(board, "Done")));

        Assert.False(Assert.Single(Column(board, "To Do").Cards).IsWaiting);
    });

    [Fact]
    public void AGroup_CanBeSetAndClearedTogether_AsOneUndoStep() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = new[] { Add(board, "A"), Add(board, "B", "something else"), Add(board, "C") };

        board.SetCardsWaitingOn(cards, "the client");
        Assert.All(cards, c => Assert.Equal("the client", c.WaitingOn));

        board.SetCardsWaitingOn(cards, null);
        Assert.All(cards, c => Assert.False(c.IsWaiting));

        Assert.Equal("Clear waiting-on of 3 tasks", board.Undo());
        Assert.Equal("Set waiting-on of 3 tasks", board.Undo());
        Assert.Equal([null, "something else", null], cards.Select(c => c.WaitingOn));
    });

    [Fact]
    public void CopyAsText_SaysWhatItIsWaitingOn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts", "Sam's quote");

        Assert.Contains("Priority: Normal\r\nWaiting on: Sam's quote", CardTextFormatter.Format(card, "To Do"));
        Assert.DoesNotContain("Waiting on", CardTextFormatter.Format(Add(board, "Free"), "To Do"));
    });

    [Fact]
    public void Excel_RoundTripsWaitingOn_AndAcceptsBlockedByAsAHeading() => wpf.Run(() =>
    {
        var single = _temp.File("one.xlsx");
        ImportService.SaveSingleTaskFile(single, new ImportedTaskRow { Title = "Order parts", WaitingOn = "Sam's quote" });
        Assert.Equal("Sam's quote", Assert.Single(ImportService.ReadTasks(single)).WaitingOn);

        var own = _temp.File("own.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Task";
            sheet.Cell(1, 2).Value = "Blocked By";
            sheet.Cell(2, 1).Value = "Book room";
            sheet.Cell(2, 2).Value = "facilities";
            sheet.Cell(3, 1).Value = "Free task";
            workbook.SaveAs(own);
        }

        var board = OpenBoard();
        var cards = board.ImportCards(ImportService.ReadTasks(own));
        Assert.Equal("facilities", cards[0].WaitingOn);
        Assert.False(cards[1].IsWaiting);
    });
}
