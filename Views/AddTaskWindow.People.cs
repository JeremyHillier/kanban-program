using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The Who picker: a drop-down of tickboxes, since a task can be assigned to several people.
// _selectedPeople keeps them in the order they were ticked - the first is the lead.
public partial class AddTaskWindow
{
    private readonly List<PersonViewModel> _selectedPeople = [];

    private void SetSelectedPeople(IEnumerable<PersonViewModel> people)
    {
        _selectedPeople.Clear();
        _selectedPeople.AddRange(people.DistinctBy(p => p.Id));
        RebuildWhoItems();
    }

    // Active people, plus anyone already on the task who has since been made inactive.
    private void RebuildWhoItems()
    {
        WhoPanel.Children.Clear();
        var listed = _viewModel.People.Where(p => p.IsActive || _selectedPeople.Any(s => s.Id == p.Id)).ToList();

        if (listed.Count == 0)
        {
            WhoPanel.Children.Add(new TextBlock
            {
                Text = "Nobody on the list yet - click + above to add a person.",
                TextWrapping = TextWrapping.Wrap, MaxWidth = 220, Margin = new Thickness(0, 2, 0, 2),
                Foreground = (Brush)FindResource("SecondaryTextStrongBrush")
            });
        }

        foreach (var person in listed)
        {
            var isSelected = _selectedPeople.Any(s => s.Id == person.Id);
            var isLead = isSelected && _selectedPeople[0].Id == person.Id;

            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            if (isSelected && !isLead)
            {
                var makeLead = new Button
                {
                    Content = "make lead", Tag = person, Style = (Style)FindResource("WhoLeadLinkStyle"),
                    Margin = new Thickness(16, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"Make {person.Name} the lead on this task"
                };
                makeLead.Click += MakeLead_Click;
                DockPanel.SetDock(makeLead, Dock.Right);
                row.Children.Add(makeLead);
            }

            var box = new CheckBox
            {
                Content = isLead && _selectedPeople.Count > 1 ? $"{person.Name}  (lead)" : person.Name,
                Tag = person, IsChecked = isSelected, VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            };
            box.Checked += WhoBox_Changed;
            box.Unchecked += WhoBox_Changed;
            row.Children.Add(box);
            WhoPanel.Children.Add(row);
        }

        WhoSummaryText.Text = _selectedPeople.Count == 0 ? "Unassigned" : string.Join(", ", _selectedPeople.Select(p => p.Name));
        WhoSummaryText.Foreground = (Brush)FindResource(_selectedPeople.Count == 0 ? "SecondaryTextStrongBrush" : "PrimaryTextBrush");
        WhoButton.ToolTip = _selectedPeople.Count > 1
            ? $"Assigned to {string.Join(", ", _selectedPeople.Select(p => p.Name))}. {_selectedPeople[0].Name} is the lead."
            : "Tick everyone this task is assigned to. The first person ticked is the lead.";

        RefreshEmailButton();
    }

    private void WhoBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: PersonViewModel person } box) return;

        _selectedPeople.RemoveAll(p => p.Id == person.Id);
        if (box.IsChecked == true) _selectedPeople.Add(person);

        // Rebuilt after this click has finished, not during it.
        Dispatcher.BeginInvoke(RebuildWhoItems, DispatcherPriority.Background);
    }

    private void MakeLead_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PersonViewModel person }) return;

        _selectedPeople.RemoveAll(p => p.Id == person.Id);
        _selectedPeople.Insert(0, person);
        Dispatcher.BeginInvoke(RebuildWhoItems, DispatcherPriority.Background);
    }

    // With StaysOpen off, a click on the button while the list is open would close it (the click
    // is outside the list) and then open it again (the click is on the button). The button sits
    // out while the list is open so that click just closes it.
    private void WhoPopup_Opened(object? sender, EventArgs e) => WhoButton.IsHitTestVisible = false;

    private void WhoPopup_Closed(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => WhoButton.IsHitTestVisible = true, DispatcherPriority.Input);

    // Reacts to who is ticked right now rather than to the saved task, so ticking someone with an
    // email address lights the button up straight away.
    private void RefreshEmailButton()
    {
        var hasEmail = _cardToEdit is not null && SelectedPeopleEmails.Count > 0;
        EmailButton.Visibility = hasEmail ? Visibility.Visible : Visibility.Collapsed;
    }

    private List<string> SelectedPeopleEmails => _selectedPeople
        .Select(p => p.Email?.Trim()).Where(e => !string.IsNullOrEmpty(e)).Select(e => e!)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        if (_cardToEdit is null || SelectedPeopleEmails.Count == 0) return;

        OutlookEmailHelper.ComposeCardEmail(this, _cardToEdit, OutlookEmailHelper.JoinRecipients(SelectedPeopleEmails), _viewModel);
    }

    private void NewWho_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Person", "Person's name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddPerson(dialog.Value);
        var added = _viewModel.People.FirstOrDefault(p => string.Equals(p.Name, dialog.Value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (added is not null && _selectedPeople.All(p => p.Id != added.Id)) _selectedPeople.Add(added);
        RebuildWhoItems();
    }

    private void DeleteWho_Click(object sender, RoutedEventArgs e) => SetSelectedPeople([]);
}
