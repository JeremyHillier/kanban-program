using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The Quick Add tasks still to review. Review closes this list and hands the board the task to start from
// (StartFrom), so the full task window opens in its place with the review strip; Mark Reviewed clears tasks from here.
// Right-click works on the chosen tasks: review, mark reviewed, delete, and the quick edits (priority, due date,
// project, who) - what is usually done to a hurried entry before it is looked at properly.
public partial class QuickEntriesWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Set when the user chose Review: the task the board should open first.
    public CardViewModel? StartFrom { get; private set; }

    public QuickEntriesWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _viewModel = viewModel;
        EntryList.ContextMenu = new ContextMenu();
        EntryList.ContextMenuOpening += EntryList_ContextMenuOpening;
        EntryList.PreviewMouseRightButtonDown += EntryList_PreviewMouseRightButtonDown;
        Refresh();
    }

    // Shows what is still to review. The tasks that were chosen stay chosen where they are still on the list.
    private void Refresh()
    {
        var chosen = EntryList.SelectedItems.OfType<CardViewModel>().ToList();
        var entries = _viewModel.QuickEntriesToReview();
        EntryList.ItemsSource = entries;
        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ReviewButton.IsEnabled = entries.Count > 0;

        var stillThere = chosen.Where(entries.Contains).ToList();
        if (stillThere.Count > 0) foreach (var card in stillThere) EntryList.SelectedItems.Add(card);
        else if (entries.Count > 0) EntryList.SelectedIndex = 0;
        UpdateButtons();
    }

    private List<CardViewModel> Chosen() => EntryList.SelectedItems.OfType<CardViewModel>().ToList();

    private void UpdateButtons() => MarkReviewedButton.IsEnabled = EntryList.SelectedItems.Count > 0;

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void EntryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || EntryList.SelectedItems.Count == 0) return;
        DeleteChosen();
        e.Handled = true;
    }

    private void MarkReviewed_Click(object sender, RoutedEventArgs e) => MarkChosenReviewed();

    private void MarkChosenReviewed()
    {
        var chosen = Chosen();
        if (chosen.Count == 0) return;
        _viewModel.MarkReviewed(chosen);
        Refresh();
    }

    private void Review_Click(object sender, RoutedEventArgs e) => ReviewFromChosen();

    private void ReviewFromChosen()
    {
        StartFrom = EntryList.SelectedItem as CardViewModel ?? (EntryList.Items.Count > 0 ? EntryList.Items[0] as CardViewModel : null);
        if (StartFrom is null) return;
        DialogResult = true;
    }

    // --- Right-click ------------------------------------------------------------------------------

    // A right-click on a task that is not chosen chooses it alone, like Explorer; on one that is, the choice stays.
    private void EntryList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current is not null and not ListBoxItem) current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        if (current is not ListBoxItem { IsSelected: false } item) return;

        EntryList.SelectedItems.Clear();
        item.IsSelected = true;
    }

    private void EntryList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var chosen = Chosen();
        if (chosen.Count == 0)
        {
            e.Handled = true; // nothing under the mouse: no menu
            return;
        }

        var menu = EntryList.ContextMenu!;
        menu.Items.Clear();
        var many = chosen.Count > 1;

        Add(menu, many ? "_Review From the First" : "_Review", ReviewFromChosen, bold: true, gesture: "Enter");
        Add(menu, "Mark Re_viewed", MarkChosenReviewed);
        menu.Items.Add(new Separator());

        var priority = new MenuItem { Header = "_Priority" };
        foreach (var name in _viewModel.Priorities.Names)
        {
            var item = Add(priority, Escape(name), () => Edit(() => _viewModel.SetCardsPriority(chosen, name)));
            item.IsChecked = chosen.All(c => c.Priority == name);
        }
        menu.Items.Add(priority);

        var due = new MenuItem { Header = "Due _Date" };
        Add(due, "Today", () => Edit(() => _viewModel.SetCardsDueDate(chosen, DateTime.Today)));
        Add(due, "Tomorrow", () => Edit(() => _viewModel.SetCardsDueDate(chosen, DateTime.Today.AddDays(1))));
        Add(due, "In a Week", () => Edit(() => _viewModel.SetCardsDueDate(chosen, DateTime.Today.AddDays(7))));
        Add(due, "No Due Date", () => Edit(() => _viewModel.SetCardsDueDate(chosen, null)), isEnabled: chosen.Any(c => c.DueDate is not null));
        menu.Items.Add(due);

        var project = new MenuItem { Header = "Pro_ject" };
        foreach (var candidate in _viewModel.Projects.Where(p => p.IsActive))
        {
            var item = Add(project, Escape(candidate.Name), () => Edit(() => _viewModel.SetCardsProject(chosen, candidate)));
            item.IsChecked = chosen.All(c => c.ProjectId == candidate.Id);
        }
        menu.Items.Add(project);

        var who = new MenuItem { Header = "_Who" };
        foreach (var person in _viewModel.People.Where(p => p.IsActive))
        {
            var item = Add(who, Escape(person.Name), () => Edit(() => _viewModel.SetCardsWho(chosen, person)));
            item.IsChecked = chosen.All(c => c.People.Count == 1 && c.WhoId == person.Id);
        }
        Add(who, "Nobody", () => Edit(() => _viewModel.SetCardsWho(chosen, null)), isEnabled: chosen.Any(c => c.People.Count > 0));
        menu.Items.Add(who);

        menu.Items.Add(new Separator());
        Add(menu, many ? "Copy _Titles" : "Copy _Title", () => Clipboard.SetText(string.Join(Environment.NewLine, chosen.Select(c => c.Title))));
        menu.Items.Add(new Separator());
        Add(menu, many ? $"_Delete {chosen.Count} Tasks..." : "_Delete...", DeleteChosen, gesture: "Delete");
    }

    // Underscores in a name are not access keys.
    private static string Escape(string text) => text.Replace("_", "__");

    private static MenuItem Add(ItemsControl parent, string header, Action action, bool bold = false, string? gesture = null, bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled, InputGestureText = gesture ?? string.Empty };
        if (bold) item.FontWeight = FontWeights.Bold;
        // Deferred, like the board's menus: the menu closes first, then the change is made.
        item.Click += (_, _) => item.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        parent.Items.Add(item);
        return item;
    }

    // A change made from the menu: the list shows it, and the tasks stay chosen.
    private void Edit(Action change)
    {
        change();
        Refresh();
    }

    // Asks first, the way the board does (a repeating task is asked about once for the whole group), then deletes; Undo
    // (Ctrl+Z on the board) brings them back.
    private void DeleteChosen()
    {
        var chosen = Chosen();
        if (chosen.Count == 0) return;

        var spawnNext = false;
        var recurring = chosen.Count(c => c.HasNextOccurrence);
        if (recurring > 0)
        {
            var dialog = chosen.Count == 1
                ? new DeleteRecurringTaskWindow(chosen[0].Title) { Owner = this }
                : new DeleteRecurringTaskWindow(chosen.Count, recurring) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SpawnNext is null) return;
            spawnNext = dialog.SpawnNext.Value;
        }
        else
        {
            var question = chosen.Count == 1
                ? $"Delete \"{chosen[0].Title}\"?\n\nUndo (Ctrl+Z) on the board brings it back."
                : $"Delete these {chosen.Count} tasks?\n\nUndo (Ctrl+Z) on the board brings them all back.";
            if (!Dialogs.Confirm(this, DialogMessage.AskDanger(chosen.Count == 1 ? "Delete Task" : "Delete Tasks", question, chosen.Count == 1 ? "Delete" : "Delete Them"))) return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _viewModel.DeleteCards(chosen, spawnNext);
            Refresh();
        }, DispatcherPriority.Background);
    }
}
