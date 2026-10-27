using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class GoalViewModel(Goal model) : ManagedListEntryViewModel<Goal>(model), IPlannedItem
{
    public MilestoneOwner Owner => MilestoneOwner.Goal;

    public DateTime? StartDate
    {
        get => Model.StartDate;
        set
        {
            if (Model.StartDate == value) return;
            Model.StartDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime? CompletedDate
    {
        get => Model.CompletedDate;
        set
        {
            if (Model.CompletedDate == value) return;
            Model.CompletedDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(IsOffered));
        }
    }

    public bool IsComplete => CompletedDate is not null;

    // Whether it is offered for a new task and in the board's Goal filter: switched on, and not finished.
    // (A task that already has it keeps it, and its own task screen still shows it.)
    public bool IsOffered => IsActive && !IsComplete;

    public DateTime? TargetDate
    {
        get => Model.TargetDate;
        set
        {
            if (Model.TargetDate == value) return;
            Model.TargetDate = value;
            OnPropertyChanged();
        }
    }
}
