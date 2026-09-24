using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class GoalViewModel(Goal model) : ManagedListEntryViewModel<Goal>(model);
