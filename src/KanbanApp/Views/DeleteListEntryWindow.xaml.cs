using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Asked when a project or person that tasks use is deleted: where those tasks go. The first choice leaves
// them without, as deleting always did; any other moves them all there in the same step.
public partial class DeleteListEntryWindow : Window
{
    internal sealed record Choice(string Name, IManagedItem? Item);

    internal IReadOnlyList<Choice> Choices { get; }

    // What was picked, once Delete is clicked. Null leaves the tasks without.
    public IManagedItem? MoveTo { get; private set; }

    internal DeleteListEntryWindow(ManagedListKind kind, IManagedItem item, int taskCount)
    {
        InitializeComponent();
        Title = kind.DeleteTitle;
        QuestionText.Text = kind.DeleteQuestion(item.Name);
        UsageText.Text = taskCount == 1 ? "1 task uses it. Choose where it goes." : $"{taskCount} tasks use it. Choose where they go.";

        Choices = [new Choice(kind.MoveNone ?? "None", null), .. kind.MoveChoices(item).Select(i => new Choice(i.Name, i))];
        MoveToComboBox.ItemsSource = Choices;
        MoveToComboBox.SelectedIndex = 0;
        MoveToComboBox.IsEnabled = Choices.Count > 1;
        HintText.Text = Choices.Count > 1
            ? string.Format(kind.MoveHint ?? "This cannot be undone.", item.Name)
            : $"There is no other active {kind.Noun} to move them to. This cannot be undone.";

        Loaded += (_, _) => MoveToComboBox.Focus();
    }

    // Picks a choice as if from the list: used by the tests.
    internal void Choose(IManagedItem? item) => MoveToComboBox.SelectedItem = Choices.First(c => ReferenceEquals(c.Item, item));

    internal void Delete_Click(object sender, RoutedEventArgs e)
    {
        MoveTo = (MoveToComboBox.SelectedItem as Choice)?.Item;
        if (IsVisible) DialogResult = true;
    }
}
