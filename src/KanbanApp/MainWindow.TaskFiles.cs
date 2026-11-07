using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The Task file button at the top of the button column: a menu of the task files on this PC, the
// open one ticked, and Manage Task Files. Opening another file replaces this whole window (App.SwitchTaskFile).
public partial class MainWindow
{
    internal DatabaseService Db => _db;

    private void TaskFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target || DataContext is not MainViewModel viewModel) return;
        var config = AppConfig.Load();
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        foreach (var file in config.FilesByRecency)
        {
            var isOpen = AppConfig.ArePathsEqual(file.Path, viewModel.CurrentDbPath);
            var exists = File.Exists(file.Path);
            var item = new MenuItem
            {
                Header = MenuText(file.DisplayName) + (exists ? "" : "  (not found)"),
                IsChecked = isOpen, IsEnabled = exists, ToolTip = file.Path,
            };
            if (!isOpen) item.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => SwitchTaskFile(file.Path)), DispatcherPriority.Background);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var manage = new MenuItem { Header = "_Manage Task Files..." };
        manage.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => OpenTaskFiles(showTemplates: false)), DispatcherPriority.Background);
        menu.Items.Add(manage);
        menu.IsOpen = true;
    }

    internal void OpenTaskFiles(bool showTemplates)
    {
        if (DataContext is not MainViewModel viewModel) return;
        var dialog = new TaskFilesWindow(viewModel, showTemplates) { Owner = this };
        dialog.ShowDialog();
        if (dialog.SwitchToPath is { } path) SwitchTaskFile(path);
        else if (dialog.CatalogueChanged) viewModel.RefreshTaskFileName();
    }

    // Another task file in place of this one: the board is rebuilt on it; this window closes.
    internal void SwitchTaskFile(string path)
    {
        if (Application.Current is App app) app.SwitchTaskFile(path, this);
    }
}
