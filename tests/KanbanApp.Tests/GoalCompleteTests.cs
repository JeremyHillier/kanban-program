using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Marking a goal complete: it stops being offered, keeps its tasks, is kept in the task file, and can be reopened.
[Collection(WpfCollection.Name)]
public sealed class GoalCompleteTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void ACompleteGoal_IsNoLongerOffered_ButItsTasksKeepIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Perfect Systems").Item!;
        var project = board.Projects.First();
        var toDo = board.Columns.Single(c => c.Name == "To Do");
        var card = board.AddCard("Tidy the files", toDo, project, "Normal", null, null, false, null, goal);
        Assert.Contains(goal, board.ActiveGoals);
        Assert.Contains("Perfect Systems", board.GoalFilterOptions);

        board.SetGoalComplete(goal, true);

        Assert.True(goal.IsComplete);
        Assert.Equal(DateTime.Today, goal.CompletedDate);
        Assert.DoesNotContain(goal, board.ActiveGoals);
        Assert.DoesNotContain("Perfect Systems", board.GoalFilterOptions);
        Assert.Equal(goal.Id, card.GoalId); // the task keeps it
        Assert.True(goal.IsActive); // finished is not the same as switched off
    });

    [Fact]
    public void Reopening_OffersItAgain() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Learn Spanish").Item!;
        board.SetGoalComplete(goal, true);

        board.SetGoalComplete(goal, false);

        Assert.False(goal.IsComplete);
        Assert.Null(goal.CompletedDate);
        Assert.Contains(goal, board.ActiveGoals);
        Assert.Contains("Learn Spanish", board.GoalFilterOptions);
    });

    [Fact]
    public void MarkingItCompleteAgain_KeepsTheFirstDate() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Save for the trip").Item!;
        goal.CompletedDate = DateTime.Today.AddDays(-9); // completed nine days ago

        board.SetGoalComplete(goal, true);

        Assert.Equal(DateTime.Today.AddDays(-9), goal.CompletedDate);
    });

    [Fact]
    public void TheDate_IsKeptInTheTaskFile_AndStampsItAsNewer() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var board = new MainViewModel(db);
        var goal = board.AddGoal("Weight to 98 kg").Item!;
        Assert.True(db.FileFormatAtOpen < DatabaseService.GoalCompleteFileFormat);

        board.SetGoalComplete(goal, true);

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal(DateTime.Today, reopened.Goals.Single(g => g.Name == "Weight to 98 kg").CompletedDate);
        Assert.Equal(DatabaseService.GoalCompleteFileFormat, new DatabaseService(_temp.File("board.db")).FileFormatAtOpen);
    });

    [Fact]
    public void OpenTasks_AreTheOnesNotInDone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Perfect Systems").Item!;
        var project = board.Projects.First();
        var toDo = board.Columns.Single(c => c.Name == "To Do");
        var done = board.Columns.Single(c => c.Name == "Done");
        board.AddCard("Open one", toDo, project, "Normal", null, null, false, null, goal);
        board.AddCard("Open two", toDo, project, "Normal", null, null, false, null, goal);
        board.AddCard("Finished", done, project, "Normal", null, null, false, null, goal);
        board.AddCard("Other goal", toDo, project, "Normal", null, null, false, null, null);

        Assert.Equal(["Open one", "Open two"], board.OpenTasksInGoal(goal).Select(c => c.Title).Order());
    });

    [Fact]
    public void AProject_HasNoCompletedDate() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Null(((IPlannedItem)board.Projects.First()).CompletedDate);
    });
}
