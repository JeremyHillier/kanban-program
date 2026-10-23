using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class TimelineWindow : Window
{
    // The columns for each view (Month, Week, Day) and how far the arrows page are in TimelineScale.
    // _windowStart is the first day of the first column: the 1st of a month in Month view, else a
    // Monday; switching view snaps it to the new view's kind of start.
    private readonly MainViewModel _viewModel;
    private DateTime _windowStart;
    private DateTime _windowEnd; // the first day after what is showing, as last drawn
    private bool _initializing = true;
    private TimelineFilter _filter = TimelineFilter.None; // which people, priorities and flags are showing
    private int _months = TimelineScale.MonthUnits; // how many months Month view shows

    // Summary rows the user has opened to show their tasks, by name. Kept for as long as the window is open.
    private readonly HashSet<string> _expanded;

    // A row is remembered by its kind and name, so a project and a goal with the same name stay apart.
    private string RowKey(string name) => (IsByGoal ? "G:" : "P:") + name;

    // One row of the Timeline: a project or goal. Summary is its bar (Summary view only); Items are its
    // tasks, laid out by TimelineLayout - every row in Detail view, only opened rows in Summary view.
    private sealed record TimelineRow(string Name, bool IsExpanded, TimelineSummary? Summary, List<TimelineItem> Items, DateTime? Target = null, List<Milestone>? Milestones = null)
    {
        public int FirstTaskLane => Summary is null ? 0 : 1;
        // A row with milestones has one more lane at the foot, kept for their words so a task box can never cover them.
        public bool HasMilestoneLane => Milestones is { Count: > 0 };
        public int Lanes => Math.Max(1, FirstTaskLane + (Items.Count == 0 ? 0 : Items.Max(i => i.Lane) + 1)) + (HasMilestoneLane ? 1 : 0);
    }

    // The same colours as the priority badge on the board: both come from the task file's priority list.
    private Brush GetPriorityBrush(string priority) => _viewModel.Priorities.Brush(priority);

    private Color GetPriorityColor(string priority) => ((SolidColorBrush)GetPriorityBrush(priority)).Color;

    private static Color LightenColor(Color color, double whiteAmount) => Color.FromRgb(
        (byte)(color.R + (255 - color.R) * whiteAmount),
        (byte)(color.G + (255 - color.G) * whiteAmount),
        (byte)(color.B + (255 - color.B) * whiteAmount));

    // The Summary bars: a project blue, or a goal purple, both dark enough for white text.
    private Color SummaryBarColor => IsByGoal ? Color.FromRgb(0x5B, 0x4A, 0x9E) : Color.FromRgb(0x2F, 0x6F, 0xA8);

    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

    public TimelineWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                NewTask_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
            switch (e.Key)
            {
                case Key.Left: Prev_Click(this, new RoutedEventArgs()); break;
                case Key.Right: Next_Click(this, new RoutedEventArgs()); break;
                case Key.Home: Today_Click(this, new RoutedEventArgs()); break;
                default: return;
            }
            e.Handled = true;
        };
        _viewModel = viewModel;
        RestoreSize();
        _expanded = _viewModel.TimelineExpanded;
        _filter = _viewModel.TimelineFilter;
        _months = _viewModel.TimelineMonths;
        MonthsComboBox.ItemsSource = TimelineScale.MonthChoices;
        MonthsComboBox.SelectedItem = _months;
        (_viewModel.TimelineView switch { TimelineZoom.Month => MonthViewRadio, TimelineZoom.Day => DayViewRadio, _ => WeekViewRadio }).IsChecked = true;
        (_viewModel.TimelineSummary ? SummaryRadio : DetailRadio).IsChecked = true;
        (_viewModel.TimelineByGoal ? ByGoalRadio : ByProjectRadio).IsChecked = true;
        _windowStart = TimelineScale.Snap(Zoom, DateTime.Today);
        Closing += (_, _) => SaveSize();
        _initializing = false;
        BuildGrid();
    }

    // Opens at the size it was last left. It never opens bigger than the screen it is on - the
    // remembered size may have come from a larger monitor.
    private void RestoreSize()
    {
        if (_viewModel.WindowSize("Timeline") is not { } size) return;

        var area = SystemParameters.WorkArea;
        Width = Math.Clamp(size.Width, MinWidth, Math.Max(MinWidth, area.Width));
        Height = Math.Clamp(size.Height, MinHeight, Math.Max(MinHeight, area.Height));
        if (size.Maximized) WindowState = WindowState.Maximized;
    }

    // A maximized window remembers the size it goes back to as well, so un-maximizing next time
    // doesn't leave it filling the screen.
    private void SaveSize()
    {
        var maximized = WindowState == WindowState.Maximized;
        var width = WindowState == WindowState.Normal ? Width : RestoreBounds.Width;
        var height = WindowState == WindowState.Normal ? Height : RestoreBounds.Height;
        _viewModel.SaveWindowSize("Timeline", width, height, maximized);
    }

    private TimelineZoom Zoom =>
        MonthViewRadio.IsChecked == true ? TimelineZoom.Month
        : DayViewRadio.IsChecked == true ? TimelineZoom.Day
        : TimelineZoom.Week;

    private bool IsSummary => SummaryRadio.IsChecked == true;

    private bool IsByGoal => ByGoalRadio.IsChecked == true;

    private string GroupHeading => IsByGoal ? "Goals" : "Projects";

    private string UnitLabel(DateTime unitStart, bool print) => Zoom switch
    {
        TimelineZoom.Month => unitStart.ToString(print ? "MMM yyyy" : "MMMM\nyyyy"),
        TimelineZoom.Day => unitStart.ToString(print ? "ddd d-MMM" : "ddd\nd-MMM"),
        _ => unitStart.ToString("d-MMM")
    };

    private string EmptyText => IsSummary
        ? "No open tasks with a due or start date in this range."
        : "No tasks with a due or start date in this range.";

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow(_viewModel, "HelpSection_Timeline") { Owner = this }.ShowDialog();
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = TimelineScale.Step(Zoom, _windowStart, -1, _months);
        BuildGrid();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = TimelineScale.Step(Zoom, _windowStart, 1, _months);
        BuildGrid();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = TimelineScale.Snap(Zoom, DateTime.Today);
        BuildGrid();
    }

    private void IncludeDoneCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initializing) BuildGrid();
    }

    // The header row lives in its own ScrollViewer (frozen vertically, no scrollbar of its own) so
    // it stays visible while the body scrolls; this keeps its horizontal offset locked to the
    // body's so the header columns stay lined up with the body's as the user scrolls sideways.
    private void BodyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0) HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
    }

    private void ZoomLevel_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _viewModel.SaveTimelineView(Zoom);
        // Kept on today when it was showing (a week starting on Sep 28 should become October, not
        // September); otherwise on where the window started.
        var today = DateTime.Today;
        var anchor = today >= _windowStart && today < _windowEnd ? today : _windowStart;
        _windowStart = TimelineScale.Snap(Zoom, anchor);
        BuildGrid();
    }

    private void Months_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || MonthsComboBox.SelectedItem is not int months) return;
        _months = months;
        _viewModel.SaveTimelineMonths(months);
        BuildGrid();
    }

    // Adds a task from here; the Timeline redraws when it is saved. From a row it starts in that project or goal.
    private void NewTask_Click(object sender, RoutedEventArgs e) => AddTask(null);

    private void AddTask(IPlannedItem? row)
    {
        var dialog = new AddTaskWindow(_viewModel) { Owner = this };
        if (row is ProjectViewModel project) dialog.PreselectPlan(project, null);
        else if (row is GoalViewModel goal) dialog.PreselectPlan(null, goal);

        if (dialog.ShowDialog() == true) dialog.AddTo(_viewModel);
        BuildGrid();
    }

    private void Filters_Click(object sender, RoutedEventArgs e)
    {
        var window = new TimelineFiltersWindow(_viewModel, _filter) { Owner = this };
        if (window.ShowDialog() != true) return;

        _filter = window.Result;
        _viewModel.SaveTimelineFilter(_filter);
        BuildGrid();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _viewModel.SaveTimelineSummary(IsSummary);
        BuildGrid();
    }

    private void GroupBy_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _viewModel.SaveTimelineByGoal(IsByGoal);
        BuildGrid();
    }

    // Opens or closes a Summary row's tasks.
    private void ToggleRow(string name)
    {
        if (!_expanded.Remove(RowKey(name))) _expanded.Add(RowKey(name));
        _viewModel.SaveTimelineExpanded(_expanded);
        BuildGrid();
    }

    // Mirrors MainWindow's own EditCard - opens the same task dialog the board uses, then saves
    // through the same MainViewModel.EditCard call so the change is identical either way. Rebuilds
    // the grid afterward regardless of Save/Cancel, since that's cheap and picks up anything that
    // moved the task out of view (a new due date, project, or column).
    private void OpenCardForEdit(CardViewModel card)
    {
        var currentColumn = _viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (currentColumn is null) return;

        var dialog = new AddTaskWindow(_viewModel, card, currentColumn) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            dialog.SaveTo(_viewModel, card);

            // The green Mark as Done: finished the board's way (its questions included), then redrawn here.
            if (dialog.MarkDoneRequested && Owner is MainWindow board)
            {
                board.MarkDoneFromElsewhere(card, _viewModel, BuildGrid);
                return;
            }
        }

        BuildGrid();
    }

    // The goal or project a row stands for (null for "No Goal" / "No Project"), with its start, target and milestones.
    private IPlannedItem? PlannedItemNamed(string name) => IsByGoal
        ? _viewModel.Goals.FirstOrDefault(g => g.Name == name)
        : _viewModel.Projects.FirstOrDefault(p => p.Name == name);

    private string GroupOf(CardViewModel card) => IsByGoal
        ? (string.IsNullOrWhiteSpace(card.GoalName) ? "No Goal" : card.GoalName)
        : card.ProjectName;

    // Shared by BuildGrid and BuildPrintDocument so both work from identical data: which projects or
    // goals get a row, their bars, the tasks in each, and the columns of the visible window.
    private (List<TimelineRow> Rows, List<DateTime> UnitStarts, DateTime RangeEnd) GetVisibleData()
    {
        var unitStarts = TimelineScale.UnitStarts(Zoom, _windowStart, _months);
        var rangeEnd = _windowEnd = TimelineScale.End(Zoom, _windowStart, _months);
        var includeDone = IncludeDoneCheckBox.IsChecked == true;

        var tasks = _viewModel.Columns
            .Where(c => includeDone || c.Name != "Done")
            .SelectMany(c => c.Cards).Where(_filter.Matches)
            .Where(c => TimelineLayout.IsInWindow(c, _windowStart, rangeEnd))
            .GroupBy(GroupOf).ToDictionary(g => g.Key, g => g.ToList());

        // Summary bars cover open tasks only, all of them, so the bar is the whole project or goal.
        var open = _viewModel.Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards).Where(_filter.Matches)
            .GroupBy(GroupOf).ToDictionary(g => g.Key, g => g.ToList());

        // The Manage list's order, then "No Project" / "No Goal", then anything else a task names.
        var noneName = IsByGoal ? "No Goal" : "No Project";
        var listed = (IsByGoal ? _viewModel.Goals.Select(g => g.Name) : _viewModel.Projects.Select(p => p.Name)).ToList();
        var order = listed.Where(n => n != noneName).Append(noneName)
            .Concat(tasks.Keys.Concat(open.Keys).Except(listed).Where(n => n != noneName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            .Distinct().ToList();

        var rows = new List<TimelineRow>();
        foreach (var name in order)
        {
            var groupTasks = tasks.GetValueOrDefault(name) ?? [];

            // A goal's target date (Group by Goal only). A goal whose target is in the window or later gets a row
            // even if none of its tasks are showing, so the target is never lost.
            var planned = PlannedItemNamed(name);
            var target = planned?.TargetDate;
            var goalStart = planned?.StartDate;
            var targetShows = target is { } day && day.Date >= unitStarts[0]; // in the window, or still to come

            // Its milestones that fall in the window (Group by Goal only). They too give a goal its row.
            var milestones = planned is not null
                ? _viewModel.MilestonesOf(planned).Where(m => TimelineLayout.TargetPosition(m.Date, unitStarts, rangeEnd) is not null).ToList()
                : [];
            var hasMilestones = milestones.Count > 0;

            if (IsSummary)
            {
                var summary = TimelineLayout.Summarise(open.GetValueOrDefault(name) ?? [], DateTime.Today, unitStarts, rangeEnd, target, goalStart);
                if (summary is null && !targetShows && !hasMilestones) continue;

                var expanded = _expanded.Contains(RowKey(name));
                rows.Add(new TimelineRow(name, expanded, summary, expanded ? TimelineLayout.Place(groupTasks, unitStarts, rangeEnd) : [], target, milestones));
            }
            else if (groupTasks.Count > 0 || targetShows || hasMilestones)
            {
                rows.Add(new TimelineRow(name, true, null, TimelineLayout.Place(groupTasks, unitStarts, rangeEnd), target, milestones));
            }
        }

        return (rows, unitStarts, rangeEnd);
    }
}
