using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The Calendar: tasks on the days they are due, as a Month, a Week or a Day. It uses the same task screen, right-click
// menu and filters as the Gantt view and the board, and changes the same tasks (drag one to another day to change its
// due date). The drawing is in CalendarWindow.Grid.cs.
public partial class CalendarWindow : Window
{
    internal const string DragFormat = "KanbanCalendarCard";

    private readonly MainViewModel _viewModel;
    private DateTime _anchor = DateTime.Today;
    private TimelineFilter _filter;
    private bool _initializing = true;
    private bool _syncingPicker;

    public CalendarWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _viewModel = viewModel;
        WindowPlacementMemory.Attach(this, viewModel, "Calendar");
        _filter = viewModel.TimelineFilter;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { NewTask(null); e.Handled = true; return; }
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase)
            {
                Undo_Click(this, new RoutedEventArgs());
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

        (viewModel.CalendarView switch { CalendarScale.Week => WeekRadio, CalendarScale.Day => DayRadio, _ => MonthRadio }).IsChecked = true;
        WeekendsCheckBox.IsChecked = viewModel.CalendarShowWeekends;
        IncludeDoneCheckBox.IsChecked = viewModel.CalendarIncludeDone;
        NoDueCheckBox.IsChecked = viewModel.CalendarShowNoDueDate;
        _initializing = false;
        BuildGrid();
    }

    private CalendarScale Mode => WeekRadio.IsChecked == true ? CalendarScale.Week : DayRadio.IsChecked == true ? CalendarScale.Day : CalendarScale.Month;

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _anchor = CalendarLayout.Step(Mode, _anchor, -1);
        BuildGrid();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _anchor = CalendarLayout.Step(Mode, _anchor, 1);
        BuildGrid();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _anchor = DateTime.Today;
        BuildGrid();
    }

    private void GoToDate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPicker || GoToDatePicker.SelectedDate is not { } date) return;
        _anchor = date;
        BuildGrid();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _viewModel.SaveCalendarView(Mode);
        BuildGrid();
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _viewModel.SaveCalendarShowWeekends(WeekendsCheckBox.IsChecked == true);
        _viewModel.SaveCalendarIncludeDone(IncludeDoneCheckBox.IsChecked == true);
        _viewModel.SaveCalendarShowNoDueDate(NoDueCheckBox.IsChecked == true);
        BuildGrid();
    }

    // A day's number, or "+3 more", takes the calendar to that day.
    private void ShowDay(DateTime day)
    {
        _anchor = day;
        if (DayRadio.IsChecked == true) BuildGrid();
        else DayRadio.IsChecked = true; // Mode_Changed redraws
    }

    private void BodyScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The days fill the window when they fit, and scroll when they do not (each row keeps a minimum height).
        if (BodyScroll.ViewportHeight > 0) CalendarGrid.MinHeight = BodyScroll.ViewportHeight;
    }

    private void Filters_Click(object sender, RoutedEventArgs e)
    {
        var window = new TimelineFiltersWindow(_viewModel, _filter) { Owner = this };
        if (window.ShowDialog() != true) return;

        _filter = window.Result;
        _viewModel.SaveTimelineFilter(_filter);
        BuildGrid();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanUndo) return;
        _viewModel.Undo();
        BuildGrid();
    }

    private void NewTask_Click(object sender, RoutedEventArgs e) => NewTask(null);

    // A new task, due on `day` when one is given (a double-click on a day).
    private void NewTask(DateTime? day)
    {
        var dialog = new AddTaskWindow(_viewModel) { Owner = this };
        if (day is { } due) dialog.PreselectDueDate(due);
        if (dialog.ShowDialog() == true) dialog.AddTo(_viewModel);
        BuildGrid();
    }

    // The same task screen the board and the Gantt view open; redrawn afterwards whether it was saved or not.
    private void OpenCardForEdit(CardViewModel card)
    {
        if (_viewModel.ColumnOf(card) is not { } column) return;

        var dialog = new AddTaskWindow(_viewModel, card, column) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            dialog.SaveTo(_viewModel, card);
            if (dialog.MarkDoneRequested && Owner is MainWindow board)
            {
                board.MarkDoneFromElsewhere(card, _viewModel, BuildGrid);
                return;
            }
        }
        BuildGrid();
    }

    // The board's right-click menu for the task, then a redraw for whatever it changed.
    private void ShowCardMenu(FrameworkElement target, CardViewModel card)
    {
        if (Owner is MainWindow board) board.ShowCardMenu(target, card, _viewModel, () => OpenCardForEdit(card), BuildGrid);
    }
}
