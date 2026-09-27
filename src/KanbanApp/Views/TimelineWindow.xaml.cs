using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using KanbanApp.Converters;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class TimelineWindow : Window
{
    // Week view: 12 weekly columns, paged 4 weeks (28 days) at a time. Day view: 21 daily columns
    // (three weeks), paged 1 week (7 days) at a time. Both step sizes are multiples of 7, so
    // _windowStart stays Monday-aligned regardless of which view is active or how much the user has
    // paged - switching views mid-navigation never needs to re-snap the range.
    private const int WeekViewUnits = 12;
    private const int DayViewUnits = 21;
    private const int WeekViewStepDays = 28;
    private const int DayViewStepDays = 7;

    private readonly MainViewModel _viewModel;
    private DateTime _windowStart;
    private bool _initializing = true;

    // Reuses the exact same priority palette as the main board's priority badge (see
    // PriorityToBrushConverter) rather than defining a second one here, so "what color means High"
    // stays consistent across the whole app.
    private static readonly PriorityToBrushConverter PriorityBrushConverter = new();

    private static Brush GetPriorityBrush(string priority) =>
        (Brush)PriorityBrushConverter.Convert(priority, typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture)!;

    private static Color GetPriorityColor(string priority) => ((SolidColorBrush)GetPriorityBrush(priority)).Color;

    private static Color LightenColor(Color color, double whiteAmount) => Color.FromRgb(
        (byte)(color.R + (255 - color.R) * whiteAmount),
        (byte)(color.G + (255 - color.G) * whiteAmount),
        (byte)(color.B + (255 - color.B) * whiteAmount));

    public TimelineWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowStart = MondayOf(DateTime.Today);
        RestoreSize();
        if (_viewModel.TimelineDayView) DayViewRadio.IsChecked = true;
        Closing += (_, _) => SaveSize();
        _initializing = false;
        BuildGrid();
    }

    // Opens at the size it was last left. It never opens bigger than the screen it is on - the
    // remembered size may have come from a larger monitor.
    private void RestoreSize()
    {
        if (_viewModel.TimelineWindowSize is not { } size) return;

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
        _viewModel.SaveTimelineWindowSize(width, height, maximized);
    }

    private bool IsDayView => DayViewRadio.IsChecked == true;
    private int UnitDays => IsDayView ? 1 : 7;
    private int UnitsToShow => IsDayView ? DayViewUnits : WeekViewUnits;
    private int StepDays => IsDayView ? DayViewStepDays : WeekViewStepDays;

    private static DateTime MondayOf(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow(_viewModel, "HelpSection_Timeline") { Owner = this }.ShowDialog();
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = _windowStart.AddDays(-StepDays);
        BuildGrid();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = _windowStart.AddDays(StepDays);
        BuildGrid();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = MondayOf(DateTime.Today);
        BuildGrid();
    }

    private void IncludeDoneCheckBox_Changed(object sender, RoutedEventArgs e) => BuildGrid();

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
        _viewModel.SaveTimelineDayView(IsDayView);
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
        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            _viewModel.EditCard(card, dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedPeople, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl, dueTime: dialog.SelectedDueTime,
                startDate: dialog.SelectedStartDate, waitingOn: dialog.WaitingOn, recurrencesLeft: dialog.RecurrencesLeft);
        }

        BuildGrid();
    }

    // Shared by BuildGrid and BuildPrintDocument so both work from identical data: which projects
    // get a row, which cards land in each, and the current unit/day math for the visible window.
    private (List<string> RowProjects, Dictionary<string, List<CardViewModel>> ByProject, int UnitsToShow, int UnitDays, DateTime RangeEnd) GetVisibleData()
    {
        var unitDays = UnitDays;
        var unitsToShow = UnitsToShow;
        var rangeEnd = _windowStart.AddDays(unitsToShow * unitDays);

        var includeDone = IncludeDoneCheckBox.IsChecked == true;
        var cards = _viewModel.Columns
            .Where(c => includeDone || c.Name != "Done")
            .SelectMany(c => c.Cards)
            .Where(c => TimelineLayout.IsInWindow(c, _windowStart, rangeEnd))
            .ToList();

        var byProject = cards.GroupBy(c => c.ProjectName).ToDictionary(g => g.Key, g => g.ToList());

        var rowProjects = _viewModel.Projects.Select(p => p.Name).Where(byProject.ContainsKey).ToList();
        if (byProject.ContainsKey("No Project")) rowProjects.Add("No Project");

        return (rowProjects, byProject, unitsToShow, unitDays, rangeEnd);
    }
}
