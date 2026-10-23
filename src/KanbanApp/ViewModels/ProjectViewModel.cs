using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ProjectViewModel(Project model) : ManagedListEntryViewModel<Project>(model), IPlannedItem
{
    public MilestoneOwner Owner => MilestoneOwner.Project;

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
