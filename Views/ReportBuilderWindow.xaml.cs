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
    private readonly List<CheckBox> _customFilterCheckBoxes = [];
    private bool _initializing = true;

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

        foreach (var filter in _viewModel.CustomFilters.Where(f => f.IsDefined))
        {
            var checkBox = new CheckBox
            {
                Content = filter.Name,
                Tag = filter,
                Margin = new Thickness(0, 0, 16, 6),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };
            _customFilterCheckBoxes.Add(checkBox);
            CustomFiltersPanel.Children.Add(checkBox);
        }

        ResetFields();

        _initializing = false;
        UpdateSortLevelAvailability();
    }

    // Shared by the constructor and the Reset button, so both restore exactly the same defaults.
    private void ResetFields()
    {
        ReportTitleTextBox.Text = "Kanban Task Report";

        foreach (var checkBox in _columnCheckBoxes) checkBox.IsChecked = true;

        ProjectFilterComboBox.SelectedIndex = 0;
        PriorityFilterComboBox.SelectedIndex = 0;
        WhoFilterComboBox.SelectedIndex = 0;
        GoalFilterComboBox.SelectedIndex = 0;
        FlagFilterComboBox.SelectedIndex = 0;
        DueFilterComboBox.SelectedIndex = 0;

        DueFromDatePicker.SelectedDate = null;
        DueToDatePicker.SelectedDate = null;
        IncludeNoDueDateCheckBox.IsChecked = false;

        foreach (var checkBox in _customFilterCheckBoxes) checkBox.IsChecked = false;

        GroupByComboBox.SelectedIndex = 0;
        SortLevel1ComboBox.SelectedIndex = 0;
        SortLevel2ComboBox.SelectedIndex = 0;
        SortLevel3ComboBox.SelectedIndex = 0;

        BoardOnlyRadio.IsChecked = true;
        ArchivedFromDatePicker.SelectedDate = null;
        ArchivedToDatePicker.SelectedDate = null;

        IncludeNotesCheckBox.IsChecked = true;
        IncludeSubTasksCheckBox.IsChecked = true;
        IncludeSubTaskSummaryCheckBox.IsChecked = false;

        UpdateSortLevelAvailability();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetFields();

    private void DatePicker_Loaded(object sender, RoutedEventArgs e) => CalendarWheelSupport.Attach((DatePicker)sender);

    private void TodayDueFrom_Click(object sender, RoutedEventArgs e) => DueFromDatePicker.SelectedDate = DateTime.Today;

    private void TodayDueTo_Click(object sender, RoutedEventArgs e) => DueToDatePicker.SelectedDate = DateTime.Today;

    private void ClearDueFrom_Click(object sender, RoutedEventArgs e) => DueFromDatePicker.SelectedDate = null;

    private void ClearDueTo_Click(object sender, RoutedEventArgs e) => DueToDatePicker.SelectedDate = null;

    private void TodayArchivedFrom_Click(object sender, RoutedEventArgs e) => ArchivedFromDatePicker.SelectedDate = DateTime.Today;

    private void TodayArchivedTo_Click(object sender, RoutedEventArgs e) => ArchivedToDatePicker.SelectedDate = DateTime.Today;

    private void ClearArchivedFrom_Click(object sender, RoutedEventArgs e) => ArchivedFromDatePicker.SelectedDate = null;

    private void ClearArchivedTo_Click(object sender, RoutedEventArgs e) => ArchivedToDatePicker.SelectedDate = null;

    private void GroupByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSortLevelAvailability();

    private void SortLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSortLevelAvailability();

    // Sort level ComboBoxItems are declared statically in XAML (not via ItemsSource), so their
    // containers exist immediately - no need to wait on ItemContainerGenerator. "Gray out" is done
    // via IsEnabled rather than removing the item, so an already-picked level stays visible and
    // selectable while the user changes the other two.
    private void UpdateSortLevelAvailability()
    {
        if (_initializing) return;

        var groupBy = GetGroupBy();
        var groupByLabel = groupBy switch { "Status" => "Category", "None" => null, _ => groupBy };

        var combos = new[] { SortLevel1ComboBox, SortLevel2ComboBox, SortLevel3ComboBox };
        for (var i = 0; i < combos.Length; i++)
        {
            var combo = combos[i];
            var selectedTag = (string)((ComboBoxItem)combo.SelectedItem).Tag;
            var usedElsewhere = combos.Where((_, idx) => idx != i).Select(c => (string)((ComboBoxItem)c.SelectedItem).Tag)
                .Concat([groupByLabel])
                .Where(v => v is not null && v != "None")
                .ToHashSet();

            foreach (ComboBoxItem item in combo.Items)
            {
                var value = (string)item.Tag;
                item.IsEnabled = value == "None" || value == selectedTag || !usedElsewhere.Contains(value);
            }
        }
    }

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
        var unionFilters = _customFilterCheckBoxes.Where(c => c.IsChecked == true).Select(c => (CustomFilter)c.Tag).ToList();

        return ReportService.BuildRows(
            _viewModel.Columns,
            GetIncludedColumns(),
            (string)ProjectFilterComboBox.SelectedItem,
            (string)PriorityFilterComboBox.SelectedItem,
            (string)WhoFilterComboBox.SelectedItem,
            (string)GoalFilterComboBox.SelectedItem,
            (string)FlagFilterComboBox.SelectedItem,
            (string)DueFilterComboBox.SelectedItem,
            DueFromDatePicker.SelectedDate,
            DueToDatePicker.SelectedDate,
            IncludeNoDueDateCheckBox.IsChecked == true,
            unionFilters.Count > 0 ? unionFilters : null,
            (string)((ComboBoxItem)SortLevel1ComboBox.SelectedItem).Tag,
            (string)((ComboBoxItem)SortLevel2ComboBox.SelectedItem).Tag,
            (string)((ComboBoxItem)SortLevel3ComboBox.SelectedItem).Tag,
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
