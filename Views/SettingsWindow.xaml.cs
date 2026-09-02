using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;

        foreach (var column in viewModel.Columns)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = column.Name,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush")
            });
            var textBox = new TextBox
            {
                Text = column.DisplayName,
                Width = 220,
                Padding = new Thickness(6),
                Tag = column,
                Background = (System.Windows.Media.Brush)FindResource("InputBackgroundBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("CardBorderBrush")
            };
            textBox.LostFocus += ColumnDisplayNameTextBox_LostFocus;
            row.Children.Add(textBox);
            ColumnNamesPanel.Children.Add(row);
        }

        ButtonsOnRightCheckBox.IsChecked = viewModel.IsButtonsOnRight;
        ColumnWidthTextBox.Text = viewModel.ColumnWidth.ToString();
        DbPathTextBox.Text = viewModel.CurrentDbPath;

        ShowSplashCheckBox.IsChecked = viewModel.ShowSplash;
        foreach (System.Windows.Controls.ComboBoxItem item in SplashDelayComboBox.Items)
        {
            if (item.Tag is string tag && int.TryParse(tag, out var ms) && ms == viewModel.SplashDelayMs)
            {
                SplashDelayComboBox.SelectedItem = item;
                break;
            }
        }
        SplashDelayComboBox.SelectedItem ??= SplashDelayComboBox.Items[1];

        ExportPathTextBox.Text = viewModel.DefaultExportPath;
        ImportPathTextBox.Text = viewModel.DefaultImportPath;
        LinkedFilesPathTextBox.Text = viewModel.LinkedFilesDefaultPath;

        StartFullScreenCheckBox.IsChecked = viewModel.StartFullScreen;
        ConfirmDeleteCheckBox.IsChecked = viewModel.ConfirmDelete;
        ConfirmArchiveCheckBox.IsChecked = viewModel.ConfirmArchive;
        AddNoteOnCompleteCheckBox.IsChecked = viewModel.AddNoteOnComplete;
        ShowDueRemindersCheckBox.IsChecked = viewModel.ShowDueReminders;
        RememberLastViewCheckBox.IsChecked = viewModel.RememberLastView;
        ShowWhatsNewCheckBox.IsChecked = viewModel.ShowWhatsNew;
    }

    private void ShowWhatsNewCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowWhatsNew(ShowWhatsNewCheckBox.IsChecked == true);
    }

    private void ShowWhatsNew_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WhatsNewWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();

        // The dialog has its own "show after every update" checkbox, so mirror any change back.
        ShowWhatsNewCheckBox.IsChecked = _viewModel.ShowWhatsNew;
    }

    private void ColumnDisplayNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { Tag: ColumnViewModel column } textBox) return;

        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            textBox.Text = column.DisplayName;
            return;
        }

        _viewModel.RenameColumnDisplayName(column, textBox.Text);
        textBox.Text = column.DisplayName;
    }

    private void StartFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetStartFullScreen(StartFullScreenCheckBox.IsChecked == true);
    }

    private void ConfirmDeleteCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetConfirmDelete(ConfirmDeleteCheckBox.IsChecked == true);
    }

    private void ConfirmArchiveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetConfirmArchive(ConfirmArchiveCheckBox.IsChecked == true);
    }

    private void AddNoteOnCompleteCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAddNoteOnComplete(AddNoteOnCompleteCheckBox.IsChecked == true);
    }

    private void ShowDueRemindersCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowDueReminders(ShowDueRemindersCheckBox.IsChecked == true);
    }

    private void RememberLastViewCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRememberLastView(RememberLastViewCheckBox.IsChecked == true);
    }

    private void ButtonsOnRightCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var wantRight = ButtonsOnRightCheckBox.IsChecked == true;
        if (wantRight != _viewModel.IsButtonsOnRight)
        {
            _viewModel.ToggleButtonPosition();
        }
    }

    private void ColumnWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ColumnWidthTextBox.Text.Trim(), out var width))
        {
            ColumnWidthTextBox.Text = _viewModel.ColumnWidth.ToString();
            return;
        }

        _viewModel.SetColumnWidth(width);
        ColumnWidthTextBox.Text = _viewModel.ColumnWidth.ToString();
    }

    private void ShowSplashCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowSplash(ShowSplashCheckBox.IsChecked == true);
    }

    private void SplashDelayComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SplashDelayComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } &&
            int.TryParse(tag, out var ms))
        {
            _viewModel.SetSplashDelayMs(ms);
        }
    }

    private void ExportPathTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.SetDefaultExportPath(ExportPathTextBox.Text.Trim());
    }

    private void ImportPathTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.SetDefaultImportPath(ImportPathTextBox.Text.Trim());
    }

    private void LinkedFilesPathTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.SetLinkedFilesDefaultPath(LinkedFilesPathTextBox.Text.Trim());
    }

    private void BrowseExportPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose Default Export Folder",
            InitialDirectory = Directory.Exists(ExportPathTextBox.Text) ? ExportPathTextBox.Text : null
        };

        if (dialog.ShowDialog() != true) return;

        ExportPathTextBox.Text = dialog.FolderName;
        _viewModel.SetDefaultExportPath(dialog.FolderName);
    }

    private void BrowseImportPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose Default Import Folder",
            InitialDirectory = Directory.Exists(ImportPathTextBox.Text) ? ImportPathTextBox.Text : null
        };

        if (dialog.ShowDialog() != true) return;

        ImportPathTextBox.Text = dialog.FolderName;
        _viewModel.SetDefaultImportPath(dialog.FolderName);
    }

    private void BrowseLinkedFilesPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose Default Linked Files Folder",
            InitialDirectory = Directory.Exists(LinkedFilesPathTextBox.Text) ? LinkedFilesPathTextBox.Text : null
        };

        if (dialog.ShowDialog() != true) return;

        LinkedFilesPathTextBox.Text = dialog.FolderName;
        _viewModel.SetLinkedFilesDefaultPath(dialog.FolderName);
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
}
