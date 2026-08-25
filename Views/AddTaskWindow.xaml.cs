using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<string> _sessionPastedFilePaths = [];
    private CardViewModel? _cardToEdit;

    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }
    public ProjectViewModel? SelectedProject { get; private set; }
    public GoalViewModel? SelectedGoal { get; private set; }
    public List<FlagViewModel> SelectedFlags { get; private set; } = [];
    public List<SubTaskViewModel> SelectedSubTasks { get; private set; } = [];
    public List<AttachmentViewModel> SelectedAttachments { get; private set; } = [];
    public string SelectedPriority { get; private set; } = "Normal";
    public DateTime? SelectedDueDate { get; private set; }
    public PersonViewModel? SelectedWho { get; private set; }
    public string? Notes { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurrencePattern { get; private set; }
    public bool ForceEditOnComplete { get; private set; }

    public AddTaskWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        CategoryComboBox.ItemsSource = viewModel.Columns;
        RebuildProjectItems();
        RebuildGoalItems();
        RebuildWhoItems();
        RebuildFlagCheckboxes();
        UpdateSubTaskProgressLabel();

        ProjectComboBox.Focus();
    }

    public void PreselectColumn(ColumnViewModel column)
    {
        CategoryComboBox.SelectedItem = column;
    }

    public void FocusNotesField()
    {
        Loaded += (_, _) =>
        {
            NotesTextBox.Focus();
            NotesTextBox.CaretIndex = NotesTextBox.Text.Length;
        };
    }

    public AddTaskWindow(MainViewModel viewModel, CardViewModel cardToEdit, ColumnViewModel currentColumn) : this(viewModel)
    {
        Title = "Edit Task";
        SubmitButton.Content = "Save";

        _cardToEdit = cardToEdit;
        EmailButton.Visibility = cardToEdit.CanEmailCard ? Visibility.Visible : Visibility.Collapsed;

        DetailsTextBox.Text = cardToEdit.Title;
        CategoryComboBox.SelectedItem = currentColumn;
        RebuildProjectItems(_viewModel.Projects.FirstOrDefault(p => p.Id == cardToEdit.ProjectId));
        RebuildGoalItems(_viewModel.Goals.FirstOrDefault(g => g.Id == cardToEdit.GoalId));

        foreach (var item in PriorityComboBox.Items.OfType<ComboBoxItem>())
        {
            if ((string)item.Content == cardToEdit.Priority)
            {
                PriorityComboBox.SelectedItem = item;
                break;
            }
        }

        DueDatePicker.SelectedDate = cardToEdit.DueDate;
        RebuildWhoItems(_viewModel.People.FirstOrDefault(p => p.Id == cardToEdit.WhoId));
        NotesTextBox.Text = cardToEdit.Notes ?? string.Empty;
        ForceEditOnCompleteCheckBox.IsChecked = cardToEdit.ForceEditOnComplete;

        RecurringCheckBox.IsChecked = cardToEdit.IsRecurring;
        RecurrenceComboBox.Visibility = cardToEdit.IsRecurring ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in RecurrenceComboBox.Items.OfType<ComboBoxItem>())
        {
            if ((string)item.Content == cardToEdit.RecurrencePattern)
            {
                RecurrenceComboBox.SelectedItem = item;
                break;
            }
        }

        RebuildFlagCheckboxes(forceCheckedIds: cardToEdit.Flags.Select(f => f.Id));

        foreach (var subTask in cardToEdit.SubTasks)
        {
            AddSubTaskRow(subTask.Title, subTask.IsDone);
        }
        UpdateSubTaskProgressLabel();

        foreach (var attachment in cardToEdit.Attachments)
        {
            AddAttachmentRow(attachment);
        }
    }

    private void RebuildProjectItems(ProjectViewModel? autoSelect = null)
    {
        var items = _viewModel.Projects.Where(p => p.IsActive).ToList();
        if (autoSelect is not null && !items.Any(p => p.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        ProjectComboBox.ItemsSource = items;
        ProjectComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(p => p.Id == autoSelect.Id);
    }

    private void RebuildGoalItems(GoalViewModel? autoSelect = null)
    {
        var items = _viewModel.Goals.Where(g => g.IsActive).ToList();
        if (autoSelect is not null && !items.Any(g => g.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        GoalComboBox.ItemsSource = items;
        GoalComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(g => g.Id == autoSelect.Id);
    }

    private void RebuildWhoItems(PersonViewModel? autoSelect = null)
    {
        var items = _viewModel.People.Where(p => p.IsActive).ToList();
        if (autoSelect is not null && !items.Any(p => p.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        WhoComboBox.ItemsSource = items;
        WhoComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(p => p.Id == autoSelect.Id);
    }

    private void RebuildFlagCheckboxes(int? autoCheckFlagId = null, IEnumerable<int>? forceCheckedIds = null)
    {
        var checkedIds = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => ((FlagViewModel)cb.Tag).Id)
            .ToHashSet();

        if (forceCheckedIds is not null)
        {
            checkedIds.UnionWith(forceCheckedIds);
        }

        FlagsPanel.Children.Clear();
        foreach (var flag in _viewModel.Flags.Where(f => f.IsActive || checkedIds.Contains(f.Id)))
        {
            FlagsPanel.Children.Add(new CheckBox
            {
                Content = flag.Name,
                Tag = flag,
                IsChecked = checkedIds.Contains(flag.Id) || flag.Id == autoCheckFlagId,
                Margin = new Thickness(0, 0, 16, 8),
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            });
        }
    }

    private void AddAttachmentRow(AttachmentViewModel attachment)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 6), Tag = attachment };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        FrameworkElement preview;
        if (attachment.IsImage && File.Exists(attachment.FilePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 40;
                bitmap.UriSource = new Uri(attachment.FilePath);
                bitmap.EndInit();
                preview = new Image { Source = bitmap, Width = 40, Height = 40, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 8, 0) };
            }
            catch
            {
                preview = new TextBlock { Text = "🖼", FontSize = 20, Width = 40, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            }
        }
        else
        {
            preview = new TextBlock { Text = "📄", FontSize = 20, Width = 40, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        }
        Grid.SetColumn(preview, 0);
        preview.Cursor = Cursors.Hand;
        preview.MouseLeftButtonUp += (_, _) => OpenAttachment(attachment.FilePath);

        var nameText = new TextBlock
        {
            Text = attachment.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Cursor = Cursors.Hand,
            ToolTip = attachment.FilePath
        };
        nameText.MouseLeftButtonUp += (_, _) => OpenAttachment(attachment.FilePath);
        Grid.SetColumn(nameText, 1);

        var deleteButton = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Margin = new Thickness(6, 0, 0, 0),
            Background = (Brush)FindResource("ButtonBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            ToolTip = "Remove attachment"
        };
        Grid.SetColumn(deleteButton, 2);
        deleteButton.Click += (_, _) => AttachmentsPanel.Children.Remove(row);

        row.Children.Add(preview);
        row.Children.Add(nameText);
        row.Children.Add(deleteButton);
        AttachmentsPanel.Children.Add(row);
    }

    private static void OpenAttachment(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show($"This file can no longer be found:\n{path}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open the file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        if (_cardToEdit is null) return;

        OutlookEmailHelper.ComposeCardEmail(this, _cardToEdit);
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a File to Attach",
            Filter = "All Files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_viewModel.LinkedFilesDefaultPath) ? _viewModel.LinkedFilesDefaultPath : null
        };

        if (dialog.ShowDialog() != true) return;

        string destPath;
        try
        {
            destPath = OutlookDragDropHelper.CopyFileIntoAttachmentsDir(dialog.FileName, _viewModel.AttachmentsDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't copy the file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _sessionPastedFilePaths.Add(destPath);

        var attachment = new AttachmentViewModel(new CardAttachment
        {
            FilePath = destPath,
            DisplayName = Path.GetFileName(destPath),
            AddedDate = DateTime.Now
        });
        AddAttachmentRow(attachment);
    }

    private void AttachmentsDropZone_DragOver(object sender, DragEventArgs e)
    {
        var canDrop = OutlookDragDropHelper.HasDroppableFiles(e.Data);
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void AttachmentsDropZone_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!OutlookDragDropHelper.HasDroppableFiles(e.Data)) return;

        List<(string FilePath, string DisplayName, bool WasSaved)> files;
        try
        {
            files = OutlookDragDropHelper.ExtractDroppedFiles(e.Data, _viewModel.AttachmentsDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't read the dropped item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (var file in files)
        {
            if (file.WasSaved)
            {
                _sessionPastedFilePaths.Add(file.FilePath);
            }

            AddAttachmentRow(new AttachmentViewModel(new CardAttachment
            {
                FilePath = file.FilePath,
                DisplayName = file.DisplayName,
                AddedDate = DateTime.Now
            }));
        }
    }

    private void PasteScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            MessageBox.Show(this, "There's no image on the clipboard. Copy a screenshot (e.g. with the Snipping Tool or PrtScn) first.",
                "No Image Found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BitmapSource? image;
        try
        {
            image = Clipboard.GetImage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't read the clipboard image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (image is null) return;

        var attachmentsDir = _viewModel.AttachmentsDir;
        Directory.CreateDirectory(attachmentsDir);

        var fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var filePath = Path.Combine(attachmentsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        _sessionPastedFilePaths.Add(filePath);

        var attachment = new AttachmentViewModel(new CardAttachment
        {
            FilePath = filePath,
            DisplayName = fileName,
            AddedDate = DateTime.Now
        });
        AddAttachmentRow(attachment);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        var survivingPaths = SelectedAttachments.Select(a => a.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _sessionPastedFilePaths)
        {
            if (survivingPaths.Contains(path)) continue;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }
    }

    private void AddSubTaskRow(string title = "", bool isDone = false)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox
        {
            IsChecked = isDone,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        checkBox.Checked += (_, _) => UpdateSubTaskProgressLabel();
        checkBox.Unchecked += (_, _) => UpdateSubTaskProgressLabel();
        Grid.SetColumn(checkBox, 0);

        var textBox = new TextBox
        {
            Text = title,
            Padding = new Thickness(6),
            Style = (Style)FindResource("HoverTextBoxStyle"),
            Background = (Brush)FindResource("InputBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush")
        };
        Grid.SetColumn(textBox, 1);

        var deleteButton = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Margin = new Thickness(6, 0, 0, 0),
            Background = (Brush)FindResource("ButtonBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            ToolTip = "Remove sub-task"
        };
        Grid.SetColumn(deleteButton, 2);
        deleteButton.Click += (_, _) =>
        {
            SubTasksPanel.Children.Remove(row);
            UpdateSubTaskProgressLabel();
        };

        row.Children.Add(checkBox);
        row.Children.Add(textBox);
        row.Children.Add(deleteButton);
        SubTasksPanel.Children.Add(row);

        if (string.IsNullOrEmpty(title))
        {
            textBox.Focus();
        }

        UpdateSubTaskProgressLabel();
    }

    private void UpdateSubTaskProgressLabel()
    {
        var checkBoxes = SubTasksPanel.Children.OfType<Grid>()
            .Select(row => (CheckBox)row.Children[0])
            .ToList();

        if (checkBoxes.Count == 0)
        {
            SubTaskProgressLabel.Text = string.Empty;
            return;
        }

        var done = checkBoxes.Count(cb => cb.IsChecked == true);
        SubTaskProgressLabel.Text = $"{done}/{checkBoxes.Count} ({done * 100 / checkBoxes.Count}%)";
    }

    private void AddSubTaskRow_Click(object sender, RoutedEventArgs e)
    {
        AddSubTaskRow();
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Project", "Project name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddProject(dialog.Value);
        RebuildProjectItems(_viewModel.Projects.FirstOrDefault(p => p.Name == dialog.Value.Trim()));
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        ProjectComboBox.SelectedIndex = -1;
    }

    private void NewGoal_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Goal", "Goal name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddGoal(dialog.Value);
        RebuildGoalItems(_viewModel.Goals.FirstOrDefault(g => g.Name == dialog.Value.Trim()));
    }

    private void DeleteGoal_Click(object sender, RoutedEventArgs e)
    {
        GoalComboBox.SelectedIndex = -1;
    }

    private void NewWho_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Person", "Person name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddPerson(dialog.Value);
        RebuildWhoItems(_viewModel.People.FirstOrDefault(p => p.Name == dialog.Value.Trim()));
    }

    private void DeleteWho_Click(object sender, RoutedEventArgs e)
    {
        WhoComboBox.SelectedIndex = -1;
    }

    private void NewFlag_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Flag", "Flag name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddFlag(dialog.Value);
        RebuildFlagCheckboxes(_viewModel.Flags.FirstOrDefault(f => f.Name == dialog.Value.Trim())?.Id);
    }

    private void TodayDueDate_Click(object sender, RoutedEventArgs e)
    {
        DueDatePicker.SelectedDate = DateTime.Today;
    }

    private void ClearDueDate_Click(object sender, RoutedEventArgs e)
    {
        DueDatePicker.SelectedDate = null;
    }

    private void RecurringCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RecurrenceComboBox.Visibility = RecurringCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var details = DetailsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(details) ||
            CategoryComboBox.SelectedItem is not ColumnViewModel column ||
            PriorityComboBox.SelectedItem is not ComboBoxItem priorityItem)
        {
            return;
        }

        if (ProjectComboBox.SelectedItem is not ProjectViewModel project)
        {
            MessageBox.Show(this, "Please select a Project before saving.", "Project Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TaskDetails = details;
        SelectedColumn = column;
        SelectedProject = project;
        SelectedGoal = GoalComboBox.SelectedItem as GoalViewModel;
        SelectedPriority = (string)priorityItem.Content;
        SelectedDueDate = DueDatePicker.SelectedDate;
        SelectedWho = WhoComboBox.SelectedItem as PersonViewModel;
        Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();

        IsRecurring = RecurringCheckBox.IsChecked == true;
        RecurrencePattern = IsRecurring && RecurrenceComboBox.SelectedItem is ComboBoxItem recurrenceItem
            ? (string)recurrenceItem.Content
            : null;
        ForceEditOnComplete = ForceEditOnCompleteCheckBox.IsChecked == true;

        SelectedFlags = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (FlagViewModel)cb.Tag)
            .ToList();

        SelectedSubTasks = SubTasksPanel.Children.OfType<Grid>()
            .Select(row => new
            {
                Title = ((TextBox)row.Children[1]).Text.Trim(),
                IsDone = ((CheckBox)row.Children[0]).IsChecked == true
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = s.IsDone }))
            .ToList();

        SelectedAttachments = AttachmentsPanel.Children.OfType<Grid>()
            .Select(row => (AttachmentViewModel)row.Tag)
            .ToList();

        DialogResult = true;
        Close();
    }
}
