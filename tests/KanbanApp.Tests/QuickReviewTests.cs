using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A task entered with Quick Add waits on the review list until the user marks it reviewed - saving it does not.
[Collection(WpfCollection.Name)]
public sealed class QuickReviewTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void AQuickAddTask_IsMarkedForReview_AndStaysMarkedAfterReopening() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var quick = board.QuickAdd("Call the bank", null)!;
        var ordinary = board.AddCard("Ordinary", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);

        Assert.True(quick.NeedsReview);
        Assert.False(ordinary.NeedsReview);
        Assert.Equal(1, board.QuickReviewCount);
        Assert.Equal("Review (1)", board.QuickReviewButtonText);

        var reopened = OpenBoard();
        Assert.Equal(["Call the bank"], reopened.QuickEntriesToReview().Select(c => c.Title));
    });

    [Fact]
    public void SavingTheTask_DoesNotClearTheMark_MarkReviewedDoes_AndUndoBringsItBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var quick = board.QuickAdd("Call the bank", null)!;

        board.SetCardPriority(quick, "High");
        Assert.True(quick.NeedsReview);

        board.MarkReviewed(quick);
        Assert.False(quick.NeedsReview);
        Assert.False(board.HasQuickEntriesToReview);
        Assert.Empty(OpenBoard().QuickEntriesToReview());
        Assert.Contains(board.GetTaskHistory(quick), h => h.Details == "Quick entry reviewed");

        board.Undo();
        Assert.True(board.QuickEntriesToReview().Single().NeedsReview);
        Assert.True(OpenBoard().QuickEntriesToReview().Count == 1);
    });

    [Fact]
    public void ADuplicate_OfAQuickEntry_IsNotOnTheList() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var quick = board.QuickAdd("Call the bank", null)!;
        board.DuplicateCard(quick);

        Assert.Single(board.QuickEntriesToReview());
    });
}
