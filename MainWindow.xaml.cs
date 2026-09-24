using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;
    private readonly DatabaseService _db;

    private readonly TimeAlertTracker _timeAlerts = new();
    private readonly DispatcherTimer _dueTimeTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    public MainWindow(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        var mainViewModel = new MainViewModel(db);
        DataContext = mainViewModel;

        // Anything whose time had already passed before the app opened is left to the startup
        // reminder list rather than also popping a time alert on the first tick.
        _timeAlerts.MarkAnnounced(mainViewModel.GetCardsPastDueTime());
        _dueTimeTimer.Tick += DueTimeTimer_Tick;
        _dueTimeTimer.Start();

        RestoreWindowBounds();
        Closing += (_, _) =>
        {
            _dueTimeTimer.Stop();
            SaveWindowBounds();
            var viewModel = DataContext as MainViewModel;
            viewModel?.SaveLastViewState();

            if (viewModel is { AutoBackupEnabled: true })
            {
                BackupService.CreateBackup(_db.DbPath, viewModel.BackupRetentionCount);
            }
        };
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        // Both of these are deferred for the same reason: showing a modal dialog synchronously here
        // would block before the splash screen (still on screen at this point) gets a chance to
        // close. Queued in order, so What's New is dealt with before the reminder list appears.
        if (viewModel.ShouldShowWhatsNewOnStartup())
        {
            Dispatcher.BeginInvoke(() => ShowWhatsNew(viewModel), DispatcherPriority.ApplicationIdle);
        }

        CheckForUpdatesOnStartup(viewModel);

        if (!viewModel.ShowDueReminders) return;

        var dueCards = viewModel.GetDueReminders();
        if (dueCards.Count == 0) return;

        Dispatcher.BeginInvoke(() => ShowReminders(dueCards, viewModel), DispatcherPriority.ApplicationIdle);
    }

    private void RestoreWindowBounds()
    {
        var width = double.TryParse(_db.GetSetting("WindowWidth"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : Width;
        var height = double.TryParse(_db.GetSetting("WindowHeight"), NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? h : Height;
        Width = Math.Clamp(width, MinWidth, SystemParameters.VirtualScreenWidth);
        Height = Math.Clamp(height, MinHeight, SystemParameters.VirtualScreenHeight);

        var hasLeft = double.TryParse(_db.GetSetting("WindowLeft"), NumberStyles.Float, CultureInfo.InvariantCulture, out var left);
        var hasTop = double.TryParse(_db.GetSetting("WindowTop"), NumberStyles.Float, CultureInfo.InvariantCulture, out var top);
        if (hasLeft && hasTop &&
            left + Width > SystemParameters.VirtualScreenLeft && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
            top + 50 > SystemParameters.VirtualScreenTop && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    private void SaveWindowBounds()
    {
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        _db.SetSetting("WindowWidth", bounds.Width.ToString(CultureInfo.InvariantCulture));
        _db.SetSetting("WindowHeight", bounds.Height.ToString(CultureInfo.InvariantCulture));
        _db.SetSetting("WindowLeft", bounds.Left.ToString(CultureInfo.InvariantCulture));
        _db.SetSetting("WindowTop", bounds.Top.ToString(CultureInfo.InvariantCulture));
    }

    // Fit to Window shares the board's width among the task columns, so the board reports it
    // whenever it changes (window resized, button column shown or hidden, buttons moved sides).
    private void BoardScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && DataContext is MainViewModel viewModel) viewModel.SetBoardWidth(e.NewSize.Width);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private DispatcherTimer? _statusMessageTimer;

    // Deferred like every other board change made from a click, and says what it took back, since
    // the card that changed may be scrolled out of view or in another column.
    private void UndoLastAction()
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.CanUndo) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (viewModel.Undo() is not { } undone) return;

            ShowStatusMessage($"Undid: {undone}");
        }), DispatcherPriority.Background);
    }

    // The short line at the foot of the board ("Undid: ...", "Added: ..."), gone after a few seconds.
    private void ShowStatusMessage(string message)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.StatusMessage = message;
        _statusMessageTimer?.Stop();
        _statusMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _statusMessageTimer.Tick += (timer, _) =>
        {
            ((DispatcherTimer)timer!).Stop();
            viewModel.StatusMessage = string.Empty;
        };
        _statusMessageTimer.Start();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new DashboardWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
    }
}
