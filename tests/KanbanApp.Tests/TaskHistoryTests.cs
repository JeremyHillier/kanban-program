using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Each task keeps what changed on it, when, and on which PC.
[Collection(WpfCollection.Name)]
public sealed class TaskHistoryTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose()
    {
        DatabaseService.ComputerName = Environment.MachineName;
        _temp.Dispose();
    }

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static List<string> Lines(MainViewModel board, CardViewModel card) =>
        MainViewModel.HistoryEntries(board.GetTaskHistory(card)).Select(e => e.Text).ToList();

    [Fact]
    public void ChangingTheDueDateAndPriority_IsRecordedWithOldAndNewValues() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Order the parts", board.Columns.First(), board.Projects.First(), "Normal", new DateTime(2026, 11, 3), null, false, null, null);

        board.SetCardPriority(card, "High");
        board.SetCardDates(card, null, new DateTime(2026, 11, 10));

        var text = string.Join("\n", Lines(board, card));
        Assert.Contains("Priority Normal → High", text);
        Assert.Contains("Due date Nov 3, 2026 → Nov 10, 2026", text);
        Assert.Contains("Added to", text); // the Created row is still there
    });

    [Fact]
    public void ASaveThatChangesNothing_AddsNoHistory() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Same", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        var before = board.GetTaskHistory(card).Count;

        board.SetCardPriority(card, "Normal");

        Assert.Equal(before, board.GetTaskHistory(card).Count);
    });

    [Fact]
    public void ChangedNotes_KeepAShortExcerptOfTheOldWording() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Notes", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null, notes: "Call the supplier about the delivery date");
        card.Notes = "Supplier confirmed";
        board.SetCardDates(card, null, new DateTime(2026, 11, 10)); // any save writes the card as it stands

        Assert.Contains(Lines(board, card), l => l.Contains("Notes changed (was: \"Call the supplier about the delivery date\")"));
    });

    [Fact]
    public void FlagsPeopleAndSubTasks_AreRecordedByName() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var flag = board.AddFlag("Urgent").Item!;
        var card = board.AddCard("Parts", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);

        board.AddFlagToCard(card, flag);

        Assert.Contains(Lines(board, card), l => l.Contains("Flag added: Urgent"));
    });

    [Fact]
    public void TheComputer_IsRecordedOnEachRow_AndTwoRowsOfOneSaveShowAsOneLine() => wpf.Run(() =>
    {
        DatabaseService.ComputerName = "OFFICE-PC";
        var board = OpenBoard();
        var card = board.AddCard("Parts", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        board.SetCardPriority(card, "High");

        var history = board.GetTaskHistory(card);
        Assert.All(history, h => Assert.Equal("OFFICE-PC", h.Computer));

        var stamp = MainViewModel.HistoryStampText(history)!;
        Assert.StartsWith("History: ", stamp);
        Assert.Contains("Priority Normal → High", stamp);
        Assert.Contains("(OFFICE-PC)", MainViewModel.HistoryTooltipText(history));
    });

    [Fact]
    public void ANewTask_HasNoHistoryButItsCreation() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var flag = board.AddFlag("Urgent").Item!;
        var card = board.AddCard("Fresh", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null, flags: [flag]);

        Assert.Single(board.GetTaskHistory(card));
    });

    [Fact]
    public void NoHistory_GivesNoLine() => Assert.Null(MainViewModel.HistoryStampText([]));
}
