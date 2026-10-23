using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// The milestones of goals and projects (a description and a date), kept here after the first read of each kind and
// changed only through these methods. Like the other managed lists they are outside Undo and the Settings snapshot.
public partial class MainViewModel
{
    private List<Milestone>? _goalMilestones;
    private List<Milestone>? _projectMilestones;

    private List<Milestone> MilestoneList(MilestoneOwner owner) => owner == MilestoneOwner.Goal
        ? _goalMilestones ??= _db.GetMilestones(owner)
        : _projectMilestones ??= _db.GetMilestones(owner);

    // Oldest first.
    public List<Milestone> MilestonesOf(IPlannedItem item) =>
        MilestoneList(item.Owner).Where(m => m.OwnerId == item.Id).OrderBy(m => m.Date).ThenBy(m => m.Id).ToList();

    // False for a blank description.
    public bool AddMilestone(IPlannedItem item, string description, DateTime date)
    {
        var text = description.Trim();
        if (text.Length == 0) return false;

        MilestoneList(item.Owner).Add(_db.AddMilestone(item.Owner, item.Id, text, date.Date));
        return true;
    }

    public void ChangeMilestone(Milestone milestone, string description, DateTime date)
    {
        var text = description.Trim();
        if (text.Length == 0 || (milestone.Description == text && milestone.Date == date.Date)) return;

        milestone.Description = text;
        milestone.Date = date.Date;
        _db.UpdateMilestone(milestone.Owner, milestone.Id, text, milestone.Date);
    }

    public void DeleteMilestone(Milestone milestone)
    {
        _db.DeleteMilestone(milestone.Owner, milestone.Id);
        MilestoneList(milestone.Owner).Remove(milestone);
    }
}
