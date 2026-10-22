using KanbanApp.Services;

namespace KanbanApp.Tests;

// A goal's optional target date: stored with the goal, cleared again, and only stamping the file as
// newer once one has been set (older copies ignore the column but would drop it on a rewrite).
public sealed class GoalTargetTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private DatabaseService Open() => new(_temp.File("board.db"));

    [Fact]
    public void ATargetDate_IsKept_AndCanBeCleared()
    {
        var db = Open();
        var goal = db.AddGoal("Grow revenue");
        Assert.Null(Assert.Single(db.GetGoals()).TargetDate);

        db.SetGoalTarget(goal.Id, new DateTime(2027, 3, 31));
        Assert.Equal(new DateTime(2027, 3, 31), Assert.Single(Open().GetGoals()).TargetDate);

        db.SetGoalTarget(goal.Id, null);
        Assert.Null(Assert.Single(Open().GetGoals()).TargetDate);
    }

    [Fact]
    public void TheFile_IsOnlyStampedAsNewer_OnceATargetIsSet()
    {
        var db = Open();
        var goal = db.AddGoal("Smoother operations");
        Open(); // a second open stamps the file as an ordinary one
        Assert.Equal(DatabaseService.FormatStampedOnOpen.ToString(), db.GetSetting("FileFormat"));

        db.SetGoalTarget(goal.Id, null);
        Assert.Equal(DatabaseService.FormatStampedOnOpen.ToString(), db.GetSetting("FileFormat"));

        db.SetGoalTarget(goal.Id, new DateTime(2027, 1, 15));
        Assert.Equal(DatabaseService.GoalTargetFileFormat.ToString(), db.GetSetting("FileFormat"));
    }
}
