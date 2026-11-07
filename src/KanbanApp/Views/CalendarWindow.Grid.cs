using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Drawing the Calendar: the day cells, the tasks in them, dragging a task to another day, and the No Due Date list.
public partial class CalendarWindow
{
    private const int MonthChipsPerDay = 3;

    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

    private Point _dragStart;
    private CardViewModel? _dragCandidate;

    private void BuildGrid()
    {
        var mode = Mode;
        var (start, end) = CalendarLayout.Range(mode, _anchor);
        RangeLabel.Text = CalendarLayout.Title(mode, _anchor);
        UndoButton.IsEnabled = _viewModel.CanUndo;
        UndoButton.ToolTip = _viewModel.CanUndo ? _viewModel.UndoToolTip : "Nothing to undo";
        FiltersButton.Content = _filter.IsEmpty ? "Filters..." : $"Filters ({_filter.Count})...";
        _syncingPicker = true;
        GoToDatePicker.SelectedDate = _anchor;
        _syncingPicker = false;

        // The tasks to draw: those the filters let through, finished ones only when asked for.
        var doneIds = _viewModel.DoneColumn?.Cards.Select(c => c.Id).ToHashSet() ?? [];
        var includeDone = IncludeDoneCheckBox.IsChecked == true;
        var shown = _viewModel.Columns.SelectMany(c => c.Cards).Where(c => _filter.Matches(c) && (includeDone || !doneIds.Contains(c.Id))).ToList();
        var byDay = shown.Where(c => c.DueDate is not null).GroupBy(c => c.DueDate!.Value.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DueTime ?? "99:99", StringComparer.Ordinal).ThenBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList());

        var weeks = CalendarLayout.Weeks(start, end, weekends: mode == CalendarScale.Day || WeekendsCheckBox.IsChecked == true);
        var columns = weeks.Max(w => w.Count);

