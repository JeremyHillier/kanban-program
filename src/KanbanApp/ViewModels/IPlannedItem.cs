using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// What a goal and a project share for planning: a start and a target date, and milestones. The Manage screen, the
// milestones window and the Timeline treat them alike through this.
public interface IPlannedItem : IManagedItem
{
    DateTime? StartDate { get; set; }
    DateTime? TargetDate { get; set; }
    MilestoneOwner Owner { get; }

    // The day it was marked complete; only goals can be, so a project has none.
    DateTime? CompletedDate => null;
}
