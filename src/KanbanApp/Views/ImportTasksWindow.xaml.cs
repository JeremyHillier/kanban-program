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
            Dialogs.Tell(this, "Template Not Saved", "The Excel template could not be saved.\n\nIf it is open in Excel, close it there and try again.", DialogTone.Error, ex.Message);
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
            Dialogs.Tell(this, "Import Failed", "That file could not be read, so nothing was imported.\n\nIf it is open in Excel, close it there and try again.", DialogTone.Error, ex.Message);
            return;
        }

        if (rows.Count == 0)
        {
            Dialogs.Tell(this, "Nothing to Import",
                "No tasks were found in that file.\n\nIt needs a heading row with a \"Title\" column, and at least one task below it. Save Template gives a file laid out the right way.");
            return;
        }

        var created = _viewModel.ImportCards(rows);

        Close();

        Dialogs.Tell(Owner, "Import Complete",
            $"Imported {created.Count} task{(created.Count == 1 ? "" : "s")}.\n\nCheck them on the next screen, where anything can still be changed.");

        var reviewWindow = new ImportedTasksWindow(_viewModel) { Owner = Owner };
        reviewWindow.ShowDialog();
    }
}
