using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The right-click menus: one for a single card, one for a multi-card selection.
public partial class MainWindow
{
    // Every right-click menu action runs after the menu has closed - see ShowQuickEditMenu for why.
    private System.Windows.Controls.MenuItem AddMenuItem(System.Windows.Controls.ItemsControl parent, string header, Action action,
        bool isEnabled = true, bool isChecked = false, string? gesture = null)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header, IsEnabled = isEnabled, IsChecked = isChecked, InputGestureText = gesture ?? string.Empty };
        item.Click += (_, _) => Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        parent.Items.Add(item);
        return item;
    }

    private static System.Windows.Controls.MenuItem AddSubmenu(System.Windows.Controls.ContextMenu menu, string header)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        menu.Items.Add(item);
        return item;
    }

    // A person or list name is data, not a caption: without this a name containing an underscore
    // would lose it to WPF's access-key handling.
    private static string MenuText(string name) => name.Replace("_", "__");

    // The card's right-click menu. Built fresh on every open so it reflects the card as it is now
    // (its column, priority, assignee, remaining flags, whether it has a website or an email).
    // Every entry reuses the path its button or quick-edit already takes, so a right-click Delete
    // or Move behaves exactly like the X or quick-move button, prompts included.
    //
    // Right-clicking a card that is part of a multi-card selection opens the selection's menu
    // instead. Right-clicking a card outside the selection drops the selection first (as dragging
    // one does), so what's highlighted always matches what a menu action would touch.
    private void Card_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;

        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = element,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
        };

        if (card.IsSelected && viewModel.SelectedCardCount > 1)
        {
            BuildSelectionMenu(menu, viewModel.SelectedCards, viewModel);
        }
        else
        {
            if (!card.IsSelected && viewModel.SelectedCardCount > 0) viewModel.ClearCardSelection();
            BuildCardMenu(menu, card, viewModel);
        }

        menu.IsOpen = true;
    }

    private void BuildCardMenu(System.Windows.Controls.ContextMenu menu, CardViewModel card, MainViewModel viewModel)
    {
        var currentColumn = viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));
        void Separator() => menu.Items.Add(new System.Windows.Controls.Separator());

        AddMenuItem(menu, "_Edit Task...", () => EditCard(card, viewModel), gesture: "Double-click");
        AddMenuItem(menu, "_Copy as Text", () => CopyToClipboard(CardTextFormatter.Format(card, currentColumn?.DisplayName ?? string.Empty)));
        AddMenuItem(menu, "Copy _Title", () => CopyToClipboard(card.Title));
        AddMenuItem(menu, "D_uplicate", () => viewModel.DuplicateCard(card));
        AddMenuItem(menu, "Save as Temp_late...", () => TemplatePrompts.SaveAs(this, viewModel, MainViewModel.TemplateFromCard(card)));
        Separator();

        var moveTo = AddSubmenu(menu, "_Move To");
        foreach (var column in viewModel.Columns)
        {
            var isCurrent = column == currentColumn;
            AddMenuItem(moveTo, MenuText(column.DisplayName), () => MoveCardDeferred(card, column, viewModel), isEnabled: !isCurrent, isChecked: isCurrent);
        }

        var priority = AddSubmenu(menu, "_Priority");
        foreach (var level in new[] { "High", "Medium", "Normal", "Low" })
        {
            AddMenuItem(priority, level, () => viewModel.SetCardPriority(card, level), isChecked: card.Priority == level);
        }

        var assign = AddSubmenu(menu, "_Assign To");
        AddMenuItem(assign, "Unassigned", () => viewModel.SetCardWho(card, null), isChecked: card.WhoId is null);
        foreach (var person in viewModel.People.Where(p => p.IsActive))
        {
            // Ticks the person on or off, leaving the others: a task can have several people.
            var label = card.People.Count > 1 && card.WhoId == person.Id ? $"{person.Name}  (lead)" : person.Name;
            AddMenuItem(assign, MenuText(label), () => viewModel.ToggleCardPerson(card, person), isChecked: card.IsAssignedTo(person.Id));
        }

        var project = AddSubmenu(menu, "P_roject");
        foreach (var option in viewModel.Projects.Where(p => p.IsActive))
        {
            AddMenuItem(project, MenuText(option.Name), () => viewModel.SetCardProject(card, option), isChecked: card.ProjectId == option.Id);
        }

        var waiting = AddSubmenu(menu, "Waitin_g On");
        AddMenuItem(waiting, card.IsWaiting ? "Change..." : "Set...", () => PromptWaitingOn([card], viewModel));
        AddMenuItem(waiting, "Clear", () => viewModel.SetCardWaitingOn(card, null), isEnabled: card.IsWaiting);
        waiting.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(waiting, "Manage List...", () => ManageWaitingOn(viewModel));

        var availableFlags = viewModel.Flags
            .Where(f => f.IsActive && card.Flags.All(cf => cf.Id != f.Id))
            .OrderBy(f => f.Name)
            .ToList();
        var addFlag = AddSubmenu(menu, "Add _Flag");
        addFlag.IsEnabled = availableFlags.Count > 0;
        foreach (var flag in availableFlags)
        {
            AddMenuItem(addFlag, MenuText(flag.Name), () => viewModel.AddFlagToCard(card, flag));
        }

        Separator();
        AddMenuItem(menu, "Open _Website", () => UrlLauncher.Open(card.WebsiteUrl, this), isEnabled: !string.IsNullOrWhiteSpace(card.WebsiteUrl));
        AddMenuItem(menu, "E_mail Task...", () => OutlookEmailHelper.ComposeCardEmail(this, card, OutlookEmailHelper.JoinRecipients(card.PeopleEmails), viewModel), isEnabled: card.CanEmailCard);
        Separator();
        AddMenuItem(menu, "_Delete...", () => DeleteCardWithConfirm(card, viewModel));
    }

    // The same menu for a whole selection. One-card-only entries (Edit, Open Website, Email) are
    // left out. A tick means every selected card already has that value; choosing an entry changes
    // only the cards that differ. The cards are captured when the menu opens, so the action applies
    // to what was highlighted at that moment.
    private void BuildSelectionMenu(System.Windows.Controls.ContextMenu menu, List<CardViewModel> cards, MainViewModel viewModel)
    {
        void Separator() => menu.Items.Add(new System.Windows.Controls.Separator());
        ColumnViewModel? ColumnOf(CardViewModel card) => viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));

        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = $"{cards.Count} tasks selected", IsEnabled = false, FontWeight = FontWeights.Bold });
        Separator();

        const string divider = "\r\n----------------------------------------\r\n\r\n";
        AddMenuItem(menu, "_Copy All as Text", () => CopyToClipboard(string.Join(divider,
            cards.Select(c => CardTextFormatter.Format(c, ColumnOf(c)?.DisplayName ?? string.Empty)))));
        AddMenuItem(menu, "Copy _Titles", () => CopyToClipboard(string.Join("\r\n", cards.Select(c => c.Title))));
        AddMenuItem(menu, "D_uplicate All", () => viewModel.DuplicateCards(cards));
        Separator();

        var moveTo = AddSubmenu(menu, "_Move All To");
        foreach (var column in viewModel.Columns)
        {
            var allHere = cards.All(column.Cards.Contains);
            AddMenuItem(moveTo, MenuText(column.DisplayName), () =>
            {
                // As with dragging a group: no "add a note?" per card, but a task set to force an
                // edit on completion still opens.
                var movedCards = viewModel.MoveCards(cards, column);
                foreach (var moved in movedCards) MaybePromptCompletionNote(moved, column, viewModel, askForNote: false);
                MaybePromptWaitingOn(movedCards, column, viewModel);
            }, isEnabled: !allHere, isChecked: allHere);
        }

        var priority = AddSubmenu(menu, "_Priority");
        foreach (var level in new[] { "High", "Medium", "Normal", "Low" })
        {
            AddMenuItem(priority, level, () => viewModel.SetCardsPriority(cards, level), isChecked: cards.All(c => c.Priority == level));
        }

        var assign = AddSubmenu(menu, "_Assign To");
        AddMenuItem(assign, "Unassigned", () => viewModel.SetCardsWho(cards, null), isChecked: cards.All(c => c.WhoId is null));
        foreach (var person in viewModel.People.Where(p => p.IsActive))
        {
            // Ticked only when every task has them; clicking adds them to the ones that do not,
            // or takes them off all of them when they are already on every one.
            var onAll = cards.All(c => c.IsAssignedTo(person.Id));
            AddMenuItem(assign, MenuText(person.Name), () => { if (onAll) viewModel.RemovePersonFromCards(cards, person); else viewModel.AddPersonToCards(cards, person); }, isChecked: onAll);
        }

        var project = AddSubmenu(menu, "P_roject");
        foreach (var option in viewModel.Projects.Where(p => p.IsActive))
        {
            AddMenuItem(project, MenuText(option.Name), () => viewModel.SetCardsProject(cards, option), isChecked: cards.All(c => c.ProjectId == option.Id));
        }

        var waiting = AddSubmenu(menu, "Waitin_g On");
        AddMenuItem(waiting, "Set...", () => PromptWaitingOn(cards, viewModel));
        AddMenuItem(waiting, "Clear", () => viewModel.SetCardsWaitingOn(cards, null), isEnabled: cards.Any(c => c.IsWaiting));
        waiting.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(waiting, "Manage List...", () => ManageWaitingOn(viewModel));

        // A flag is offered while at least one selected card lacks it.
        var availableFlags = viewModel.Flags
            .Where(f => f.IsActive && cards.Any(c => c.Flags.All(cf => cf.Id != f.Id)))
            .OrderBy(f => f.Name)
            .ToList();
        var addFlag = AddSubmenu(menu, "Add _Flag");
        addFlag.IsEnabled = availableFlags.Count > 0;
        foreach (var flag in availableFlags)
        {
            AddMenuItem(addFlag, MenuText(flag.Name), () => viewModel.AddFlagToCards(cards, flag));
        }

        Separator();
        AddMenuItem(menu, "Clear _Selection", viewModel.ClearCardSelection, gesture: "Esc");
        Separator();
        AddMenuItem(menu, $"_Delete {cards.Count} Tasks...", () => DeleteCardsWithConfirm(cards, viewModel));
    }

    private void CopyToClipboard(string text)
    {
        // Another program (a clipboard manager, a remote-desktop session) can briefly hold the
        // clipboard open, which makes SetText throw. Say so rather than letting it reach the crash
        // handler, so the user knows to just try again.
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            Dialogs.Tell(this, "Could Not Copy", "The task was not copied.\n\nAnother program is using the clipboard. Try again in a moment.", DialogTone.Warning);
        }
    }
}
