using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The Quick Add strip. Opened by MainWindow's system-wide hotkey, so it usually appears over some
// other program: it is always on top, sits in the upper middle of the main screen, and stays open
// after adding so several tasks can go in one after
// another. It deliberately does not close when it loses focus - switching away to copy something
// to paste in shouldn't throw away what was typed.
public partial class QuickAddWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Raised after each task is added, so the board can say so.
    public event Action<CardViewModel>? TaskAdded;

    public QuickAddWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        ProjectComboBox.ItemsSource = viewModel.Projects.Where(p => p.IsActive).ToList();
        ProjectComboBox.SelectedItem = viewModel.QuickAddDefaultProject;
        UpdatePreview();

        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + area.Height * 0.22;

        Loaded += (_, _) => FocusTaskBox();
        PreviewKeyDown += QuickAddWindow_PreviewKeyDown;
    }

    public void FocusTaskBox()
    {
        Activate();
        TaskTextBox.Focus();
        Keyboard.Focus(TaskTextBox);
    }

    private void QuickAddWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && !ProjectComboBox.IsDropDownOpen)
        {
            e.Handled = true;
            AddTask();
        }
    }

    private void AddTask()
    {
        var card = _viewModel.QuickAdd(TaskTextBox.Text, ProjectComboBox.SelectedItem as ProjectViewModel);
        if (card is null) return;

        TaskAdded?.Invoke(card);
        HeadingText.Text = $"Added \"{(card.Title.Length > 50 ? card.Title[..47] + "..." : card.Title)}\" - type another, or Esc to close";
        TaskTextBox.Clear();
    }

    private void TaskTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    // Says back what the codes were understood as, so a mistyped one is caught before Enter.
    private void UpdatePreview()
    {
        var parsed = _viewModel.ParseQuickAdd(TaskTextBox.Text);
        if (string.IsNullOrWhiteSpace(TaskTextBox.Text))
        {
            PreviewText.Text = $"Goes into To Do. Optional codes:  {QuickAddParser.Hint}";
            return;
        }

        var parts = new List<string> { $"To Do  ·  {parsed.Priority ?? "Normal"} priority" };
        if (parsed.WhoName is not null) parts.Add($"for {parsed.WhoName}");
        if (parsed.DueDate is { } due) parts.Add($"due {due:ddd, MMM d}");
        PreviewText.Text = string.Join("  ·  ", parts);
    }
}
