using System.Diagnostics;
using System.IO;
using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        ButtonsOnRightCheckBox.IsChecked = viewModel.IsButtonsOnRight;
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
    }

    private void ButtonsOnRightCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var wantRight = ButtonsOnRightCheckBox.IsChecked == true;
        if (wantRight != _viewModel.IsButtonsOnRight)
        {
            _viewModel.ToggleButtonPosition();
        }
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
            if (!File.Exists(newPath) && File.Exists(currentPath))
            {
                File.Copy(currentPath, newPath);
            }

            var config = AppConfig.Load();
            config.DbPath = newPath;
            config.Save();

            var result = MessageBox.Show(
                "The database location has been updated. The app needs to restart for this to take effect. Restart now?",
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
}
