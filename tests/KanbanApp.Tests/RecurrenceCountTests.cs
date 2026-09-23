using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A recurring task set to happen a number of times: each new occurrence carries one fewer, and the
// last one creates nothing. Left blank, it repeats with no end, as before.
[Collection(WpfCollection.Name)]
public sealed class RecurrenceCountTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel AddWeekly(MainViewModel board, int? times) =>
        board.AddCard("Water plants", Column(board, "To Do"), board.Projects.First(), "Normal", new DateTime(2026, 9, 7), null,
            true, "Weekly", null, recurrencesLeft: times);

    private static void Complete(MainViewModel board, CardViewModel card) => board.MoveCardCommand.Execute((card, Column(board, "Done")));

    private static void Edit(MainViewModel board, CardViewModel card, bool isRecurring, int? times) =>
        board.EditCard(card, card.Title, Column(board, "To Do"), board.Projects.First(), card.Priority, card.DueDate, card.People,
            isRecurring, isRecurring ? "Weekly" : null, null, card.Flags, card.SubTasks, card.Notes, card.Attachments,
            card.ForceEditOnComplete, card.WebsiteUrl, card.DueTime, card.StartDate, card.WaitingOn, times);

    [Fact]
    public void ATaskSetToThreeTimes_CountsDown_AndTheThirdCreatesNoMore() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var first = AddWeekly(board, 3);
        Assert.Equal("↻ Repeats Weekly · 2 more", first.RecurrenceDisplay);

        Complete(board, first);
        var second = Assert.Single(Column(board, "To Do").Cards);
        Assert.Equal(2, second.RecurrencesLeft);
        Assert.Equal(new DateTime(2026, 9, 14), second.DueDate);
        Assert.Equal("↻ Repeats Weekly · 1 more", second.RecurrenceDisplay);

        Complete(board, second);
        var third = Assert.Single(Column(board, "To Do").Cards);
        Assert.Equal(1, third.RecurrencesLeft);
        Assert.Equal("↻ Repeats Weekly · last one", third.RecurrenceDisplay);
        Assert.False(third.HasNextOccurrence);

        Complete(board, third);
        Assert.Empty(Column(board, "To Do").Cards);
        Assert.Equal(3, Column(board, "Done").Cards.Count);
    });

    [Fact]
    public void LeftBlank_ItKeepsRepeating_WithNoEnd() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddWeekly(board, null);
        Assert.Equal("↻ Repeats Weekly", card.RecurrenceDisplay);

        for (var i = 0; i < 5; i++)
        {
            Complete(board, card);
            card = Assert.Single(Column(board, "To Do").Cards);
            Assert.Null(card.RecurrencesLeft);
        }

        Assert.Equal(new DateTime(2026, 10, 12), card.DueDate);
    });

    [Fact]
    public void TheCount_IsSavedToTheTaskFile_OnTheTaskAndOnEachNewOccurrence() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Complete(board, AddWeekly(board, 4));

        var reopened = OpenBoard();

        Assert.Equal(4, Assert.Single(Column(reopened, "Done").Cards).RecurrencesLeft);
        Assert.Equal(3, Assert.Single(Column(reopened, "To Do").Cards).RecurrencesLeft);
    });

    [Fact]
    public void SkippingAnOccurrence_ByDeletingIt_CountsAsOneOfTheTimes() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.DeleteCard(AddWeekly(board, 2), spawnNextOccurrence: true);

        var last = Assert.Single(Column(board, "To Do").Cards);
        Assert.Equal(1, last.RecurrencesLeft);

        // The last one has nothing to skip to, so deleting it creates nothing.
        board.DeleteCard(last, spawnNextOccurrence: true);
        Assert.Empty(board.Columns.SelectMany(c => c.Cards));
    });

    [Fact]
    public void RaisingTheCountOnTheLastOne_KeepsTheSeriesGoing() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddWeekly(board, 1);

        Edit(board, card, isRecurring: true, times: 3);
        Complete(board, card);

        Assert.Equal(2, Assert.Single(Column(board, "To Do").Cards).RecurrencesLeft);
    });

    [Fact]
    public void ClearingTheCount_MakesItRepeatWithNoEnd_AndUntickingRecurringDropsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddWeekly(board, 5);

        Edit(board, card, isRecurring: true, times: null);
        Assert.Null(card.RecurrencesLeft);
        Assert.True(card.HasNextOccurrence);

        Edit(board, card, isRecurring: true, times: 5);
        Edit(board, card, isRecurring: false, times: 5);
        Assert.Null(card.RecurrencesLeft);
        Assert.Null(Assert.Single(Column(OpenBoard(), "To Do").Cards).RecurrencesLeft);
    });

    [Fact]
    public void ATaskThatDoesNotRepeat_NeverStoresACount() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("One-off", Column(board, "To Do"), board.Projects.First(), "Normal", null, null,
            false, null, null, recurrencesLeft: 6);

        Assert.Null(card.RecurrencesLeft);
        Assert.Equal(string.Empty, card.RecurrenceDisplay);
    });

    [Fact]
    public void DuplicateAndTemplates_CarryTheCount() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddWeekly(board, 6);

        Assert.Equal(6, board.DuplicateCard(card)!.RecurrencesLeft);

        var template = MainViewModel.TemplateFromCard(card);
        Assert.Equal(6, template.RecurrenceCount);
        board.SaveTaskTemplate("Plants", template);
        Assert.Equal(6, OpenBoard().TaskTemplates.Single().RecurrenceCount);

        Edit(board, card, isRecurring: false, times: null);
        Assert.Null(MainViewModel.TemplateFromCard(card).RecurrenceCount);
    });

    [Fact]
    public void UndoingAnEdit_PutsTheOldCountBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddWeekly(board, 4);

        Edit(board, card, isRecurring: true, times: 9);
        board.Undo();

        var restored = Assert.Single(Column(board, "To Do").Cards);
        Assert.Equal(4, restored.RecurrencesLeft);
        Assert.Equal(4, Assert.Single(Column(OpenBoard(), "To Do").Cards).RecurrencesLeft);
    });

    [Theory]
    [InlineData(null, "")]
    [InlineData(1, " (this is the last one)")]
    [InlineData(2, " (1 more after this one)")]
    [InlineData(10, " (9 more after this one)")]
    public void TheCopiedText_SaysHowManyAreStillToCome(int? left, string expected)
    {
        Assert.Equal(expected, CardTextFormatter.RepeatsLeftText(left));
    }
}
