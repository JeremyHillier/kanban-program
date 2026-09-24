using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ProjectViewModel(Project model) : ManagedListEntryViewModel<Project>(model);