        WeekdayHeader.Children.Clear();
        WeekdayHeader.ColumnDefinitions.Clear();
        WeekdayHeader.Visibility = mode == CalendarScale.Day ? Visibility.Collapsed : Visibility.Visible;
        CalendarGrid.Children.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        CalendarGrid.RowDefinitions.Clear();
        for (var c = 0; c < columns; c++)
        {
            WeekdayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        foreach (var _ in weeks) CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = mode == CalendarScale.Month ? 110 : 320 });

        var widest = weeks.First(w => w.Count == columns);
        for (var c = 0; c < widest.Count; c++)
        {
            var name = new TextBlock
            {
                Text = widest[c].ToString("dddd"), HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4),
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            };
            Grid.SetColumn(name, c);
            WeekdayHeader.Children.Add(name);
        }

        for (var r = 0; r < weeks.Count; r++)
        {
            foreach (var day in weeks[r])
            {
                var cell = BuildDayCell(day, mode, byDay.GetValueOrDefault(day.Date) ?? [], doneIds);
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, mode == CalendarScale.Day ? 0 : weeks[r].IndexOf(day));
                CalendarGrid.Children.Add(cell);
            }
        }

        // The side list: tasks with no due date, under the same filters.
        var noDue = shown.Where(c => c.DueDate is null).OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        NoDueList.ItemsSource = noDue;
        NoDueCountText.Text = $"{noDue.Count} task{(noDue.Count == 1 ? "" : "s")}. Drag one onto a day to set its due date.";
        NoDuePanel.Visibility = NoDueCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = NoDueCheckBox.IsChecked == true ? GridLength.Auto : new GridLength(0);
    }

    private UIElement BuildDayCell(DateTime day, CalendarScale mode, List<CardViewModel> tasks, HashSet<int> doneIds)
    {
        var isToday = day.Date == DateTime.Today;
        var cell = new Border
        {
            Margin = new Thickness(1), Padding = new Thickness(4), CornerRadius = new CornerRadius(4), AllowDrop = true, Tag = day.Date,
            BorderThickness = new Thickness(isToday ? 2 : 1), Background = (Brush)FindResource("PanelBackgroundBrush"),
            BorderBrush = (Brush)FindResource(isToday ? "AccentTextBrush" : "PanelBorderBrush"),
            ToolTip = "Double-click to add a task due this day"
        };
        // The days of the month before and after, in Month view, are drawn quieter.
        if (mode == CalendarScale.Month && day.Month != _anchor.Month) cell.Opacity = 0.6;
        cell.DragOver += Cell_DragOver;
        cell.Drop += Cell_Drop;
        cell.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2) return;
            NewTask(day.Date);
            e.Handled = true;
        };

        var panel = new StackPanel();
        var label = new TextBlock
        {
            Text = mode == CalendarScale.Month ? (day.Day == 1 ? day.ToString("MMM d") : day.Day.ToString()) : day.ToString("ddd d"),
            FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 3), HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = (Brush)FindResource(isToday ? "AccentTextBrush" : "PrimaryTextBrush"),
            Cursor = mode == CalendarScale.Day ? Cursors.Arrow : Cursors.Hand, ToolTip = mode == CalendarScale.Day ? null : "Show this day"
        };
        if (mode != CalendarScale.Day)
        {
            label.MouseLeftButtonUp += (_, e) => { ShowDay(day.Date); e.Handled = true; };
            label.MouseEnter += (_, _) => label.TextDecorations = TextDecorations.Underline;
            label.MouseLeave += (_, _) => label.TextDecorations = null;
        }
        panel.Children.Add(label);

        var limit = mode == CalendarScale.Month ? MonthChipsPerDay : int.MaxValue;
        foreach (var task in tasks.Take(limit)) panel.Children.Add(BuildChip(task, doneIds.Contains(task.Id), mode == CalendarScale.Day));
        if (tasks.Count > limit)
        {
            var more = new TextBlock
            {
                Text = $"+{tasks.Count - limit} more", FontSize = 11, Margin = new Thickness(2, 1, 0, 0), Cursor = Cursors.Hand, ToolTip = "Show this day",
                Foreground = (Brush)FindResource("AccentTextBrush")
            };
            more.MouseLeftButtonUp += (_, e) => { ShowDay(day.Date); e.Handled = true; };
            panel.Children.Add(more);
        }

        // In Week and Day view a long day scrolls inside its cell rather than growing the row.
        if (mode == CalendarScale.Month) cell.Child = panel;
        else cell.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = panel };
        return cell;
    }

    // One task in a day: its priority colour (or the card's own colour with a stripe of its priority), the time if it
    // has one, struck through when finished, red-outlined when overdue.
    private Border BuildChip(CardViewModel card, bool done, bool roomy)
    {
        var priorityBrush = _viewModel.Priorities.Brush(card.Priority);
        var cardBrush = card.BackgroundBrush;
        var overdue = CalendarLayout.IsOverdue(card.DueDate, done, DateTime.Today);
        var time = DueTimeParser.Format(card.DueTime);

        var text = new TextBlock
        {
            Text = (time.Length > 0 ? time + "  " : "") + card.Title + (card.IsWaiting ? "  (waiting)" : ""),
            FontSize = roomy ? 13 : 11, TextWrapping = TextWrapping.Wrap,
            Foreground = cardBrush is null ? Brushes.White : (Brush)FindResource("PrimaryTextBrush"),
            TextDecorations = done ? TextDecorations.Strikethrough : null
        };
        var chip = new Border
        {
            Background = cardBrush ?? priorityBrush, CornerRadius = new CornerRadius(3), Padding = new Thickness(5, roomy ? 4 : 2, 5, roomy ? 4 : 2), Margin = new Thickness(0, 0, 0, 2),
            BorderBrush = overdue ? OverdueBrush : priorityBrush,
            BorderThickness = overdue ? new Thickness(2) : cardBrush is null ? new Thickness(0) : new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand, Opacity = done ? 0.65 : 1, Child = text, Tag = card,
            ToolTip = $"{card.Title}\nPriority: {card.Priority}\n{(card.ProjectName is { Length: > 0 } p ? $"Project: {p}\n" : "")}" +
                      $"{(card.WhoName is { Length: > 0 } who && who != "Unassigned" ? $"Who: {who}\n" : "")}" +
                      $"{(done ? "Done\n" : overdue ? "Overdue\n" : "")}Double-click to open. Drag to another day to change the due date."
        };
        chip.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                OpenCardForEdit(card);
                e.Handled = true;
                return;
            }
            _dragStart = e.GetPosition(null);
            _dragCandidate = card;
        };
        chip.MouseLeftButtonUp += (_, _) => _dragCandidate = null;
        chip.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate != card || !MovedFarEnough(e)) return;
            _dragCandidate = null;
            DragDrop.DoDragDrop(chip, new DataObject(DragFormat, card.Id), DragDropEffects.Move);
        };
        chip.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            ShowCardMenu(chip, card);
        };
        return chip;
    }

    private bool MovedFarEnough(MouseEventArgs e)
    {
        var diff = _dragStart - e.GetPosition(null);
        return Math.Abs(diff.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void Cell_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    // Dropping a task on a day makes that its due date (its time stays; a start date after it moves back).
    private void Cell_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DateTime day } || !e.Data.GetDataPresent(DragFormat) || e.Data.GetData(DragFormat) is not int id) return;
        e.Handled = true;
        if (_viewModel.FindCardById(id) is not { } card) return;

        // Deferred: this runs inside the drag's own message loop, and the redraw replaces the cell it came from.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.SetCardDueDate(card, day);
            BuildGrid();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    // ---- The No Due Date list

    private CardViewModel? CardUnderMouse(MouseEventArgs e) =>
        ItemsControl.ContainerFromElement(NoDueList, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: CardViewModel card } ? card : null;

    private void NoDueList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragCandidate = CardUnderMouse(e);
    }

    private void NoDueList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is not { } card || !MovedFarEnough(e)) return;
        _dragCandidate = null;
        DragDrop.DoDragDrop(NoDueList, new DataObject(DragFormat, card.Id), DragDropEffects.Move);
    }

    private void NoDueList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CardUnderMouse(e) is { } card) OpenCardForEdit(card);
    }

    private void NoDueList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (CardUnderMouse(e) is not { } card) return;
        e.Handled = true;
        NoDueList.SelectedItem = card;
        ShowCardMenu(NoDueList, card);
    }
}
