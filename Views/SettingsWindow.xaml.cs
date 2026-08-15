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
    }

    private void ButtonsOnRightCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var wantRight = ButtonsOnRightCheckBox.IsChecked == true;
        if (wantRight != _viewModel.IsButtonsOnRight)
        {
            _viewModel.ToggleButtonPosition();
        }
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
