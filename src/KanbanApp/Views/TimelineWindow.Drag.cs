using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Dragging on the Timeline changes dates: a task's box moves the whole task, the end of its line changes its due
// date, a milestone's line and a target's diamond change theirs. Every change is saved at once and can be taken
// back with Undo (Ctrl+Z). Summary bars are not dragged - they are worked out from the tasks under them.
public partial class TimelineWindow
{
    // What a drag measures against: the row's lane grid (its column 0 starts at x = 0) and the columns it was drawn with.
    private sealed record Surface(Grid Lane, double ColumnWidth, IReadOnlyList<DateTime> UnitStarts, DateTime RangeEnd);

    // What has been dragged in this window, newest last; each entry puts its change back.
    private readonly Stack<(string Label, Action Undo)> _undo = new();

    private void PushUndo(string label, Action undo)
    {
        _undo.Push((label, undo));
        UpdateUndoButton();
    }

    // Edits made another way (the task window, the milestones list) could make an old Undo overwrite them.
    private void ClearUndo()
    {
        _undo.Clear();
        UpdateUndoButton();
    }

    private void UpdateUndoButton()
    {
        UndoButton.IsEnabled = _undo.Count > 0;
        UndoButton.ToolTip = _undo.Count > 0 ? $"Undo: {_undo.Peek().Label} (Ctrl+Z)" : "Nothing to undo. Dates you drag here can be undone.";
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLast();

    private void UndoLast()
    {
        if (_undo.Count == 0) return;

        _undo.Pop().Undo();
        UpdateUndoButton();
        BuildGrid();
    }

    // Lets `handle` be dragged sideways to a new day. `anchor` is the day it stands on now; with `edge` it is a thing
    // that sits at the end of a day (a due date, target or milestone), without it the start of one (a box, which moves
    // as a whole). Nothing happens until the mouse has moved a few pixels, so a click or double-click still just clicks.
    // `mover`, when given, follows the mouse a day at a time; otherwise a line shows where it will land. `describe` words
    // the new day in a small label by the mouse, and `commit` is told the chosen day when the mouse is let go.
    private void MakeDraggable(FrameworkElement handle, Surface s, DateTime anchor, bool edge, DateTime? earliest, FrameworkElement? mover,
        Func<DateTime, string> describe, Action<DateTime> commit)
    {
        var anchorAt = edge ? TimelineLayout.EndOffset(anchor, s.UnitStarts, s.RangeEnd) : TimelineLayout.Offset(anchor, s.UnitStarts, s.RangeEnd);
        Point? down = null;
        var dragging = false;
        var grab = 0.0;
        var current = anchor;
        Popup? tip = null;
        TextBlock? tipText = null;
        Rectangle? ghost = null;

        DateTime DayFor(double x)
        {
            var position = x / s.ColumnWidth - grab;
            var day = TimelineLayout.DayNear(position, edge, s.UnitStarts, s.RangeEnd);
            return edge && earliest is { } first && day < first ? first : day;
        }

        void Show(Point at)
        {
            current = DayFor(at.X);
            var snapped = edge ? TimelineLayout.EndOffset(current, s.UnitStarts, s.RangeEnd) : TimelineLayout.Offset(current, s.UnitStarts, s.RangeEnd);

            if (mover is not null) mover.RenderTransform = new TranslateTransform((snapped - anchorAt) * s.ColumnWidth, 0);
            else if (ghost is not null) ghost.Margin = new Thickness(snapped * s.ColumnWidth - 1, 0, 0, 0);

            if (tip is not null && tipText is not null)
            {
                tipText.Text = describe(current);
                tip.HorizontalOffset = at.X + 14;
                tip.VerticalOffset = at.Y + 20;
            }
        }

        void Begin()
        {
            dragging = true;
            if (mover is null)
            {
                ghost = new Rectangle
                {
                    Width = 2, Fill = (Brush)FindResource("PrimaryTextBrush"), Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Left,
                    IsHitTestVisible = false
                };
                Panel.SetZIndex(ghost, 50);
                Grid.SetRowSpan(ghost, Math.Max(1, s.Lane.RowDefinitions.Count));
                Grid.SetColumnSpan(ghost, Math.Max(1, s.Lane.ColumnDefinitions.Count));
                s.Lane.Children.Add(ghost);
            }

            tipText = new TextBlock { Foreground = (Brush)FindResource("PrimaryTextBrush"), FontSize = 12, FontWeight = FontWeights.SemiBold };
            tip = new Popup
            {
                PlacementTarget = s.Lane, Placement = PlacementMode.Relative, AllowsTransparency = true, IsHitTestVisible = false,
                Child = new Border
                {
                    Background = (Brush)FindResource("PanelBackgroundBrush"), BorderBrush = (Brush)FindResource("CardBorderBrush"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(7, 3, 7, 3), Child = tipText
                },
                IsOpen = true
            };
        }

        void Finish()
        {
            if (tip is not null) tip.IsOpen = false;
            if (ghost is not null) s.Lane.Children.Remove(ghost);
            if (mover is not null) mover.RenderTransform = null;
            tip = null;
            ghost = null;
            dragging = false;
            down = null;
        }

        handle.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 1) return;

            down = e.GetPosition(s.Lane);
            grab = down.Value.X / s.ColumnWidth - anchorAt;
            current = anchor;
            handle.CaptureMouse();
        };

        handle.MouseMove += (_, e) =>
        {
            if (down is not { } start || !handle.IsMouseCaptured) return;

            var at = e.GetPosition(s.Lane);
            if (!dragging)
            {
                if (Math.Abs(at.X - start.X) < 4) return;
                Begin();
            }
            Show(at);
        };

        handle.MouseLeftButtonUp += (_, _) =>
        {
            if (down is null) return;

            var moved = dragging;
            var chosen = current;
            Finish();
            handle.ReleaseMouseCapture();
            if (moved && chosen != anchor) commit(chosen);
        };

        // Windows took the mouse away part way (Alt+Tab, a dialog): the drag is dropped, nothing changes.
        handle.LostMouseCapture += (_, _) =>
        {
            if (down is not null) Finish();
        };
    }

