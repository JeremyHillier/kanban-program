using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class FlagViewModel(Flag model) : ManagedListEntryViewModel<Flag>(model);
