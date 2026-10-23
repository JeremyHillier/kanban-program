using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// A goal's milestones (a description and a date), kept here after the first read and changed only through these
// methods. Like the other managed lists they are outside Undo and the Settings snapshot.
public partial class MainViewModel
{
    private List<GoalMilestone>? _milestones;

    public IReadOnlyList<GoalMilestone> Milestones => _milestones ??= _db.GetGoalMilestones();

    // Oldest first.
    public List<GoalMilestone> MilestonesOf(GoalViewModel goal) =>
        Milestones.Where(m => m.GoalId == goal.Id).OrderBy(m => m.Date).ThenBy(m => m.Id).ToList();

    // False for a blank description.
    public bool AddMilestone(GoalViewModel goal, string description, DateTime date)
    {
        var text = description.Trim();
        if (text.Length == 0) return false;

        _ = Milestones;
        _milestones!.Add(_db.AddGoalMilestone(goal.Id, text, date.Date));
        return true;
    }

    public void ChangeMilestone(GoalMilestone milestone, string description, DateTime date)
    {
        var text = description.Trim();
        if (text.Length == 0 || (milestone.Description == text && milestone.Date == date.Date)) return;

        milestone.Description = text;
        milestone.Date = date.Date;
        _db.UpdateGoalMilestone(milestone.Id, text, milestone.Date);
    }

    public void DeleteMilestone(GoalMilestone milestone)
    {
        _db.DeleteGoalMilestone(milestone.Id);
        _milestones?.Remove(milestone);
    }
}