    private static string RangeText(DateTime? start, DateTime? due) => (start, due) switch
    {
        ({ } a, { } b) when a != b => $"{a:MMM d} to {b:MMM d}",
        ({ } a, null) => $"starts {a:MMM d}",
        (_, { } b) => b.ToString("MMM d"),
        _ => string.Empty
    };

    // The whole task moves by as many days as the box was dragged: its start and its due date together.
    private void MakeTaskMovable(FrameworkElement box, FrameworkElement mover, Surface s, CardViewModel task, DateTime anchor)
    {
        MakeDraggable(box, s, anchor, edge: false, earliest: null, mover,
            describe: day => RangeText(task.StartDate?.AddDays((day - anchor).Days), task.DueDate?.AddDays((day - anchor).Days)),
            commit: day =>
            {
                var days = (day - anchor).Days;
                var oldStart = task.StartDate;
                var oldDue = task.DueDate;
                _viewModel.SetCardDates(task, oldStart?.AddDays(days), oldDue?.AddDays(days));
                PushUndo($"moved \"{task.Title}\"", () => RestoreDates(task, oldStart, oldDue));
                BuildGrid();
            });
    }

    // The end of a task's line: only the due date changes, and never to before its start.
    private void MakeDueDateDraggable(FrameworkElement grip, Surface s, CardViewModel task, DateTime due)
    {
        MakeDraggable(grip, s, due, edge: true, earliest: task.StartDate?.Date, mover: null,
            describe: day => $"Due {day:MMM d, yyyy}",
            commit: day =>
            {
                var oldStart = task.StartDate;
                var oldDue = task.DueDate;
                _viewModel.SetCardDates(task, oldStart, day);
                PushUndo($"changed the due date of \"{task.Title}\"", () => RestoreDates(task, oldStart, oldDue));
                BuildGrid();
            });
    }

    private void RestoreDates(CardViewModel task, DateTime? start, DateTime? due)
    {
        if (_viewModel.Columns.Any(c => c.Cards.Contains(task))) _viewModel.SetCardDates(task, start, due);
    }

    private void MakeMilestoneDraggable(FrameworkElement line, Surface s, Models.Milestone milestone)
    {
        var old = milestone.Date;
        MakeDraggable(line, s, old.Date, edge: true, earliest: null, mover: null,
            describe: day => $"{milestone.Description}: {day:MMM d, yyyy}",
            commit: day =>
            {
                _viewModel.ChangeMilestone(milestone, milestone.Description, day);
                PushUndo($"moved the milestone \"{milestone.Description}\"", () => _viewModel.ChangeMilestone(milestone, milestone.Description, old));
                BuildGrid();
            });
    }

    private void MakeTargetDraggable(FrameworkElement diamond, Surface s, IPlannedItem planned, DateTime target)
    {
        void Set(DateTime? day)
        {
            if (planned is GoalViewModel goal) _viewModel.SetGoalTarget(goal, day);
            else if (planned is ProjectViewModel project) _viewModel.SetProjectTarget(project, day);
        }

        MakeDraggable(diamond, s, target.Date, edge: true, earliest: null, mover: null,
            describe: day => $"Target {day:MMM d, yyyy}",
            commit: day =>
            {
                Set(day);
                PushUndo($"changed the target of {planned.Name}", () => Set(target));
                BuildGrid();
            });
    }
}
