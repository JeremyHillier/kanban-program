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
    // Independent of the board's own Project/Priority/Who selection - fresh FilterOptionViewModel
    // instances carrying just the option names, so checking one here never touches the board's filter.
    private readonly List<FilterOptionViewModel> _projectOptions = [];
    private readonly List<FilterOptionViewModel> _priorityOptions = [];
    private readonly List<FilterOptionViewModel> _whoOptions = [];
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
                // Done is left unchecked by default - most reports are about what's still open,
                // and completed work already has its own Archive Done workflow.
                IsChecked = column.Name != "Done",
                Margin = new Thickness(0, 0, 16, 6),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };
            _columnCheckBoxes.Add(checkBox);
            ColumnsPanel.Children.Add(checkBox);
        }

        _projectOptions.AddRange(_viewModel.ProjectFilterOptions.Select(o => new FilterOptionViewModel(o.Name)));
        _priorityOptions.AddRange(_viewModel.PriorityFilterOptions.Select(o => new FilterOptionViewModel(o.Name)));
        _whoOptions.AddRange(_viewModel.WhoFilterOptions.Select(o => new FilterOptionViewModel(o.Name)));
        ProjectFilterListBox.ItemsSource = _projectOptions;
        PriorityFilterListBox.ItemsSource = _priorityOptions;
        WhoFilterListBox.ItemsSource = _whoOptions;

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

        SavedViewsComboBox.ItemsSource = _viewModel.SavedReportViews;

        ResetFields();

        _initializing = false;
        UpdateSortLevelAvailability();
    }

    // Shared by the constructor and the Reset button, so both restore exactly the same defaults.
    private void ResetFields()
    {
        ReportTitleTextBox.Text = "Kanban Task Report";

        foreach (var checkBox in _columnCheckBoxes) checkBox.IsChecked = (string)checkBox.Tag != "Done";

        foreach (var option in _projectOptions) option.IsSelected = false;
        foreach (var option in _priorityOptions) option.IsSelected = false;
        foreach (var option in _whoOptions) option.IsSelected = false;
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
        PortraitRadio.IsChecked = true;
        ArchivedFromDatePicker.SelectedDate = null;
        ArchivedToDatePicker.SelectedDate = null;

        IncludeNotesCheckBox.IsChecked = true;
        IncludeSubTasksCheckBox.IsChecked = true;
        IncludeSubTaskSummaryCheckBox.IsChecked = false;

        UpdateSortLevelAvailability();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetFields();

    private void LoadReportView_Click(object sender, RoutedEventArgs e)
    {
        if (SavedViewsComboBox.SelectedItem is not SavedReportView view) return;
        ApplyReportView(view);
    }

    private void SaveReportView_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new PromptWindow("Save Report View", "Name for this view:") { Owner = this };
        if (prompt.ShowDialog() != true) return;

        var name = prompt.Value.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_viewModel.SavedReportViews.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            var overwrite = MessageBox.Show(this, $"A saved view named \"{name}\" already exists. Overwrite it?",
                "Overwrite View", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        _viewModel.SaveReportView(CaptureCurrentAsView(name));
        SavedViewsComboBox.SelectedItem = _viewModel.SavedReportViews.FirstOrDefault(v => v.Name == name);
    }

    private void DeleteReportView_Click(object sender, RoutedEventArgs e)
    {
        if (SavedViewsComboBox.SelectedItem is not SavedReportView view) return;

        var result = MessageBox.Show(this, $"Delete the saved view \"{view.Name}\"?",
            "Delete View", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        _viewModel.DeleteReportView(view.Name);
    }

    // Captures every field this window exposes - broader than GetParameterSummary, which only
    // describes the filter/scope/sort choices that affect which rows match, not layout choices
    // like columns, orientation, or the Notes/Sub-tasks toggles.
    private SavedReportView CaptureCurrentAsView(string name) => new()
    {
        Name = name,
        Title = GetReportTitle(),
        IncludedColumns = GetIncludedColumns().ToList(),
        Project = _projectOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
        Priority = _priorityOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
        Who = _whoOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
        Goal = (string)GoalFilterComboBox.SelectedItem,
        Flag = (string)FlagFilterComboBox.SelectedItem,
        Due = (string)DueFilterComboBox.SelectedItem,
        DueFrom = DueFromDatePicker.SelectedDate?.ToString("yyyy-MM-dd"),
        DueTo = DueToDatePicker.SelectedDate?.ToString("yyyy-MM-dd"),
        IncludeNoDueDate = IncludeNoDueDateCheckBox.IsChecked == true,
        CustomFilterNames = _customFilterCheckBoxes.Where(c => c.IsChecked == true).Select(c => ((CustomFilter)c.Tag).Name).ToList(),
        SortLevel1 = (string)((ComboBoxItem)SortLevel1ComboBox.SelectedItem).Tag,
        SortLevel2 = (string)((ComboBoxItem)SortLevel2ComboBox.SelectedItem).Tag,
        SortLevel3 = (string)((ComboBoxItem)SortLevel3ComboBox.SelectedItem).Tag,
        GroupBy = GetGroupBy(),
        ArchiveScope = GetArchiveScope().ToString(),
        ArchivedFrom = ArchivedFromDatePicker.SelectedDate?.ToString("yyyy-MM-dd"),
        ArchivedTo = ArchivedToDatePicker.SelectedDate?.ToString("yyyy-MM-dd"),
        IsLandscape = LandscapeRadio.IsChecked == true,
        IncludeNotes = IncludeNotesCheckBox.IsChecked == true,
        IncludeSubTasks = IncludeSubTasksCheckBox.IsChecked == true,
        IncludeSubTaskSummary = IncludeSubTaskSummaryCheckBox.IsChecked == true
    };

    private void ApplyReportView(SavedReportView view)
    {
        ReportTitleTextBox.Text = view.Title;

        foreach (var checkBox in _columnCheckBoxes) checkBox.IsChecked = view.IncludedColumns.Contains((string)checkBox.Tag);

        foreach (var option in _projectOptions) option.IsSelected = view.Project.Contains(option.Name);
        foreach (var option in _priorityOptions) option.IsSelected = view.Priority.Contains(option.Name);
        foreach (var option in _whoOptions) option.IsSelected = view.Who.Contains(option.Name);

        SelectComboItem(GoalFilterComboBox, view.Goal);
        SelectComboItem(FlagFilterComboBox, view.Flag);
        SelectComboItem(DueFilterComboBox, view.Due);

        DueFromDatePicker.SelectedDate = ParseDate(view.DueFrom);
        DueToDatePicker.SelectedDate = ParseDate(view.DueTo);
        IncludeNoDueDateCheckBox.IsChecked = view.IncludeNoDueDate;

        // A custom filter slot renamed or deleted since this view was saved just drops out here,
        // the same as a gone Project/Who/Goal name silently falling out of the lists above.
        foreach (var checkBox in _customFilterCheckBoxes)
        {
            checkBox.IsChecked = view.CustomFilterNames.Contains(((CustomFilter)checkBox.Tag).Name);
        }

        SelectComboItemByTag(GroupByComboBox, view.GroupBy);
        SelectComboItemByTag(SortLevel1ComboBox, view.SortLevel1);
        SelectComboItemByTag(SortLevel2ComboBox, view.SortLevel2);
        SelectComboItemByTag(SortLevel3ComboBox, view.SortLevel3);

        BoardOnlyRadio.IsChecked = view.ArchiveScope == nameof(ReportArchiveScope.BoardOnly);
        BoardAndArchivedRadio.IsChecked = view.ArchiveScope == nameof(ReportArchiveScope.BoardAndArchived);
        ArchivedOnlyRadio.IsChecked = view.ArchiveScope == nameof(ReportArchiveScope.ArchivedOnly);

        ArchivedFromDatePicker.SelectedDate = ParseDate(view.ArchivedFrom);
        ArchivedToDatePicker.SelectedDate = ParseDate(view.ArchivedTo);

        LandscapeRadio.IsChecked = view.IsLandscape;
        PortraitRadio.IsChecked = !view.IsLandscape;

        IncludeNotesCheckBox.IsChecked = view.IncludeNotes;
        IncludeSubTasksCheckBox.IsChecked = view.IncludeSubTasks;
        IncludeSubTaskSummaryCheckBox.IsChecked = view.IncludeSubTaskSummary;

        UpdateSortLevelAvailability();
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        if (combo.Items.Contains(value)) combo.SelectedItem = value;
    }

    private static void SelectComboItemByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem comboBoxItem && (string)comboBoxItem.Tag == tag)
            {
                combo.SelectedItem = comboBoxItem;
                return;
            }
        }
    }

    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, out var parsed) ? parsed : null;

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

    // Summarizes every filter/scope/sort choice the report was built with, so a printed or saved
    // report is self-describing without the reader having to remember what was picked in this
    // window. Only non-default selections are mentioned, to keep the common case (few or no
    // filters) short.
    private string GetParameterSummary()
    {
        var parts = new List<string>();

        var includedCount = _columnCheckBoxes.Count(c => c.IsChecked == true);
        if (includedCount < _columnCheckBoxes.Count)
        {
            var names = _columnCheckBoxes.Where(c => c.IsChecked == true).Select(c => c.Content.ToString());
            parts.Add($"Columns: {string.Join(", ", names)}");
        }

        var unionFilters = _customFilterCheckBoxes.Where(c => c.IsChecked == true).Select(c => (CustomFilter)c.Tag).ToList();
        if (unionFilters.Count > 0)
        {
            parts.Add($"Custom filters (any of): {string.Join(", ", unionFilters.Select(f => f.Name))}");
        }
        else
        {
            var filterParts = new List<string>();
            void AddIfAnySelected(string label, List<FilterOptionViewModel> options)
            {
                var selected = options.Where(o => o.IsSelected).Select(o => o.Name).ToList();
                if (selected.Count > 0) filterParts.Add($"{label}: {string.Join(", ", selected)}");
            }
            void AddIfSet(string label, ComboBox combo) { if ((string)combo.SelectedItem != "All") filterParts.Add($"{label}: {combo.SelectedItem}"); }
            AddIfAnySelected("Project", _projectOptions);
            AddIfAnySelected("Priority", _priorityOptions);
            AddIfAnySelected("Who", _whoOptions);
            AddIfSet("Goal", GoalFilterComboBox);
            AddIfSet("Flag", FlagFilterComboBox);
            AddIfSet("Due", DueFilterComboBox);
            if (filterParts.Count > 0) parts.Add("Filters: " + string.Join(", ", filterParts));
        }

        if (DueFromDatePicker.SelectedDate is not null || DueToDatePicker.SelectedDate is not null || IncludeNoDueDateCheckBox.IsChecked == true)
        {
            var from = DueFromDatePicker.SelectedDate?.ToString("MMM d, yyyy") ?? "any";
            var to = DueToDatePicker.SelectedDate?.ToString("MMM d, yyyy") ?? "any";
            var noDue = IncludeNoDueDateCheckBox.IsChecked == true ? " + tasks with no due date" : "";
            parts.Add($"Due date range: {from} to {to}{noDue}");
        }

        var sortLevels = new[] { SortLevel1ComboBox, SortLevel2ComboBox, SortLevel3ComboBox }
            .Select(c => (string)((ComboBoxItem)c.SelectedItem).Tag)
            .Where(v => v != "None")
            .ToList();
        if (sortLevels.Count > 0) parts.Add("Sort order: " + string.Join(" then ", sortLevels));

        var groupBy = GetGroupBy();
        if (groupBy != "None") parts.Add($"Grouped by: {(groupBy == "Status" ? "Category" : groupBy)}");

        var scope = GetArchiveScope();
        if (scope != ReportArchiveScope.BoardOnly)
        {
            var scopeText = scope == ReportArchiveScope.ArchivedOnly ? "Archived only" : "Board tasks + archived";
            if (ArchivedFromDatePicker.SelectedDate is not null || ArchivedToDatePicker.SelectedDate is not null)
            {
                var aFrom = ArchivedFromDatePicker.SelectedDate?.ToString("MMM d, yyyy") ?? "any";
                var aTo = ArchivedToDatePicker.SelectedDate?.ToString("MMM d, yyyy") ?? "any";
                scopeText += $" (archived {aFrom} to {aTo})";
            }
            parts.Add($"Scope: {scopeText}");
        }

        return parts.Count == 0 ? "No filters applied" : "Parameters: " + string.Join("   |   ", parts);
    }

    private List<Models.ReportRow> BuildRows()
    {
        var scope = GetArchiveScope();
        var unionFilters = _customFilterCheckBoxes.Where(c => c.IsChecked == true).Select(c => (CustomFilter)c.Tag).ToList();

        return ReportService.BuildRows(
            _viewModel.Columns,
            GetIncludedColumns(),
            _projectOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
            _priorityOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
            _whoOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
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
            IncludeSubTaskSummaryCheckBox.IsChecked == true, LandscapeRadio.IsChecked == true, GetParameterSummary());

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
            IncludeSubTaskSummaryCheckBox.IsChecked == true, LandscapeRadio.IsChecked == true, GetParameterSummary());

        MessageBox.Show(this, $"Report saved to:\n{filePath}", "Report Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
