using System.Windows;
using System.Windows.Input;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// F1 opens Help from any window, as it does across Windows, at the section about that window; the
// Help button at the bottom left of every working window does the same. One class handler and one
// map of window to section, registered once at start-up, so no window needs a binding of its own.
public static class HelpKey
{
    // The Help section each window opens on (an x:Name on the How it works tab). A window not listed
    // opens Help at the top.
    public static readonly IReadOnlyDictionary<Type, string> Topics = new Dictionary<Type, string>
    {
        [typeof(AddTaskWindow)] = "HelpSection_AddEditTask",
        [typeof(QuickAddWindow)] = "HelpSection_QuickAdd",
        [typeof(ManageTemplatesWindow)] = "HelpSection_TaskTemplates",
        [typeof(DeleteRecurringTaskWindow)] = "HelpSection_RecurringTasks",
        [typeof(ManageWaitingOnWindow)] = "HelpSection_WaitingOn",
        [typeof(ManageListWindow)] = "HelpSection_ProjectsGoals",
        [typeof(MilestonesWindow)] = "HelpSection_ProjectsGoals",
        [typeof(ManagePrioritiesWindow)] = "HelpSection_Priorities",
        [typeof(ManageCustomFiltersWindow)] = "HelpSection_CustomFilters",
        [typeof(ArchivedTasksWindow)] = "HelpSection_ArchivingDeleting",
        [typeof(DeletedTasksWindow)] = "HelpSection_ArchivingDeleting",
        [typeof(ImportTasksWindow)] = "HelpSection_ImportingTasks",
        [typeof(ImportedTasksWindow)] = "HelpSection_ImportingTasks",
        [typeof(ReportBuilderWindow)] = "HelpSection_ReportBuilder",
        [typeof(QuickReportWindow)] = "HelpSection_ReportBuilder",
        [typeof(ReportPreviewWindow)] = "HelpSection_ReportOutput",
        [typeof(DashboardWindow)] = "HelpSection_Dashboard",
        [typeof(TimelineWindow)] = "HelpSection_Timeline",
        [typeof(TimelineFiltersWindow)] = "HelpSection_Timeline",
        [typeof(ReminderWindow)] = "HelpSection_Reminders",
        [typeof(SettingsWindow)] = "HelpSection_Settings",
    };

    // Windows where F1 does nothing: Help itself, a message box, and the splash.
    public static readonly IReadOnlySet<Type> Silent = new HashSet<Type> { typeof(HelpWindow), typeof(MessageWindow), typeof(SplashWindow) };

    public static void Register() =>
        EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));

    // The Help button at the foot of a window: its parameter is the window.
    public static ICommand ShowCommand { get; } = new RelayCommand(p => { if (p is Window w) Show(w); });

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F1 || Keyboard.Modifiers != ModifierKeys.None || sender is not Window window || Silent.Contains(window.GetType())) return;
        e.Handled = true;
        Show(window);
    }

    // Opens Help for the window, at its section when it has one. Help needs the board's view model
    // for the version and What's New; a window carries it, or the board does.
    public static void Show(Window owner)
    {
        var viewModel = owner.DataContext as MainViewModel ?? Application.Current?.MainWindow?.DataContext as MainViewModel;
        if (viewModel is null) return;
        new HelpWindow(viewModel, Topics.GetValueOrDefault(owner.GetType())) { Owner = owner }.ShowDialog();
    }
}
