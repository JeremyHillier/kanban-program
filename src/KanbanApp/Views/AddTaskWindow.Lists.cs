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

// The Project, Goal and Flag choices, and adding to or clearing them from here.
public partial class AddTaskWindow
{
    private void RebuildProjectItems(ProjectViewModel? autoSelect = null)
    {
        var items = _viewModel.Projects.Where(p => p.IsActive).ToList();
        if (autoSelect is not null && !items.Any(p => p.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        ProjectComboBox.ItemsSource = items;
        ProjectComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(p => p.Id == autoSelect.Id);
    }

    private void RebuildGoalItems(GoalViewModel? autoSelect = null)
    {
        var items = _viewModel.Goals.Where(g => g.IsActive).ToList();
        if (autoSelect is not null && !items.Any(g => g.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        GoalComboBox.ItemsSource = items;
        GoalComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(g => g.Id == autoSelect.Id);
    }

    private void RebuildFlagCheckboxes(int? autoCheckFlagId = null, IEnumerable<int>? forceCheckedIds = null)
    {
        var checkedIds = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => ((FlagViewModel)cb.Tag).Id)
            .ToHashSet();

        if (forceCheckedIds is not null)
        {
            checkedIds.UnionWith(forceCheckedIds);
        }

        FlagsPanel.Children.Clear();
        foreach (var flag in _viewModel.Flags.Where(f => f.IsActive || checkedIds.Contains(f.Id)))
        {
            FlagsPanel.Children.Add(new CheckBox
            {
                Content = flag.Name,
                Tag = flag,
                IsChecked = checkedIds.Contains(flag.Id) || flag.Id == autoCheckFlagId,
                Margin = new Thickness(0, 0, 16, 8),
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            });
        }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Project", "Project name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var added = _viewModel.AddProject(dialog.Value);
        ManagedListPrompts.ShowAddNotice(this, added, "project", dialog.Value);
        RebuildProjectItems(added.Item);
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        ProjectComboBox.SelectedIndex = -1;
    }

    private void NewGoal_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Goal", "Goal name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var added = _viewModel.AddGoal(dialog.Value);
        ManagedListPrompts.ShowAddNotice(this, added, "goal", dialog.Value);
        RebuildGoalItems(added.Item);
    }

    private void DeleteGoal_Click(object sender, RoutedEventArgs e)
    {
        GoalComboBox.SelectedIndex = -1;
    }

    private void NewFlag_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Flag", "Flag name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var added = _viewModel.AddFlag(dialog.Value);
        ManagedListPrompts.ShowAddNotice(this, added, "flag", dialog.Value);
        RebuildFlagCheckboxes(added.Item?.Id);
    }
}
