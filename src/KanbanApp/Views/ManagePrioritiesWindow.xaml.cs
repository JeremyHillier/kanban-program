using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The task file's priority list: add to it, rename, recolour, reorder, choose the default and
// delete. Every change is made straight away (like the other Manage screens) and the board behind
// follows. Each line shows the badge as it looks on a card, and how many tasks have that priority.
public partial class ManagePrioritiesWindow : Window
{
    private readonly MainViewModel _viewModel;

    private sealed record Row(MainViewModel.PriorityEntry Entry, Brush Brush);

    public ManagePrioritiesWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Reload();
    }

    private Row? Selected => PriorityListBox.SelectedItem as Row;

    private void Reload(string? select = null)
    {
        select ??= Selected?.Entry.Name;
        var rows = _viewModel.PriorityEntries.Select(e => new Row(e, _viewModel.Priorities.BrushForColor(e.Color))).ToList();
        PriorityListBox.ItemsSource = rows;
        PriorityListBox.SelectedItem = rows.FirstOrDefault(r => string.Equals(r.Entry.Name, select?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (PriorityListBox.SelectedItem is not null) PriorityListBox.ScrollIntoView(PriorityListBox.SelectedItem);
        RefreshButtons();
    }

    private void PriorityListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshButtons();

    private void RefreshButtons()
    {
        var row = Selected;
        var index = PriorityListBox.SelectedIndex;
        RenameButton.IsEnabled = ColorButton.IsEnabled = row is not null;
        MoveUpButton.IsEnabled = row is not null && index > 0;
        MoveDownButton.IsEnabled = row is not null && index < PriorityListBox.Items.Count - 1;
        DefaultButton.IsEnabled = row is { Entry.IsDefault: false };
        DeleteButton.IsEnabled = row is not null && _viewModel.Priorities.CanRemove(row.Entry.Name);
    }

    private void PriorityListBox_KeyDown(object sender, KeyEventArgs e)
    {
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Delete && DeleteButton.IsEnabled) Delete_Click(sender, e);
        else if (key == Key.F2) Rename_Click(sender, e);
        else if (alt && key == Key.Up) MoveUp_Click(sender, e);
        else if (alt && key == Key.Down) MoveDown_Click(sender, e);
        else return;
        e.Handled = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Priority", "Priority name") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        if (!_viewModel.AddPriority(dialog.Value))
        {
            Dialogs.Tell(this, "Priorities", $"There is already a priority called \"{dialog.Value.Trim()}\".");
        }

        Reload(dialog.Value);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;

        var label = row.Entry.TaskCount == 0 ? "New name" : $"New name (also changes {TaskWord(row.Entry.TaskCount)})";
        var dialog = new PromptWindow("Rename Priority", label, row.Entry.Name, "Save") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        var newName = dialog.Value.Trim();
        if (newName == row.Entry.Name) return;

        if (!_viewModel.RenamePriority(row.Entry.Name, newName))
        {
            Dialogs.Tell(this, "Priorities", $"There is already a priority called \"{newName}\", so the name was not changed.");
            return;
        }

        Reload(newName);
    }

    // The colours on offer, each shown as its badge.
    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;

        var menu = new ContextMenu { PlacementTarget = ColorButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var color in PriorityColors.All)
        {
            var swatch = new Border
            {
                Background = _viewModel.Priorities.BrushForColor(color.Key), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2, 8, 2), MinWidth = 70,
                Child = new TextBlock { Text = color.Key, Foreground = Brushes.White, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }
            };
            var item = new MenuItem { Header = swatch, IsChecked = color.Key == row.Entry.Color };
            var key = color.Key;
            item.Click += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                _viewModel.SetPriorityColor(row.Entry.Name, key);
                Reload(row.Entry.Name);
            });
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int places)
    {
        if (Selected is not { } row) return;
        if (_viewModel.MovePriority(row.Entry.Name, places)) Reload(row.Entry.Name);
        PriorityListBox.Focus();
    }

    private void MakeDefault_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        if (_viewModel.SetDefaultPriority(row.Entry.Name)) Reload(row.Entry.Name);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || !_viewModel.Priorities.CanRemove(row.Entry.Name)) return;

        if (row.Entry.TaskCount > 0)
        {
            var one = row.Entry.TaskCount == 1;
            var fallback = _viewModel.Priorities.Default;
            if (!Dialogs.Confirm(this, DialogMessage.AskDanger("Delete Priority",
                    $"Delete \"{row.Entry.Name}\" and change {TaskWord(row.Entry.TaskCount)} to {fallback}?\n\n" +
                    $"• {(one ? "The task that has" : "Every task that has")} this priority, including archived tasks, will become {fallback}, the default priority.\n" +
                    "• Undo does not bring a deleted priority back.",
                    "Delete")))
            {
                return;
            }
        }

        _viewModel.DeletePriority(row.Entry.Name);
        Reload(select: _viewModel.Priorities.Default);
    }

    private static string TaskWord(int count) => count == 1 ? "1 task" : $"{count} tasks";
}
