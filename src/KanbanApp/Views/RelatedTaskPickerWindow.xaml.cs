using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Picks a task to link to and how this task is related to it. Tasks already linked are not offered.
public partial class RelatedTaskPickerWindow : Window
{
    private readonly IReadOnlyList<LinkCandidate> _candidates;

    public int ChosenId { get; private set; }
    public RelationType ChosenType => TypeBox.SelectedItem is RelationType type ? type : RelationType.RelatedTo;

    public RelatedTaskPickerWindow(MainViewModel viewModel, int? thisTaskId, IEnumerable<int> alreadyLinked)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        var skip = alreadyLinked.ToHashSet();
        _candidates = viewModel.LinkCandidates(thisTaskId).Where(c => !skip.Contains(c.Id)).ToList();

        TypeBox.ItemsSource = CardLinkRules.Types;
        TypeBox.ItemTemplate = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = new TypeLabelConverter() });
        TypeBox.ItemTemplate.VisualTree = factory;
        TypeBox.SelectedItem = RelationType.RelatedTo;

        Refresh();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private sealed class TypeLabelConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is RelationType type ? CardLinkRules.Phrase(type) : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private void Refresh()
    {
        var words = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        TaskList.ItemsSource = _candidates
            .Where(c => words.All(w => c.Title.Contains(w, StringComparison.CurrentCultureIgnoreCase) || c.ProjectName.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
        if (TaskList.Items.Count > 0 && words.Length > 0) TaskList.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    // Down moves from the search box into the list, as in any search-and-pick box.
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || TaskList.Items.Count == 0) return;
        TaskList.Focus();
        if (TaskList.SelectedIndex < 0) TaskList.SelectedIndex = 0;
        e.Handled = true;
    }

    private void TaskList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AddButton.IsEnabled = TaskList.SelectedItem is LinkCandidate;
        ChosenText.Text = TaskList.SelectedItem is LinkCandidate chosen ? chosen.Title : string.Empty;
    }

    private void TaskList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TaskList.SelectedItem is LinkCandidate) Add_Click(sender, e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is not LinkCandidate chosen) return;
        ChosenId = chosen.Id;
        DialogResult = true;
    }
}
