using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Related tasks on the task screen: the task's links to others (related to, depends on, blocks, child of,
// parent of), each a clickable title that opens that task over this one - so nothing typed here is lost by
// following it. The links are kept as rows in this window and written when the task is saved, like
// sub-tasks; the rules (no circles, one parent, no repeats) are CardLinkRules.
public partial class AddTaskWindow
{
    private sealed record RelatedEntry(int OtherId, RelationType Type);

    private readonly List<RelatedEntry> _related = [];

    // Tasks that have a task screen open right now, so following a link never opens a second window for one task.
    private static readonly Dictionary<int, AddTaskWindow> OpenEditors = [];

    // The rows as the links to store for this task.
    private List<CardLink> LinksFor(int cardId) => _related.Select(r => CardLinkRules.FromPerspective(cardId, r.Type, r.OtherId)).ToList();

    private void LoadRelated(CardViewModel card)
    {
        foreach (var related in _viewModel.RelatedTasksOf(card.Id)) _related.Add(new RelatedEntry(related.OtherId, related.Type));
        OpenEditors[card.Id] = this;
        Closed += (_, _) => { if (OpenEditors.TryGetValue(card.Id, out var open) && open == this) OpenEditors.Remove(card.Id); };
        RebuildRelatedRows();
    }

    private void RebuildRelatedRows()
    {
        RelatedPanel.Children.Clear();
        foreach (var entry in _related) RelatedPanel.Children.Add(BuildRelatedRow(entry));
        RelatedCountLabel.Text = _related.Count == 0 ? string.Empty : $"{_related.Count} linked";
    }

    private UIElement BuildRelatedRow(RelatedEntry entry)
    {
        var brief = _viewModel.BriefOf(entry.OtherId);
        var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var type = new TextBlock
        {
            Text = CardLinkRules.Label(entry.Type), VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        };
        row.Children.Add(type);

        var title = new TextBlock
        {
            Text = brief?.Title ?? "(task no longer exists)", TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            Cursor = brief is null ? Cursors.Arrow : Cursors.Hand, ToolTip = brief is null ? null : "Open this task",
            Foreground = (Brush)FindResource(brief is null ? "SecondaryTextBrush" : "AccentTextBrush")
        };
        if (brief is not null)
        {
            title.MouseEnter += (_, _) => title.TextDecorations = TextDecorations.Underline;
            title.MouseLeave += (_, _) => title.TextDecorations = null;
            title.MouseLeftButtonUp += (_, _) => OpenRelated(entry);
        }
        Grid.SetColumn(title, 1);
        row.Children.Add(title);

        var status = new TextBlock
        {
            Text = brief?.Status ?? "Gone", VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(8, 0, 6, 0),
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        };
        Grid.SetColumn(status, 2);
        row.Children.Add(status);

        var remove = new Button
        {
            Content = "✕", Padding = new Thickness(5, 0, 5, 0), ToolTip = "Remove this link", IsTabStop = false,
            Background = (Brush)FindResource("ButtonBackgroundBrush"), Foreground = (Brush)FindResource("PrimaryTextBrush")
        };
        AutomationProperties.SetName(remove, $"Remove link to {brief?.Title ?? "task"}");
        remove.Click += (_, _) =>
        {
            _related.Remove(entry);
            RebuildRelatedRows();
        };
        Grid.SetColumn(remove, 3);
        row.Children.Add(remove);
        return row;
    }

    private void AddRelated_Click(object sender, RoutedEventArgs e)
    {
        var picker = new RelatedTaskPickerWindow(_viewModel, _cardToEdit?.Id, _related.Select(r => r.OtherId)) { Owner = this };
        if (picker.ShowDialog() != true) return;

        // A new task has no id yet; 0 is never a task's, so it stands for it in the checks.
        var self = _cardToEdit?.Id ?? 0;
        var graph = _viewModel.Links.Where(l => !CardLinkRules.Involves(l, self))
            .Concat(_related.Select(r => CardLinkRules.FromPerspective(self, r.Type, r.OtherId))).ToList();
        var link = CardLinkRules.FromPerspective(self, picker.ChosenType, picker.ChosenId);
        if (CardLinkRules.Validate(graph, link) is { } problem)
        {
            Dialogs.Tell(this, "Link Not Added", problem, DialogTone.Warning);
            return;
        }

        _related.Add(new RelatedEntry(picker.ChosenId, picker.ChosenType));
        RebuildRelatedRows();
    }

    // Opens the linked task over this window. This task's own unsaved changes stay in this window, so
    // following a link loses nothing; the other task is saved or not by its own Save and Cancel.
    private void OpenRelated(RelatedEntry entry)
    {
        var card = _viewModel.FindCardById(entry.OtherId);
        if (card is null)
        {
            var brief = _viewModel.BriefOf(entry.OtherId);
            Dialogs.Tell(this, "Task Not on the Board",
                $"\"{brief?.Title ?? "That task"}\" is {(brief?.IsDeleted == true ? "deleted" : "archived")}.\n\nOpen {(brief?.IsDeleted == true ? "Deleted" : "Archived")} Tasks to bring it back.", DialogTone.Info);
            return;
        }
        if (OpenEditors.ContainsKey(card.Id))
        {
            Dialogs.Tell(this, "Already Open", $"\"{card.Title}\" is already open in another task window. Finish with that one first.", DialogTone.Info);
            return;
        }
        if (_viewModel.ColumnOf(card) is not { } column) return;

        var dialog = new AddTaskWindow(_viewModel, card, column) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            dialog.SaveTo(_viewModel, card);
            if (dialog.MarkDoneRequested && Application.Current.MainWindow is MainWindow board) board.MarkDoneFromElsewhere(card, _viewModel, () => { });
        }
        RebuildRelatedRows();
    }

    private string RelatedSignature() => string.Join("|", _related.Select(r => $"{r.Type}:{r.OtherId}"));
}
