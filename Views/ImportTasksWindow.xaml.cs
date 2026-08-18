using System.IO;
using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Win32;

namespace KanbanApp.Views;

public partial class ImportTasksWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ImportTasksWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Import Template",
            Filter = "Excel File (*.xlsx)|*.xlsx",
            FileName = "Task Import Template.xlsx"
        };
        if (!string.IsNullOrWhiteSpace(_viewModel.DefaultImportPath) && Directory.Exists(_viewModel.DefaultImportPath))
        {
            dialog.InitialDirectory = _viewModel.DefaultImportPath;
        }

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ImportService.SaveTemplate(dialog.FileName,
                _viewModel.Columns.Select(c => c.DisplayName),
                _viewModel.Projects.Select(p => p.Name),
                _viewModel.Goals.Select(g => g.Name),
                _viewModel.People.Select(p => p.Name));
            StatusText.Text = $"Template saved to:\n{dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the template:\n{ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Excel File to Import",
            Filter = "Excel File (*.xlsx)|*.xlsx"
        };
        if (!string.IsNullOrWhiteSpace(_viewModel.DefaultImportPath) && Directory.Exists(_viewModel.DefaultImportPath))
        {
            dialog.InitialDirectory = _viewModel.DefaultImportPath;
        }

        if (dialog.ShowDialog(this) != true) return;

        List<Models.ImportedTaskRow> rows;
        try
        {
            rows = ImportService.ReadTasks(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not read that file:\n{ex.Message}", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (rows.Count == 0)
        {
            MessageBox.Show(this, "No tasks were found in that file. Make sure it has a header row with a \"Title\" column and at least one task below it.",
                "Nothing to Import", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var created = _viewModel.ImportCards(rows);

        Close();

        MessageBox.Show(Owner, $"Imported {created.Count} task{(created.Count == 1 ? "" : "s")}.",
            "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

        var reviewWindow = new ImportedTasksWindow(_viewModel) { Owner = Owner };
        reviewWindow.ShowDialog();
    }
}
