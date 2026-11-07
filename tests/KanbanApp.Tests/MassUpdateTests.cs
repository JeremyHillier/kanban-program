using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Mass Update: only the ticked fields change, it is one Undo step, and copies go to another task file.
[Collection(WpfCollection.Name)]
public sealed class MassUpdateTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private (MainViewModel Board, List<CardViewModel> Cards) Board()
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var todo = board.Columns.Single(c => c.Name == "To Do");
        var cards = Enumerable.Range(1, 3).Select(i => board.AddCard($"Task {i}", todo, board.Projects.First(), i == 1 ? "High" : "Normal",
            DateTime.Today.AddDays(i), null, false, null, null, startDate: DateTime.Today)).ToList();
        return (board, cards);
    }

    [Fact]
    public void OnlyTheTickedFields_Change() => wpf.Run(() =>
    {
        var (board, cards) = Board();

        board.ApplyMassUpdate(cards, new MassUpdateChanges { Priority = "Low" });

        Assert.All(cards, c => Assert.Equal("Low", c.Priority));
        Assert.Equal([DateTime.Today.AddDays(1), DateTime.Today.AddDays(2), DateTime.Today.AddDays(3)], cards.Select(c => c.DueDate!.Value));
        Assert.All(cards, c => Assert.Equal("To Do", board.ColumnOf(c)!.Name));
    });

    [Fact]
    public void ShiftingDates_MovesDueAndStartTogether_AndLeavesUndatedTasksAlone() => wpf.Run(() =>
    {
        var (board, cards) = Board();
        var undated = board.AddCard("No date", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);

        board.ApplyMassUpdate([.. cards, undated], new MassUpdateChanges { Due = DueChange.Shift, ShiftDays = 7 });

        Assert.Equal(DateTime.Today.AddDays(8), cards[0].DueDate);
        Assert.Equal(DateTime.Today.AddDays(7), cards[0].StartDate);
        Assert.Null(undated.DueDate);
    });

    [Fact]
    public void ManyChangesAtOnce_AreOneUndoStep() => wpf.Run(() =>
    {
        var (board, cards) = Board();

        var moved = board.ApplyMassUpdate(cards, new MassUpdateChanges { Priority = "Low", Due = DueChange.Clear, SetColor = true, Color = "Blue", Column = board.DoneColumn });

        Assert.Equal(3, moved.Count);
        Assert.All(cards, c => Assert.Equal(("Low", null, "Blue"), (c.Priority, c.DueDate, c.Color)));

        board.Undo();

        Assert.Equal("High", cards[0].Priority);
        Assert.Equal(DateTime.Today.AddDays(1), cards[0].DueDate);
        Assert.Null(cards[0].Color);
        Assert.Equal("To Do", board.ColumnOf(cards[0])!.Name);
    });

    [Fact]
    public void AFlag_CanBeAddedToAll_AndTakenOffAgain() => wpf.Run(() =>
    {
        var (board, cards) = Board();
        var flag = board.AddFlag("Urgent").Item!;

        board.ApplyMassUpdate(cards, new MassUpdateChanges { FlagAction = FlagChange.Add, Flag = flag });
        Assert.All(cards, c => Assert.Single(c.Flags));

        board.ApplyMassUpdate(cards, new MassUpdateChanges { FlagAction = FlagChange.Remove, Flag = flag });
        Assert.All(cards, c => Assert.Empty(c.Flags));
    });

    [Fact]
    public void SelectAllVisible_SkipsWhatTheFiltersHide() => wpf.Run(() =>
    {
        var (board, cards) = Board();
        board.SetCardPriority(cards[0], "High");
        board.KeywordFilter = "Task 1";

        board.SelectAllVisible();

        Assert.Equal(1, board.SelectedCardCount);
    });

    [Fact]
    public void Copies_GoToAnotherFile_WithTheirProjectAndFlagMatchedByName() => wpf.Run(() =>
    {
        var (board, cards) = Board();
        var flag = board.AddFlag("Urgent").Item!;
        board.AddFlagToCard(cards[0], flag);
        var target = _temp.File("other\\other.db");
        _ = new DatabaseService(target);

        var count = TaskCopyService.Copy(cards.Select(c => (c, "To Do")), target);

        Assert.Equal(3, count);
        var other = new MainViewModel(new DatabaseService(target));
        var copied = other.Columns.SelectMany(c => c.Cards).OrderBy(c => c.Title).ToList();
        Assert.Equal(["Task 1", "Task 2", "Task 3"], copied.Select(c => c.Title));
        Assert.Equal(cards[0].ProjectName, copied[0].ProjectName);
        Assert.Equal("Urgent", Assert.Single(copied[0].Flags).Name);
        Assert.Equal(3, board.Columns.SelectMany(c => c.Cards).Count()); // the originals stay
    });
}
