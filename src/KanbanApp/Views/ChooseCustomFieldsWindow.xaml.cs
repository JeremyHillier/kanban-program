using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// A tick list of the offered custom fields: the ones a new task in a project starts with (opened from Manage Projects).
public partial class ChooseCustomFieldsWindow : Window
{
    private readonly List<(CheckBox Box, CustomFieldDef Field)> _boxes = [];

    // The slots ticked, after Save.
    public List<int> SelectedSlots { get; private set; } = [];

    public ChooseCustomFieldsWindow(MainViewModel viewModel, string title, string intro, IEnumerable<int> selected)
    {
        InitializeComponent();
        Title = title;
        IntroText.Text = intro;

        var chosen = selected.ToHashSet();
        foreach (var field in viewModel.ActiveCustomFields)
        {
            var box = new CheckBox
            {
                Content = field.Name, IsChecked = chosen.Contains(field.Slot), Margin = new Thickness(0, 0, 0, 6),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };
            _boxes.Add((box, field));
            FieldsPanel.Children.Add(box);
        }

        // A field chosen earlier that is no longer offered is kept as it was, not dropped by a save.
        _hidden = chosen.Where(s => viewModel.ActiveCustomFields.All(f => f.Slot != s)).ToList();
    }

    private readonly List<int> _hidden;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SelectedSlots = [.. _boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Field.Slot), .. _hidden];
        DialogResult = true;
    }
}
