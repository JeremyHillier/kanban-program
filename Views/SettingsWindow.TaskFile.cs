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
        Dialogs.Tell(this, "Backup", "Backup created.\n\nIt is in the backups folder, which Open Backups Folder shows.");
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
                ? "\n\nThe old file is removed once the app restarts at the new location."
                : "";
            OfferRestart($"Restart now to use the new location?\n\nThe task file location has been changed. It takes effect when the app restarts.{cleanupNote}", newPath);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Location Not Changed", "The task file location could not be changed. The app keeps using the file it has now.", DialogTone.Error, ex.Message);
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
            Dialogs.Tell(this, "Switch File", "Select a file in the list first.");
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
            Dialogs.Tell(this, "New File",
                "A file with that name already exists, so no new file was made.\n\nChoose a different name, or use Open Existing File to open that one.",
                DialogTone.Warning, dialog.FileName);
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
            Dialogs.Tell(this, "Switch File", "That is already the file that is open.");
            return;
        }

        if (expectExisting && !File.Exists(newPath))
        {
            // Enter keeps to Cancel: a mistyped or moved path should not quietly become an empty file.
            if (!Dialogs.Confirm(this, DialogMessage.AskDanger("File Not Found",
                    "Start a new, empty task file here?\n\nNo file was found at this location. It may have been moved, renamed or deleted, or be on a drive that is not connected.",
                    "Start a New File") with { Detail = newPath }))
                return;
        }

        var config = AppConfig.Load();
        config.SwitchTo(newPath);
        config.Save();

        if (!OfferRestart("Restart now to open this file?\n\nThe app opens it when it restarts.", newPath))
            RefreshRecentFilesList();
    }

    // The task file is now newPath but the app is still on the old one: offers to restart onto it. True when
    // the app is restarting; otherwise Settings shows the new file, which opens next time.
    private bool OfferRestart(string question, string newPath)
    {
        var restart = Dialogs.Confirm(this, new DialogMessage("Restart Required", question)
        {
            Tone = DialogTone.Question, Yes = "Restart Now", No = "Later", Detail = newPath,
        });

        if (restart)
        {
            Process.Start(Environment.ProcessPath!);
            _restarting = true;
            Application.Current.Shutdown();
        }
        else
        {
            DbPathTextBox.Text = newPath;
        }
        return restart;
    }
}
