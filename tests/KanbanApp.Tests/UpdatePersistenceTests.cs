using System.Text.Json;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The Update button stays after a restart: the newer version a check found is kept per PC in the task file
// until this copy is past it or the version is skipped.
[Collection(WpfCollection.Name)]
public sealed class UpdatePersistenceTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void AnUpdateFound_IsStillOfferedAfterARestart_UntilSkipped() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.NoteUpdateCheck(new UpdateInfo("999.0.0", ["New: something"]));
        Assert.True(board.HasAvailableUpdate);

        var reopened = OpenBoard();
        Assert.Equal("999.0.0", reopened.AvailableUpdate?.Version);
        Assert.Equal(["New: something"], reopened.AvailableUpdate!.Notes);

        reopened.SkipUpdateVersion("999.0.0");
        Assert.False(reopened.HasAvailableUpdate);
        Assert.False(OpenBoard().HasAvailableUpdate);
    });

    [Fact]
    public void AStoredUpdate_ThisCopyHasSinceReached_IsForgotten() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var db = new DatabaseService(_temp.File("board.db"));
        db.SetSetting($"AvailableUpdate:{board.ThisComputer}", JsonSerializer.Serialize(new UpdateInfo("0.0.1", ["Old news"])));

        var reopened = new MainViewModel(db);
        Assert.False(reopened.HasAvailableUpdate);
        Assert.True(string.IsNullOrEmpty(new DatabaseService(_temp.File("board.db")).GetSetting($"AvailableUpdate:{board.ThisComputer}")));
    });

    [Fact]
    public void ACheckThatFindsNothingNewer_ClearsIt_AndOneThatCannotReachTheList_LeavesIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.NoteUpdateCheck(new UpdateInfo("999.0.0", []));

        board.NoteUpdateCheck(null); // couldn't reach the list
        Assert.True(board.HasAvailableUpdate);
        Assert.True(OpenBoard().HasAvailableUpdate);

        board.NoteUpdateCheck(new UpdateInfo(board.AppVersion.TrimStart('v'), [])); // up to date now
        Assert.False(board.HasAvailableUpdate);
        Assert.False(OpenBoard().HasAvailableUpdate);
    });

    [Fact]
    public void RubbishInTheSetting_IsIgnored() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var db = new DatabaseService(_temp.File("board.db"));
        db.SetSetting($"AvailableUpdate:{board.ThisComputer}", "{not json");

        Assert.False(new MainViewModel(db).HasAvailableUpdate);
    });
}

// The task window's Task and Notes boxes remember the heights they were dragged to.
[Collection(WpfCollection.Name)]
public sealed class TaskBoxHeightsTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheHeights_AreRemembered_AndNoneUntilDragged() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Null(board.TaskBoxHeights);

        board.SaveTaskBoxHeights(80.4, 220);

        Assert.Equal((80d, 220d), new MainViewModel(new DatabaseService(_temp.File("board.db"))).TaskBoxHeights);
    });

    [Fact]
    public void EachTask_RemembersItsOwn_AndOthersUseTheNewTaskHeights() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        board.SaveTaskBoxHeights(null, 60, 150); // a new task was dragged
        board.SaveTaskBoxHeights(7, 120, 400);   // task 7 was dragged

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal((120d, 400d), reopened.TaskBoxHeightsFor(7));
        Assert.Equal((60d, 150d), reopened.TaskBoxHeightsFor(8));    // never dragged: the new-task heights
        Assert.Equal((60d, 150d), reopened.TaskBoxHeightsFor(null)); // a new task
    });
}
