using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The task file itself: moving it, switching to another (recent, existing or new) file, and
// the automatic backups.
public partial class SettingsWindow
{
    private void AutoBackupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAutoBackupEnabled(AutoBackupCheckBox.IsChecked == true);
    }

    private void BackupRetentionTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(BackupRetentionTextBox.Text.Trim(), out var count))
        {
            BackupRetentionTextBox.Text = _viewModel.BackupRetentionCount.ToString();
            return;
        }

        _viewModel.SetBackupRetentionCount(count);
        BackupRetentionTextBox.Text = _viewModel.BackupRetentionCount.ToString();
    }

    private void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        BackupService.CreateBackup(_viewModel.CurrentDbPath, _viewModel.BackupRetentionCount);
        MessageBox.Show(this, "Backup created.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenBackupsFolder_Click(object sender, RoutedEventArgs e)
    {
        var backupsDir = BackupService.GetBackupsDir(_viewModel.CurrentDbPath);
        Directory.CreateDirectory(backupsDir);
        Process.Start(new ProcessStartInfo(backupsDir) { UseShellExecute = true });
    }

    private void ChangeLocation_Click(object sender, RoutedEventArgs e)
    {
        var currentPath = _viewModel.CurrentDbPath;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Choose Database File Location",
            FileName = Path.GetFileName(currentPath),
            DefaultExt = ".db",
            Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(currentPath)
        };

        if (dialog.ShowDialog() != true) return;

        var newPath = dialog.FileName;
        if (string.Equals(Path.GetFullPath(newPath), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var didCopy = false;
            if (!File.Exists(newPath) && File.Exists(currentPath))
            {
                File.Copy(currentPath, newPath);
                didCopy = true;

                var oldAttachmentsDir = DatabaseService.GetAttachmentsDir(currentPath);
                if (Directory.Exists(oldAttachmentsDir))
                {
                    CopyDirectoryRecursive(oldAttachmentsDir, DatabaseService.GetAttachmentsDir(newPath));
                }
            }

            var config = AppConfig.Load();
            config.DbPath = newPath;
            config.PendingCleanupPath = didCopy ? currentPath : null;
            config.Save();

            var cleanupNote = didCopy
                ? "\n\nThe old database file will be removed automatically once the app restarts at the new location."
                : "";
            var result = MessageBox.Show(
                $"The database location has been updated.{cleanupNote}\n\nThe app needs to restart for this to take effect. Restart now?",
                "Restart Required", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(Environment.ProcessPath!);
                _restarting = true;
                Application.Current.Shutdown();
            }
            else
            {
                DbPathTextBox.Text = newPath;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't update the database location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }
    }

    private record RecentFileEntry(string Path)
    {
        public string FileName => System.IO.Path.GetFileNameWithoutExtension(Path);
        public string FullPath => Path;
    }

    private void RefreshRecentFilesList()
    {
        var config = AppConfig.Load();
        RecentFilesListBox.ItemsSource = config.RecentDbPaths.Select(p => new RecentFileEntry(p)).ToList();
    }

    private void RecentFilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentFilesListBox.SelectedItem is RecentFileEntry entry) SwitchToFile(entry.Path, expectExisting: true);
    }

    private void SwitchToSelected_Click(object sender, RoutedEventArgs e)
    {
        if (RecentFilesListBox.SelectedItem is not RecentFileEntry entry)
        {
            MessageBox.Show(this, "Select a file from the list first.", "Switch File", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SwitchToFile(entry.Path, expectExisting: true);
    }

    private void RemoveFromRecent_Click(object sender, RoutedEventArgs e)
    {
        if (RecentFilesListBox.SelectedItem is not RecentFileEntry entry) return;

        var config = AppConfig.Load();
        config.RecentDbPaths.RemoveAll(p => AppConfig.ArePathsEqual(p, entry.Path));
        config.Save();
        RefreshRecentFilesList();
    }

    private void OpenExistingFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Task File",
            Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(_viewModel.CurrentDbPath)
        };

        if (dialog.ShowDialog() != true) return;

        SwitchToFile(dialog.FileName, expectExisting: true);
    }

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Create New Task File",
            DefaultExt = ".db",
            Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(_viewModel.CurrentDbPath),
            OverwritePrompt = false
        };

        if (dialog.ShowDialog() != true) return;

        if (File.Exists(dialog.FileName))
        {
            MessageBox.Show(this,
                "A file already exists at that location. Choose a different name for the new file, or use \"Open Existing File...\" to open that one instead.",
                "New File", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SwitchToFile(dialog.FileName, expectExisting: false);
    }

    // expectExisting distinguishes "open/switch to a file that should already be there" (Recent
    // list, Open Existing) from "New File", where non-existence is exactly what's wanted - a stale
    // recent path or a mistyped Open path would otherwise silently start a brand new empty file
    // instead of the one the user meant to reopen.
    private void SwitchToFile(string newPath, bool expectExisting)
    {
        if (AppConfig.ArePathsEqual(newPath, _viewModel.CurrentDbPath))
        {
            MessageBox.Show(this, "That's already the current file.", "Switch File", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (expectExisting && !File.Exists(newPath))
        {
            var proceed = MessageBox.Show(this,
                $"No file was found at:\n{newPath}\n\nContinuing will start a brand new, empty file there. Continue?",
                "File Not Found", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        var config = AppConfig.Load();
        config.SwitchTo(newPath);
        config.Save();

        var result = MessageBox.Show(this,
            $"Switched to:\n{newPath}\n\nThe app needs to restart to open this file. Restart now?",
            "Restart Required", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            Process.Start(Environment.ProcessPath!);
            _restarting = true;
            Application.Current.Shutdown();
        }
        else
        {
            DbPathTextBox.Text = newPath;
            RefreshRecentFilesList();
        }
    }
}
