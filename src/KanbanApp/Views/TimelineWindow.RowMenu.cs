using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Right-clicking a project or goal on the Timeline (its name, or its Summary bar) to change it without leaving
// the Timeline: rename it, edit its milestones, or open the Manage list it lives in.
public partial class TimelineWindow
{
    private ContextMenu? BuildRowMenu(string name)
    {
        // "No Project" and "No Goal" are not entries of the list, so there is nothing to edit.
        if (PlannedItemNamed(name) is not { } item) return null;

        var menu = new ContextMenu();
        var noun = IsByGoal ? "goal" : "project";

        var rename = new MenuItem { Header = "_Rename..." };
        rename.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => RenameRow(name, noun)), DispatcherPriority.Background);
        menu.Items.Add(rename);

        var newTask = new MenuItem { Header = "_New Task Here..." };
        newTask.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => AddTask(item)), DispatcherPriority.Background);
        menu.Items.Insert(0, newTask);

        var milestones = new MenuItem { Header = "_Milestones..." };
        milestones.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            new MilestonesWindow(_viewModel, item) { Owner = this }.ShowDialog();
            BuildGrid();
        }), DispatcherPriority.Background);
        menu.Items.Add(milestones);

        menu.Items.Add(new Separator());

        var manage = new MenuItem { Header = IsByGoal ? "Manage _Goals..." : "Manage _Projects..." };
        manage.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            new ManageListWindow(_viewModel, IsByGoal ? ManagedListKind.Goals(_viewModel) : ManagedListKind.Projects(_viewModel)) { Owner = this }.ShowDialog();
            BuildGrid();
        }), DispatcherPriority.Background);
        menu.Items.Add(manage);
        return menu;
    }

    private void RenameRow(string name, string noun)
    {
        var prompt = new PromptWindow($"Rename {char.ToUpperInvariant(noun[0])}{noun[1..]}", $"New name for \"{name}\":", name, "Rename") { Owner = this };
        if (prompt.ShowDialog() != true) return;

        var newName = prompt.Value.Trim();
        if (newName.Length == 0 || newName == name) return;

        var renamed = IsByGoal
            ? _viewModel.Goals.FirstOrDefault(g => g.Name == name) is { } goal && _viewModel.RenameGoal(goal, newName)
            : _viewModel.Projects.FirstOrDefault(p => p.Name == name) is { } project && _viewModel.RenameProject(project, newName);

        if (!renamed)
        {
            Dialogs.Tell(this, "Name Already Used", $"The name was not changed.\n\nThere is already a {noun} called \"{newName}\".");
            return;
        }

        // An opened row stays open under its new name.
        if (_expanded.Remove(RowKey(name))) _expanded.Add(RowKey(newName));
        _viewModel.SaveTimelineExpanded(_expanded);
        BuildGrid();
    }
}
