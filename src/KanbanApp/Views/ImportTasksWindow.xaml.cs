using System.IO;
using System.Windows;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Win32;

namespace KanbanApp.Views;

// Import from Excel: pick a file, or drop one on the window - from a folder, or out of Outlook as
// the attachment itself or the whole email. Either way the tasks found are listed for a yes before
// anything is imported, and the review screen follows.
public partial class ImportTasksWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Where files taken out of a dropped email (or Outlook's virtual files) are written; gone once the window closes.
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "Kanban Task Board Import", Guid.NewGuid().ToString("N"));

    public static readonly DependencyProperty IsDragOverProperty =
        DependencyProperty.Register(nameof(IsDragOver), typeof(bool), typeof(ImportTasksWindow), new PropertyMetadata(false));

    public bool IsDragOver
    {
        get => (bool)GetValue(IsDragOverProperty);
        set => SetValue(IsDragOverProperty, value);
    }

    public ImportTasksWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Closed += (_, _) =>
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); }
            catch (IOException) { /* a file still open somewhere; the temp folder is tidied another day */ }
            catch (UnauthorizedAccessException) { }
        };
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
                _viewModel.People.Select(p => p.Name),
                _viewModel.Priorities.Names);
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

        ImportFiles([dialog.FileName]);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        var canDrop = OutlookDragDropHelper.HasDroppableFiles(e.Data);
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        IsDragOver = canDrop;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => IsDragOver = false;

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        IsDragOver = false;
        if (!OutlookDragDropHelper.HasDroppableFiles(e.Data)) return;

        List<string> dropped;
        try
        {
            dropped = OutlookDragDropHelper.ExtractDroppedFiles(e.Data, _workDir).Select(f => f.FilePath).ToList();
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Could Not Read the Drop", "What was dropped could not be read, so nothing was imported.", DialogTone.Error, ex.Message);
            return;
        }

        var found = ImportDrop.FindExcelFiles(dropped, _workDir);
        if (found.ExcelFiles.Count == 0)
        {
            Dialogs.Tell(this, "No Excel File", "Nothing dropped was an Excel (.xlsx) file, so nothing was imported.\n\n" +
                "Drop the Excel file itself, or an email that has one attached.", detail: string.Join("\n", found.Ignored));
            return;
        }

        ImportFiles(found.ExcelFiles, found.Ignored);
    }

    // Reads the files, asks, imports, and opens the review screen. ignored names anything dropped
    // alongside that was not an Excel file, so the user knows it was left out.
    private void ImportFiles(IReadOnlyList<string> paths, IReadOnlyList<string>? ignored = null)
    {
        var files = new List<(string File, List<ImportedTaskRow> Rows)>();
        foreach (var path in paths)
        {
            try
            {
                files.Add((path, ImportService.ReadTasks(path)));
            }
            catch (Exception ex)
            {
                Dialogs.Tell(this, "Import Failed", $"{Path.GetFileName(path)} could not be read, so nothing was imported.\n\nIf it is open in Excel, close it there and try again.", DialogTone.Error, ex.Message);
                return;
            }
        }

        var rows = files.SelectMany(f => f.Rows).ToList();
        if (rows.Count == 0)
        {
            Dialogs.Tell(this, "Nothing to Import",
                $"No tasks were found in {(files.Count == 1 ? "that file" : "those files")}.\n\nIt needs a heading row with a \"Title\" column, and at least one task below it. Download Template gives a file laid out the right way.");
            return;
        }

        var text = ImportDrop.DescribeForConfirm(files);
        if (ignored is { Count: > 0 }) text += $"\n\nLeft out, not being Excel files: {string.Join(", ", ignored)}.";
        if (!Dialogs.Confirm(this, DialogMessage.Ask("Import Tasks", text, "Import"))) return;

        _viewModel.ImportCards(rows);
        Close();

        var reviewWindow = new ImportedTasksWindow(_viewModel) { Owner = Owner };
        reviewWindow.ShowDialog();
    }
}
