using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Change several tasks at once. Each field has a tick box; only the ticked fields are changed, so a task keeps
// everything else it has. Apply shows what will change and asks before doing it.
public partial class MassUpdateWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly int _count;
    private readonly List<(CheckBox Box, Control[] Controls)> _rows = [];

    private ComboBox _project = null!, _goal = null!, _personMode = null!, _person = null!, _priority = null!, _column = null!, _dueMode = null!, _color = null!, _flagMode = null!, _flag = null!;
    private DatePicker _dueDate = null!;
    private TextBox _shift = null!, _waiting = null!;
    private CheckBox _projectBox = null!, _goalBox = null!, _peopleBox = null!, _priorityBox = null!, _columnBox = null!, _dueBox = null!, _colorBox = null!, _flagBox = null!, _waitingBox = null!;

    public MassUpdateChanges Changes { get; private set; } = new();

    public MassUpdateWindow(MainViewModel viewModel, int count)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _viewModel = viewModel;
        _count = count;
        IntroText.Text = $"{count} task{(count == 1 ? "" : "s")} selected. Tick the fields to change; everything you leave unticked stays as it is.";

        (_projectBox, _project) = Row("Project", Combo(viewModel.Projects.Where(p => p.IsActive).Select(p => (object)p).ToList()));
        (_goalBox, _goal) = Row("Goal", Combo([(object)"No Goal", .. viewModel.Goals.Where(g => g.IsOffered)]));
        _personMode = Combo(["Set to only", "Add", "Take off"]);
        _person = Combo([(object)"Unassigned", .. viewModel.People.Where(p => p.IsActive)]);
        _peopleBox = RowOf("Assigned to", _personMode, _person);
        (_priorityBox, _priority) = Row("Priority", Combo(viewModel.Priorities.Names.Cast<object>().ToList()));
        (_columnBox, _column) = Row("Category (column)", Combo(viewModel.Columns.Cast<object>().ToList(), "DisplayName"));
        _dueMode = Combo(["Set to", "Clear", "Shift by days"]);
        _dueDate = new DatePicker { Width = 130, SelectedDate = DateTime.Today };
        _shift = new TextBox { Width = 50, Text = "1", Padding = new Thickness(4), ToolTip = "Days later (a minus sign for earlier). Start dates move with the due dates." };
        _dueBox = RowOf("Due date", _dueMode, _dueDate, _shift);
        (_colorBox, _color) = Row("Colour", Combo([(object)"None", .. CardColors.All.Select(c => c.Key)]));
        _flagMode = Combo(["Add", "Remove"]);
        _flag = Combo(viewModel.Flags.Where(f => f.IsActive).Cast<object>().ToList());
        _flagBox = RowOf("Flag", _flagMode, _flag);
        _waiting = new TextBox { Width = 250, Padding = new Thickness(4), ToolTip = "What the tasks are waiting on. Leave empty to clear it." };
        _waitingBox = RowOf("Waiting on", _waiting);

        foreach (var (box, _) in _rows) { box.Checked += (_, _) => Update(); box.Unchecked += (_, _) => Update(); }
        foreach (var c in _rows.SelectMany(r => r.Controls))
        {
            if (c is ComboBox combo) combo.SelectionChanged += (_, _) => Update();
            else if (c is TextBox text) text.TextChanged += (_, _) => Update();
            else if (c is DatePicker date) date.SelectedDateChanged += (_, _) => Update();
        }
        Update();
    }

    private static ComboBox Combo(IReadOnlyList<object> items, string? member = null)
    {
        var combo = new ComboBox { Width = 250, Padding = new Thickness(6, 3, 6, 3), DisplayMemberPath = member ?? "Name" };
        foreach (var item in items) combo.Items.Add(item);
        // Plain strings have no Name to show.
        if (items.Count > 0 && items.All(i => i is string)) combo.DisplayMemberPath = null;
        else if (items.Any(i => i is string)) combo.ItemTemplate = MixedTemplate(member ?? "Name");
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    private static DataTemplate MixedTemplate(string member)
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = new MixedNameConverter(member) });
        template.VisualTree = factory;
        return template;
    }

    private sealed class MixedNameConverter(string member) : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value as string ?? value?.GetType().GetProperty(member)?.GetValue(value)?.ToString() ?? string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private (CheckBox, ComboBox) Row(string label, ComboBox combo) => (RowOf(label, combo), combo);

    private CheckBox RowOf(string label, params Control[] controls)
    {
        var box = new CheckBox { Content = label, Width = 130, VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        line.Children.Add(box);
        foreach (var c in controls)
        {
            c.IsEnabled = false;
            c.Margin = new Thickness(0, 0, 6, 0);
            line.Children.Add(c);
        }
        box.Checked += (_, _) => { foreach (var c in controls) c.IsEnabled = true; };
        box.Unchecked += (_, _) => { foreach (var c in controls) c.IsEnabled = false; };
        Rows.Children.Add(line);
        _rows.Add((box, controls));
        return box;
    }

    private MassUpdateChanges Build()
    {
        var person = _person.SelectedItem as PersonViewModel;
        var dueMode = _dueMode.SelectedIndex;
        int.TryParse(_shift.Text.Trim(), out var days);
        return new MassUpdateChanges
        {
            SetProject = _projectBox.IsChecked == true, Project = _project.SelectedItem as ProjectViewModel,
            SetGoal = _goalBox.IsChecked == true, Goal = _goal.SelectedItem as GoalViewModel,
            People = _peopleBox.IsChecked != true ? PeopleChange.None : _personMode.SelectedIndex switch { 0 => PeopleChange.Replace, 1 => PeopleChange.Add, _ => PeopleChange.Remove },
            Person = person,
            Priority = _priorityBox.IsChecked == true ? _priority.SelectedItem as string : null,
            Column = _columnBox.IsChecked == true ? _column.SelectedItem as ColumnViewModel : null,
            Due = _dueBox.IsChecked != true ? DueChange.None : dueMode switch { 0 => DueChange.Set, 1 => DueChange.Clear, _ => DueChange.Shift },
            DueDate = _dueDate.SelectedDate, ShiftDays = days,
            SetColor = _colorBox.IsChecked == true, Color = _color.SelectedItem as string is { } key && key != "None" ? key : null,
            FlagAction = _flagBox.IsChecked != true ? FlagChange.None : _flagMode.SelectedIndex == 0 ? FlagChange.Add : FlagChange.Remove,
            Flag = _flag.SelectedItem as FlagViewModel,
            SetWaiting = _waitingBox.IsChecked == true, WaitingOn = _waiting.Text,
        };
    }

    private void Update()
    {
        if (_dueMode is null) return;
        _dueDate.Visibility = _dueMode.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        _shift.Visibility = _dueMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        _person.IsEnabled = _peopleBox.IsChecked == true;
        Changes = Build();
        ApplyButton.IsEnabled = !Changes.IsEmpty;
        SummaryText.Text = Changes.IsEmpty ? "Nothing ticked yet." : string.Join("\n", Changes.Lines.Select(l => "•  " + l));
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Changes = Build();
        if (Changes.IsEmpty) return;
        if (Changes.Due == DueChange.Shift && Changes.ShiftDays == 0)
        {
            Dialogs.Tell(this, "Mass Update", "Type the number of days to shift the dates by.");
            return;
        }
        if (Changes.Due == DueChange.Set && Changes.DueDate is null)
        {
            Dialogs.Tell(this, "Mass Update", "Pick the due date to set.");
            return;
        }
        if (!Dialogs.Confirm(this, DialogMessage.Ask("Mass Update",
                $"Change {_count} task{(_count == 1 ? "" : "s")}?\n\n" + string.Join("\n", Changes.Lines.Select(l => "•  " + l)) + "\n\nUndo takes it all back in one step.", "Change Them"))) return;
        DialogResult = true;
    }
}
