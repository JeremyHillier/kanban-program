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
            config.Repoint(currentPath, newPath);
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

    // Set when the user chose another file in Manage Task Files: the board switches once Settings has closed.
    public string? SwitchToPath { get; private set; }

    private void ManageTaskFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TaskFilesWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
        DbPathTextBox.Text = _viewModel.CurrentDbPath;
        if (dialog.CatalogueChanged) _viewModel.RefreshTaskFileName();
        if (dialog.SwitchToPath is null) return;

        SwitchToPath = dialog.SwitchToPath;
        Close(); // asks about unsaved settings as usual; the switch follows
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
