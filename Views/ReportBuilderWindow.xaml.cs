using System.IO;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Win32;

namespace KanbanApp.Views;

public partial class ReportBuilderWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<CheckBox> _columnCheckBoxes = [];

    public ReportBuilderWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        foreach (var column in _viewModel.Columns)
        {
            var checkBox = new CheckBox
            {
                Content = column.DisplayName,
                Tag = column.Name,
                IsChecked = true,
                Margin = new Thickness(0, 0, 16, 6),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };
            _columnCheckBoxes.Add(checkBox);
            ColumnsPanel.Children.Add(checkBox);
        }

        ProjectFilterComboBox.ItemsSource = _viewModel.ProjectFilterOptions;
        PriorityFilterComboBox.ItemsSource = _viewModel.PriorityFilterOptions;
        WhoFilterComboBox.ItemsSource = _viewModel.WhoFilterOptions;
        GoalFilterComboBox.ItemsSource = _viewModel.GoalFilterOptions;
        FlagFilterComboBox.ItemsSource = _viewModel.FlagFilterOptions;
        DueFilterComboBox.ItemsSource = _viewModel.DueFilterOptions;

        ProjectFilterComboBox.SelectedIndex = 0;
        PriorityFilterComboBox.SelectedIndex = 0;
        WhoFilterComboBox.SelectedIndex = 0;
        GoalFilterComboBox.SelectedIndex = 0;
        FlagFilterComboBox.SelectedIndex = 0;
        DueFilterComboBox.SelectedIndex = 0;

        IncludeNotesCheckBox.IsChecked = true;
        IncludeSubTasksCheckBox.IsChecked = true;
    }

    private void DatePicker_Loaded(object sender, RoutedEventArgs e) => CalendarWheelSupport.Attach((DatePicker)sender);

    private void TodayArchivedFrom_Click(object sender, RoutedEventArgs e) => ArchivedFromDatePicker.SelectedDate = DateTime.Today;

    private void TodayArchivedTo_Click(object sender, RoutedEventArgs e) => ArchivedToDatePicker.SelectedDate = DateTime.Today;

    private HashSet<string> GetIncludedColumns() =>
        _columnCheckBoxes.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToHashSet();

    private string GetGroupBy() => (string)((ComboBoxItem)GroupByComboBox.SelectedItem).Tag;

    private string GetReportTitle() =>
        string.IsNullOrWhiteSpace(ReportTitleTextBox.Text) ? "Kanban Task Report" : ReportTitleTextBox.Text.Trim();

    private ReportArchiveScope GetArchiveScope() =>
        ArchivedOnlyRadio.IsChecked == true ? ReportArchiveScope.ArchivedOnly
        : BoardAndArchivedRadio.IsChecked == true ? ReportArchiveScope.BoardAndArchived
        : ReportArchiveScope.BoardOnly;

    private List<Models.ReportRow> BuildRows()
    {
        var scope = GetArchiveScope();
        return ReportService.BuildRows(
            _viewModel.Columns,
            GetIncludedColumns(),
            (string)ProjectFilterComboBox.SelectedItem,
            (string)PriorityFilterComboBox.SelectedItem,
            (string)WhoFilterComboBox.SelectedItem,
            (string)GoalFilterComboBox.SelectedItem,
            (string)FlagFilterComboBox.SelectedItem,
            (string)DueFilterComboBox.SelectedItem,
            scope,
            scope == ReportArchiveScope.BoardOnly ? null : _viewModel.GetArchivedReportRows(),
            ArchivedFromDatePicker.SelectedDate,
            ArchivedToDatePicker.SelectedDate);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        var title = GetReportTitle();
        var rows = BuildRows();
        var document = ReportService.BuildFixedDocument(
            title, rows, GetGroupBy(), IncludeNotesCheckBox.IsChecked == true, IncludeSubTasksCheckBox.IsChecked == true,
            IncludeSubTaskSummaryCheckBox.IsChecked == true);

        new ReportPreviewWindow(document) { Owner = this }.ShowDialog();
    }

    private void Pdf_Click(object sender, RoutedEventArgs e)
    {
        var title = GetReportTitle();
        var rows = BuildRows();

        var sanitizedTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{sanitizedTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        string? filePath = null;

        if (!string.IsNullOrWhiteSpace(_viewModel.DefaultExportPath) && Directory.Exists(_viewModel.DefaultExportPath))
        {
            filePath = Path.Combine(_viewModel.DefaultExportPath, fileName);
        }
        else
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Report as PDF",
                Filter = "PDF File (*.pdf)|*.pdf",
                FileName = fileName
            };

            if (dialog.ShowDialog(this) == true)
            {
                filePath = dialog.FileName;
            }
        }

        if (filePath is null) return;

        ReportService.SavePdf(title, rows, GetGroupBy(), IncludeNotesCheckBox.IsChecked == true, IncludeSubTasksCheckBox.IsChecked == true, filePath,
            IncludeSubTaskSummaryCheckBox.IsChecked == true);

        MessageBox.Show(this, $"Report saved to:\n{filePath}", "Report Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
