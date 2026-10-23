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

    [Fact]
    public void Milestones_AreKept_ChangedAndDeleted_WithTheirGoal()
    {
        var db = Open();
        var goal = db.AddGoal("Grow revenue");
        var other = db.AddGoal("Smoother operations");

        var site = db.AddGoalMilestone(goal.Id, "Site live", new DateTime(2026, 12, 1, 15, 30, 0));
        db.AddGoalMilestone(goal.Id, "Press launch", new DateTime(2026, 11, 1));
        db.AddGoalMilestone(other.Id, "Keys handed back", new DateTime(2027, 1, 5));

        var all = Open().GetGoalMilestones();
        Assert.Equal(["Press launch", "Site live", "Keys handed back"], all.Select(m => m.Description)); // by date
        Assert.Equal(new DateTime(2026, 12, 1), all.First(m => m.Id == site.Id).Date); // a day, never a time

        db.UpdateGoalMilestone(site.Id, "Site live (v2)", new DateTime(2026, 12, 10));
        Assert.Equal(("Site live (v2)", new DateTime(2026, 12, 10)), Open().GetGoalMilestones().Where(m => m.Id == site.Id).Select(m => (m.Description, m.Date)).Single());

        db.DeleteGoalMilestone(site.Id);
        Assert.DoesNotContain(Open().GetGoalMilestones(), m => m.Id == site.Id);

        db.DeleteGoal(goal.Id);
        Assert.Equal(["Keys handed back"], Open().GetGoalMilestones().Select(m => m.Description));
    }

    [Fact]
    public void TheFile_IsOnlyStampedAsNewer_OnceAMilestoneIsSaved()
    {
        var db = Open();
        var goal = db.AddGoal("Smoother operations");
        Open();
        Assert.Equal(DatabaseService.FormatStampedOnOpen.ToString(), db.GetSetting("FileFormat"));

        db.AddGoalMilestone(goal.Id, "Keys handed back", new DateTime(2027, 1, 5));
        Assert.Equal(DatabaseService.GoalMilestoneFileFormat.ToString(), db.GetSetting("FileFormat"));
    }

    [Fact]
    public void AStartDate_IsKept_ClearedAndStampsTheFileOnlyOnceSet()
    {
        var db = Open();
        var goal = db.AddGoal("Grow revenue");
        db.SetGoalTarget(goal.Id, new DateTime(2027, 3, 31));
        db.SetGoalStart(goal.Id, null);
        Assert.Equal(DatabaseService.GoalTargetFileFormat.ToString(), db.GetSetting("FileFormat")); // still only the target's stamp

        db.SetGoalStart(goal.Id, new DateTime(2026, 11, 2));
        var saved = Assert.Single(Open().GetGoals());
        Assert.Equal((new DateTime(2026, 11, 2), new DateTime(2027, 3, 31)), (saved.StartDate!.Value, saved.TargetDate!.Value));
        Assert.Equal(DatabaseService.GoalStartFileFormat.ToString(), db.GetSetting("FileFormat"));

        db.SetGoalStart(goal.Id, null);
        Assert.Null(Assert.Single(Open().GetGoals()).StartDate);
    }
}
