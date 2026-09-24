using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Sub-tasks: the rows, ticking them (done ones sink to the bottom), dragging to reorder, and
// the progress label.
public partial class AddTaskWindow
{
    private void AddSubTaskRow(string title = "", bool isDone = false)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dragHandle = new TextBlock
        {
            Text = "☰",
            FontSize = 12,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            Cursor = Cursors.SizeNS,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Drag to reorder"
        };
        Grid.SetColumn(dragHandle, 0);
        dragHandle.PreviewMouseLeftButtonDown += (_, args) =>
        {
            _subTaskDragStartPoint = args.GetPosition(null);
            _subTaskDragCandidate = row;
        };
        dragHandle.MouseMove += (_, args) =>
        {
            if (args.LeftButton != MouseButtonState.Pressed || !ReferenceEquals(_subTaskDragCandidate, row)) return;

            var currentPosition = args.GetPosition(null);
            var diff = _subTaskDragStartPoint - currentPosition;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(dragHandle, row, DragDropEffects.Move);
            _subTaskDragCandidate = null;
            // Safety net: DragOver/Drop normally clear this, but a drag that ends outside the panel
            // (dropped elsewhere, or cancelled with Escape) wouldn't otherwise get a chance to.
            SubTaskInsertionIndicator.Visibility = Visibility.Collapsed;
        };

        var checkBox = new CheckBox
        {
            IsChecked = isDone,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        checkBox.Checked += SubTaskCheckBox_Changed;
        checkBox.Unchecked += SubTaskCheckBox_Changed;
        Grid.SetColumn(checkBox, 1);

        var textBox = new TextBox
        {
            Text = title,
            Padding = new Thickness(6, 3, 6, 3),
            Style = (Style)FindResource("HoverTextBoxStyle"),
            Background = (Brush)FindResource("InputBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            // Matches the Task and Notes fields' SpellCheck.IsEnabled="True" Language="en-US".
            // Set here rather than in XAML because these rows are built in code, one per sub-task.
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-US")
        };
        SpellCheck.SetIsEnabled(textBox, true);
        Grid.SetColumn(textBox, 2);

        var deleteButton = new Button
        {
            Content = "×",
            Width = 22,
            Height = 22,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(0),
            Background = (Brush)FindResource("ButtonBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            ToolTip = "Remove sub-task"
        };
        Grid.SetColumn(deleteButton, 3);
        deleteButton.Click += (_, _) =>
        {
            SubTasksPanel.Children.Remove(row);
            UpdateSubTaskProgressLabel();
        };

        row.Children.Add(dragHandle);
        row.Children.Add(checkBox);
        row.Children.Add(textBox);
        row.Children.Add(deleteButton);
        SubTasksPanel.Children.Add(row);

        if (string.IsNullOrEmpty(title))
        {
            textBox.Focus();
        }

        UpdateSubTaskProgressLabel();
    }

    private void SubTaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSubTaskProgressLabel();

        // Deferred via BeginInvoke: reordering the panel while this CheckBox's own Checked/Unchecked
        // handler is still dispatching would tear down its row mid-event - the same WPF deadlock
        // documented throughout this codebase (see ReminderWindow.MarkDoneCheckBox_Checked).
        Dispatcher.BeginInvoke(new Action(ReorderSubTasksByDoneState), DispatcherPriority.Background);
    }

    // Stable sort: not-done rows first, done rows last, preserving each group's existing relative
    // order (LINQ OrderBy is a stable sort) so completed sub-tasks settle at the bottom without
    // otherwise reshuffling anything - including rows the user just manually dragged into place.
    private void ReorderSubTasksByDoneState()
    {
        var sorted = SubTasksPanel.Children.OfType<Grid>()
            .OrderBy(row => ((CheckBox)row.Children[1]).IsChecked == true)
            .ToList();

        SubTasksPanel.Children.Clear();
        foreach (var row in sorted)
        {
            SubTasksPanel.Children.Add(row);
        }
    }

    private void SubTasksPanel_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (e.Data.GetData(typeof(Grid)) is not Grid draggedRow)
        {
            e.Effects = DragDropEffects.None;
            SubTaskInsertionIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        e.Effects = DragDropEffects.Move;
        var (_, indicatorY) = GetSubTaskDropTarget(e.GetPosition(SubTasksPanel), draggedRow);
        SubTaskInsertionIndicator.Margin = new Thickness(0, indicatorY, 0, 0);
        SubTaskInsertionIndicator.Visibility = Visibility.Visible;
    }

    private void SubTasksPanel_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave fires spuriously whenever the mouse crosses onto a child element that isn't
        // itself drop-enabled (a row's TextBox/CheckBox/delete button) - the immediate hit-test
        // target briefly stops being "droppable" even though the mouse is still well within the
        // panel, which was making the indicator flicker as it tracked the cursor across row content.
        // Only actually hide it once the mouse has genuinely left the panel's bounds.
        var position = e.GetPosition(SubTasksPanel);
        if (position.X >= 0 && position.X <= SubTasksPanel.ActualWidth &&
            position.Y >= 0 && position.Y <= SubTasksPanel.ActualHeight)
        {
            return;
        }

        SubTaskInsertionIndicator.Visibility = Visibility.Collapsed;
    }

