using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The Priority and Who lists share one row; dragging the bar between them moves the share, which is remembered.
[Collection(WpfCollection.Name)]
public sealed class FilterShareTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheShare_IsClamped_Saved_AndRestored() => wpf.Run(() =>
    {
        var path = _temp.File("board.db");
        var board = new MainViewModel(new DatabaseService(path));
        Assert.Equal(0.5, board.PriorityListShare);

        board.PriorityListShare = 0.9;
        Assert.Equal(0.75, board.PriorityListShare);
        Assert.Equal(0.25, board.WhoListWidth.Value, 6);

        board.PriorityListShare = 0.4;
        board.SaveFilterListHeights();

        var reopened = new MainViewModel(new DatabaseService(path));
        Assert.Equal(0.4, reopened.PriorityListShare, 6);
    });
}
