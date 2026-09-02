using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    public TimelineWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowStart = MondayOf(DateTime.Today);
        _initializing = false;
        BuildGrid();
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
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedWho, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl);
        }

        BuildGrid();
    }

    // Rebuilds both grids from scratch on every navigation/toggle rather than trying to update them
    // in place - the row/column count changes with the data (only projects with a due task in the
    // visible window get a row), so an incremental update would need the same "figure out which
    // rows/columns are needed" logic anyway. The header row lives in HeaderGrid (its own frozen
    // ScrollViewer) and the project rows live in TimelineGrid (the scrollable body) - both get
    // identical column definitions built by AddColumns so their cells stay lined up.
    private void BuildGrid()
    {
        var brush = (Brush)FindResource("PrimaryTextBrush");
        var secondaryBrush = (Brush)FindResource("SecondaryTextBrush");
        var borderBrush = (Brush)FindResource("CardBorderBrush");
        var cardBrush = (Brush)FindResource("CardBackgroundBrush");
        var panelBrush = (Brush)FindResource("PanelBackgroundBrush");
        var alternateRowBrush = (Brush)FindResource("AlternateRowBrush");

        var unitDays = UnitDays;
        var unitsToShow = UnitsToShow;
        var stepLabel = IsDayView ? "1 Week" : "4 Weeks";
        PrevButton.Content = $"◀ {stepLabel}";
        NextButton.Content = $"{stepLabel} ▶";

        var rangeEnd = _windowStart.AddDays(unitsToShow * unitDays);
        RangeLabel.Text = $"{_windowStart:MMM d} – {rangeEnd.AddDays(-1):MMM d, yyyy}";

        var includeDone = IncludeDoneCheckBox.IsChecked == true;
        var cards = _viewModel.Columns
            .Where(c => includeDone || c.Name != "Done")
            .SelectMany(c => c.Cards)
            .Where(c => c.DueDate is not null && c.DueDate.Value.Date >= _windowStart && c.DueDate.Value.Date < rangeEnd)
            .ToList();

        var byProject = cards.GroupBy(c => c.ProjectName).ToDictionary(g => g.Key, g => g.ToList());

        var rowProjects = _viewModel.Projects.Select(p => p.Name).Where(byProject.ContainsKey).ToList();
        if (byProject.ContainsKey("No Project")) rowProjects.Add("No Project");

        HeaderGrid.Children.Clear();
        HeaderGrid.RowDefinitions.Clear();
        HeaderGrid.ColumnDefinitions.Clear();

        TimelineGrid.Children.Clear();
        TimelineGrid.RowDefinitions.Clear();
        TimelineGrid.ColumnDefinitions.Clear();

        const double projectColWidth = 150;
        var unitColWidth = IsDayView ? 90 : 150;

        void AddColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(projectColWidth) });
            for (var w = 0; w < unitsToShow; w++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitColWidth) });
            }
        }
        AddColumns(HeaderGrid);
        AddColumns(TimelineGrid);

        HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddCell(Grid grid, int row, int col, UIElement content, Brush? background = null)
        {
            var border = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = background,
                Child = content
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        AddCell(HeaderGrid, 0, 0, new TextBlock
        {
            Text = "Projects", FontWeight = FontWeights.Bold, Foreground = brush,
            Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center
        }, panelBrush);

        for (var w = 0; w < unitsToShow; w++)
        {
            var unitStart = _windowStart.AddDays(w * unitDays);
            var headerText = IsDayView ? unitStart.ToString("ddd\nd-MMM") : unitStart.ToString("d-MMM");
            AddCell(HeaderGrid, 0, w + 1, new TextBlock
            {
                Text = headerText, FontWeight = FontWeights.Bold, Foreground = brush,
                Margin = new Thickness(4, 6, 4, 6), VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            }, panelBrush);
        }

        if (rowProjects.Count == 0)
        {
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });
            AddCell(TimelineGrid, 0, 0, new TextBlock
            {
                Text = "No tasks with a due date in this range.", Foreground = secondaryBrush,
                FontStyle = FontStyles.Italic, Margin = new Thickness(6)
            });
            for (var w = 0; w < unitsToShow; w++) AddCell(TimelineGrid, 0, w + 1, new Border());
            return;
        }

        for (var r = 0; r < rowProjects.Count; r++)
        {
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });

            // Alternating shading, matching the app's existing AlternateRowBrush convention.
            var rowBackground = r % 2 == 1 ? alternateRowBrush : null;

            var projectName = rowProjects[r];
            AddCell(TimelineGrid, r, 0, new TextBlock
            {
                Text = projectName, FontWeight = FontWeights.SemiBold, Foreground = brush,
                Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            }, rowBackground);

            var tasksByUnit = byProject[projectName]
                .GroupBy(c => (c.DueDate!.Value.Date - _windowStart).Days / unitDays)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DueDate).ToList());

            for (var w = 0; w < unitsToShow; w++)
            {
                var cellPanel = new StackPanel { Margin = new Thickness(3) };
                if (tasksByUnit.TryGetValue(w, out var tasks))
                {
                    foreach (var task in tasks)
                    {
                        var parts = new List<string> { task.Title };
                        if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                        parts.Add(task.DueDate!.Value.ToString("MMM d"));

                        var block = new Border
                        {
                            Background = cardBrush,
                            BorderBrush = borderBrush,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(5, 3, 5, 3),
                            Margin = new Thickness(0, 0, 0, 3),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock
                            {
                                Text = string.Join(" - ", parts), Foreground = brush,
                                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                                ToolTip = $"{task.Title}\n{(task.WhoName != "Unassigned" ? $"Who: {task.WhoName}\n" : "")}Due: {task.DueDate:MMM d, yyyy}\n\nDouble-click to open"
                            }
                        };
                        block.MouseLeftButtonDown += (_, args) =>
                        {
                            if (args.ClickCount != 2) return;
                            OpenCardForEdit(task);
                        };
                        cellPanel.Children.Add(block);
                    }
                }
                AddCell(TimelineGrid, r, w + 1, cellPanel, rowBackground);
            }
        }
    }
}