    private void SubTasksPanel_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        SubTaskInsertionIndicator.Visibility = Visibility.Collapsed;
        if (e.Data.GetData(typeof(Grid)) is not Grid draggedRow) return;

        var dropPosition = e.GetPosition(SubTasksPanel);
        var (newIndex, _) = GetSubTaskDropTarget(dropPosition, draggedRow);

        // Deferred via BeginInvoke: same reasoning as SubTaskCheckBox_Changed above - this handler
        // still runs nested inside DoDragDrop's own message loop when Drop fires (Drop fires before
        // DoDragDrop returns to the drag handle's MouseMove), so mutating SubTasksPanel.Children here
        // immediately carries the same risk as mutating it from inside a Checked/Click handler.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var children = SubTasksPanel.Children;
            var oldIndex = children.IndexOf(draggedRow);
            if (oldIndex < 0) return;

            children.RemoveAt(oldIndex);
            var insertAt = newIndex;
            if (insertAt > oldIndex) insertAt--;
            children.Insert(Math.Clamp(insertAt, 0, children.Count), draggedRow);
        }), DispatcherPriority.Background);
    }

    // Returns both where a drop would land (Index, in "before removal" Children-count space - see
    // the adjustment in SubTasksPanel_Drop above) and the Y position (relative to SubTasksPanel) for
    // the insertion-line indicator, so DragOver and Drop always agree on the same target.
    private (int Index, double IndicatorY) GetSubTaskDropTarget(Point positionInPanel, Grid draggedRow)
    {
        var rows = SubTasksPanel.Children.OfType<Grid>().Where(r => !ReferenceEquals(r, draggedRow)).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var top = rows[i].TranslatePoint(new Point(0, 0), SubTasksPanel).Y;
            if (positionInPanel.Y < top + rows[i].ActualHeight / 2)
            {
                return (SubTasksPanel.Children.IndexOf(rows[i]), top);
            }
        }

        var bottom = rows.Count > 0
            ? rows[^1].TranslatePoint(new Point(0, 0), SubTasksPanel).Y + rows[^1].ActualHeight
            : 0;
        return (SubTasksPanel.Children.Count, bottom);
    }

    private void UpdateSubTaskProgressLabel()
    {
        var checkBoxes = SubTasksPanel.Children.OfType<Grid>()
            .Select(row => (CheckBox)row.Children[1])
            .ToList();

        if (checkBoxes.Count == 0)
        {
            SubTaskProgressLabel.Text = string.Empty;
            return;
        }

        var done = checkBoxes.Count(cb => cb.IsChecked == true);
        SubTaskProgressLabel.Text = $"{done}/{checkBoxes.Count} ({done * 100 / checkBoxes.Count}%)";
    }

    private void AddSubTaskRow_Click(object sender, RoutedEventArgs e)
    {
        AddSubTaskRow();
    }
}
