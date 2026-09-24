using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The default folders (export, import, linked files) and Your Details, used in emails.
public partial class SettingsWindow
{
    private void ExportPathTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.SetDefaultExportPath(ExportPathTextBox.Text.Trim());
    }

    private void UserNameTextBox_LostFocus(object sender, RoutedEventArgs e) => _viewModel.SetUserName(UserNameTextBox.Text);

    private void UserTitleTextBox_LostFocus(object sender, RoutedEventArgs e) => _viewModel.SetUserTitle(UserTitleTextBox.Text);

    private void UserEmailTextBox_LostFocus(object sender, RoutedEventArgs e) => _viewModel.SetUserEmail(UserEmailTextBox.Text);

    private void UserPhoneTextBox_LostFocus(object sender, RoutedEventArgs e) => _viewModel.SetUserPhone(UserPhoneTextBox.Text);

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
}
