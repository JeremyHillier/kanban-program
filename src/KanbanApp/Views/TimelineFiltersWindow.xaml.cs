using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Choose which people, priorities and flags the Timeline shows. Show applies the choice (Result); Cancel leaves it.
public partial class TimelineFiltersWindow : Window
{
    private readonly List<(CheckBox Box, int Id)> _people = [];
    private readonly List<(CheckBox Box, string Name)> _priorities = [];
    private readonly List<(CheckBox Box, int Id)> _flags = [];

    public TimelineFilter Result { get; private set; } = TimelineFilter.None;

    public TimelineFiltersWindow(MainViewModel viewModel, TimelineFilter current)
    {
        InitializeComponent();
        AccessKeys.Bind(this);

        foreach (var person in viewModel.People.Where(p => p.IsActive || current.PersonIds.Contains(p.Id)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var box = NewBox(person.Name, current.PersonIds.Contains(person.Id));
            _people.Add((box, person.Id));
            PeoplePanel.Children.Add(box);
        }

        // The priorities in the order the task file ranks them, plus any a saved filter still names.
        foreach (var name in viewModel.Priorities.Names.Concat(current.Priorities.Except(viewModel.Priorities.Names)))
        {
            var box = NewBox(name, current.Priorities.Contains(name));
            _priorities.Add((box, name));
            PrioritiesPanel.Children.Add(box);
        }

        foreach (var flag in viewModel.Flags.Where(f => f.IsActive || current.FlagIds.Contains(f.Id)).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var box = NewBox(flag.Name, current.FlagIds.Contains(flag.Id));
            _flags.Add((box, flag.Id));
            FlagsPanel.Children.Add(box);
        }
    }

    private CheckBox NewBox(string text, bool isChecked) => new()
    {
        Content = text.Replace("_", "__"), IsChecked = isChecked, Margin = new Thickness(0, 3, 0, 3),
        Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
    };

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (box, _) in _people) box.IsChecked = false;
        foreach (var (box, _) in _priorities) box.IsChecked = false;
        foreach (var (box, _) in _flags) box.IsChecked = false;
    }

    private void Show_Click(object sender, RoutedEventArgs e)
    {
        Result = new TimelineFilter(
            _people.Where(p => p.Box.IsChecked == true).Select(p => p.Id).ToHashSet(),
            _priorities.Where(p => p.Box.IsChecked == true).Select(p => p.Name).ToHashSet(StringComparer.Ordinal),
            _flags.Where(f => f.Box.IsChecked == true).Select(f => f.Id).ToHashSet());
        DialogResult = true;
    }
}
