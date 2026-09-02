using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class TimelineWindow : Window
{
    private const int WeeksToShow = 12;

    private readonly MainViewModel _viewModel;
    private DateTime _windowStart;

    public TimelineWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowStart = MondayOf(DateTime.Today);
        BuildGrid();
    }

    private static DateTime MondayOf(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = _windowStart.AddDays(-28);
        BuildGrid();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = _windowStart.AddDays(28);
        BuildGrid();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = MondayOf(DateTime.Today);
        BuildGrid();
    }

    private void IncludeDoneCheckBox_Changed(object sender, RoutedEventArgs e) => BuildGrid();

    // Rebuilds the grid from scratch on every navigation/toggle rather than trying to update it in
    // place - the row/column count changes with the data (only projects with a due task in the
    // visible window get a row), so an incremental update would need the same "figure out which
    // rows/columns are needed" logic anyway.
    private void BuildGrid()
    {
        var brush = (Brush)FindResource("PrimaryTextBrush");
        var secondaryBrush = (Brush)FindResource("SecondaryTextBrush");
        var borderBrush = (Brush)FindResource("CardBorderBrush");
        var cardBrush = (Brush)FindResource("CardBackgroundBrush");
        var panelBrush = (Brush)FindResource("PanelBackgroundBrush");

        var rangeEnd = _windowStart.AddDays(WeeksToShow * 7);
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

        TimelineGrid.Children.Clear();
        TimelineGrid.RowDefinitions.Clear();
        TimelineGrid.ColumnDefinitions.Clear();

        const double projectColWidth = 150;
        const double weekColWidth = 150;

        TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(projectColWidth) });
        for (var w = 0; w < WeeksToShow; w++)
        {
            TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weekColWidth) });
        }

        TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var r = 0; r < rowProjects.Count; r++)
        {
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });
        }

        void AddCell(int row, int col, UIElement content, Brush? background = null)
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
            TimelineGrid.Children.Add(border);
        }

        AddCell(0, 0, new TextBlock
        {
            Text = "Projects", FontWeight = FontWeights.Bold, Foreground = brush,
            Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center
        }, panelBrush);

        for (var w = 0; w < WeeksToShow; w++)
        {
            var weekStart = _windowStart.AddDays(w * 7);
            AddCell(0, w + 1, new TextBlock
            {
                Text = weekStart.ToString("d-MMM"), FontWeight = FontWeights.Bold, Foreground = brush,
                Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }, panelBrush);
        }

        if (rowProjects.Count == 0)
        {
            AddCell(1, 0, new TextBlock
            {
                Text = "No tasks with a due date in this range.", Foreground = secondaryBrush,
                FontStyle = FontStyles.Italic, Margin = new Thickness(6)
            });
            for (var w = 0; w < WeeksToShow; w++) AddCell(1, w + 1, new Border());
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });
            return;
        }

        for (var r = 0; r < rowProjects.Count; r++)
        {
            var projectName = rowProjects[r];
            AddCell(r + 1, 0, new TextBlock
            {
                Text = projectName, FontWeight = FontWeights.SemiBold, Foreground = brush,
                Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            });

            var tasksByWeek = byProject[projectName]
                .GroupBy(c => (c.DueDate!.Value.Date - _windowStart).Days / 7)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DueDate).ToList());

            for (var w = 0; w < WeeksToShow; w++)
            {
                var cellPanel = new StackPanel { Margin = new Thickness(3) };
                if (tasksByWeek.TryGetValue(w, out var tasks))
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
                            Child = new TextBlock
                            {
                                Text = string.Join(" - ", parts), Foreground = brush,
                                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                                ToolTip = $"{task.Title}\n{(task.WhoName != "Unassigned" ? $"Who: {task.WhoName}\n" : "")}Due: {task.DueDate:MMM d, yyyy}"
                            }
                        };
                        cellPanel.Children.Add(block);
                    }
                }
                AddCell(r + 1, w + 1, cellPanel);
            }
        }
    }
}
