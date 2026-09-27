using System.IO;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Win32;

namespace KanbanApp.Views;

// Run a saved report view in two clicks: pick it, then Preview, Print or PDF. Everything about the
// report comes from the saved view (see ReportRunner); "today" dates are worked out for today.
public partial class QuickReportWindow : Window
{

    private readonly MainViewModel _viewModel;

    public QuickReportWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        LoadViews(viewModel.QuickReportLastView);
    }

    private void LoadViews(string? select)
    {
        var views = _viewModel.SavedReportViews.ToList();
        ViewComboBox.ItemsSource = views;
        ViewComboBox.SelectedItem = views.FirstOrDefault(v => string.Equals(v.Name, select, StringComparison.OrdinalIgnoreCase)) ?? views.FirstOrDefault();

        var any = views.Count > 0;
        ViewComboBox.IsEnabled = PreviewButton.IsEnabled = PrintButton.IsEnabled = PdfButton.IsEnabled = any;
        if (!any) SummaryText.Text = "No saved report views yet. Open the Report Builder, set up a report, and click Save View. It will then be listed here.";
    }

    private SavedReportView? Selected => ViewComboBox.SelectedItem as SavedReportView;

    private void ViewComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Selected is not { } view) return;
        SummaryText.Text = ReportRunner.Summarise(view, DateTime.Today);
        _viewModel.SetQuickReportLastView(view.Name);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } view) return;
        new ReportPreviewWindow(ReportRunner.BuildDocument(_viewModel, view, DateTime.Today)) { Owner = this }.ShowDialog();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } view) return;

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        dialog.PrintDocument(ReportRunner.BuildDocument(_viewModel, view, DateTime.Today).DocumentPaginator, view.Title);
    }

    private void Pdf_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } view) return;

        var fileName = $"{string.Join("_", view.Title.Split(Path.GetInvalidFileNameChars()))}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        string? filePath = null;
        if (!string.IsNullOrWhiteSpace(_viewModel.DefaultExportPath) && Directory.Exists(_viewModel.DefaultExportPath))
        {
            filePath = Path.Combine(_viewModel.DefaultExportPath, fileName);
        }
        else
        {
            var save = new SaveFileDialog { Title = "Save Report as PDF", Filter = "PDF File (*.pdf)|*.pdf", FileName = fileName };
            if (save.ShowDialog(this) == true) filePath = save.FileName;
        }
        if (filePath is null) return;

        ReportRunner.SavePdf(_viewModel, view, DateTime.Today, filePath);
        Dialogs.Tell(this, "Report Saved", "The report was saved as a PDF.", detail: filePath);
    }

    private void ReportBuilder_Click(object sender, RoutedEventArgs e)
    {
        new ReportBuilderWindow(_viewModel) { Owner = this }.ShowDialog();
        LoadViews(Selected?.Name); // a view may have been added, changed or deleted
    }
}
