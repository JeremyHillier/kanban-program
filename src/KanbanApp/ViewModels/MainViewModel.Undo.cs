using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// Undo (Ctrl+Z) for changes to tasks. It works by copy rather than by reversing each kind of
// action: just before an action touches some tasks, those tasks are copied as stored
// (DatabaseService.SnapshotCards), and undoing puts the copies back and takes away any task the
// action created (a duplicate, a recurring task's next occurrence). That one mechanism covers every
// action below, including whatever a future one changes, as long as it wraps itself in RecordUndo.
//
// Recorded: add, edit, the quick-edits (priority, due date, who, project, flags, sub-task ticks),
// move, drag-reorder, duplicate, delete, Archive Done, import, and the group versions of those.
// Not recorded: anything on the managed lists or in Settings, attachments (their files move and
// are erased on disk, so the attachment list is left as it is), and the Archived/Deleted screens.
//
// The list lives for the session only and holds the last MaxUndoSteps actions. There is no redo.
public partial class MainViewModel
{
    private const int MaxUndoSteps = 30;

    private sealed class UndoStep(string label)
    {
        public string Label { get; } = label;
        public Dictionary<int, CardSnapshot> Before { get; } = [];
        public List<int> CreatedIds { get; } = [];

        // Cards the action took off the board (delete, archive), kept so Undo can put the very same
        // card back rather than a look-alike - anything still holding it stays valid.
        public Dictionary<int, CardViewModel> Removed { get; } = [];

        // Set only by a drag-reorder: the whole column's stored order and the sort that was active,
        // since reordering rewrites the first and switches the second to manual.
        public List<(int CardId, int SortOrder)>? ColumnOrder { get; set; }
        public int? ReorderedColumnId { get; set; }
        public List<SortKey>? SortKeys { get; set; }
    }

    private readonly List<UndoStep> _undoSteps = [];
    private UndoStep? _recording;
    private int _recordingDepth;
    private bool _undoing;

    public bool CanUndo => _undoSteps.Count > 0;

    // What the next Undo would take back, e.g. "Move 3 tasks" - shown on the Undo button's tooltip.
    public string? UndoDescription => _undoSteps.Count > 0 ? _undoSteps[^1].Label : null;

    public string UndoToolTip => UndoDescription is { } next ? $"Undo: {next} (Ctrl+Z)" : "Nothing to undo (Ctrl+Z)";

    // A short line shown at the foot of the board for a few seconds ("Undid: ..."). The window
    // sets it and clears it; empty means hidden.
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void NotifyUndoChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(UndoToolTip));
    }

    internal static string DescribeAction(string verb, IReadOnlyCollection<CardViewModel> cards, string? suffix = null)
    {
        var what = cards.Count == 1 ? Quoted(cards.First().Title) : $"{cards.Count} tasks";
        return suffix is null ? $"{verb} {what}" : $"{verb} {what} {suffix}";
    }

    internal static string DescribeAction(string verb, string title) => $"{verb} {Quoted(title)}";

    private static string Quoted(string title) => $"\"{(title.Length > 40 ? title[..37] + "..." : title)}\"";

    // Wrap an action in this, before it changes anything: using var undo = RecordUndo(...).
    // Actions nest (a group move moves each card; completing a recurring task adds the next one) -
    // everything inside the outermost RecordUndo becomes one step under its label.
    private IDisposable RecordUndo(string label, IEnumerable<CardViewModel> cards, ColumnViewModel? reordering = null)
    {
        if (_undoing) return NoUndo.Instance;

        _recording ??= new UndoStep(label);
        _recordingDepth++;

        // Inside an outer step, a card is only copied the first time it is named - that is its
        // state before the step began.
        var ids = cards.Select(c => c.Id).Where(id => !_recording.Before.ContainsKey(id) && !_recording.CreatedIds.Contains(id)).ToList();
        foreach (var snapshot in _db.SnapshotCards(ids)) _recording.Before[snapshot.CardId] = snapshot;

        if (reordering is not null && _recording.ColumnOrder is null)
        {
            _recording.ReorderedColumnId = reordering.Id;
            _recording.ColumnOrder = _db.GetColumnCardOrder(reordering.Id).Select((id, i) => (id, i)).ToList();
            _recording.SortKeys = [.. _sortKeys];
        }

        return new UndoScope(this);
    }

    private void FinishRecording()
    {
        if (--_recordingDepth > 0 || _recording is null) return;

        var step = _recording;
        _recording = null;
        if (step.Before.Count == 0 && step.CreatedIds.Count == 0 && step.ColumnOrder is null) return;

        _undoSteps.Add(step);
        if (_undoSteps.Count > MaxUndoSteps) _undoSteps.RemoveAt(0);
        NotifyUndoChanged();
    }

    private sealed class UndoScope(MainViewModel owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            owner.FinishRecording();
        }
    }

    private sealed class NoUndo : IDisposable
    {
        public static readonly NoUndo Instance = new();
        public void Dispose() { }
    }

    private CardViewModel? FindCard(int cardId) => Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Id == cardId);

    // Takes back the most recent recorded action and returns its description, or null if there is
    // nothing to undo.
    public string? Undo()
    {
        if (_undoSteps.Count == 0 || _recording is not null) return null;

        var step = _undoSteps[^1];
        _undoSteps.RemoveAt(_undoSteps.Count - 1);
        _undoing = true;
        try
        {
            ClearCardSelection();
            RemoveCreatedCards(step);

            var restoredIds = _db.RestoreCards(step.Before.Values, step.Label);
            if (step.ColumnOrder is not null) _db.UpdateSortOrders(step.ColumnOrder);
            ShowRestoredCards(restoredIds, step);

            if (step.SortKeys is not null)
            {
                _sortKeys.Clear();
                _sortKeys.AddRange(step.SortKeys);
                NotifySortRanksChanged();
            }

            if (step.ReorderedColumnId is { } columnId && Columns.FirstOrDefault(c => c.Id == columnId) is { } reordered)
            {
                var position = StoredPositions(reordered);
                ArrangeColumn(reordered, reordered.Cards.OrderBy(c => position.GetValueOrDefault(c.Id, int.MaxValue)).ToList());
            }

            ApplySort();
            RefreshDashboardStats();
        }
        finally
        {
            _undoing = false;
            NotifyUndoChanged();
        }

        return step.Label;
    }

    // A task the undone action created goes away again. One with attachments goes to the Deleted
    // list rather than being erased, because erasing would delete its files and there is no redo.
    // One that has since left the board (archived, deleted) is left where it is.
    private void RemoveCreatedCards(UndoStep step)
    {
        foreach (var id in step.CreatedIds)
        {
            if (FindCard(id) is not { } card) continue;

            var column = Columns.First(c => c.Cards.Contains(card));
            if (card.Attachments.Count > 0)
            {
                ReconcileAttachmentLocations(card, "Deleted");
                _db.DeleteCard(card.Id, card.Title, column.Name);
            }
            else
            {
                _db.PermanentlyDeleteCard(card.Id, card.Title, "the board (Undo)");
            }

            column.Cards.Remove(card);
        }
    }

    // Brings the board in line with the restored rows: a card still on the board takes the
    // restored values (and changes column if need be); one that had left it (deleted, archived)
    // comes back. Each lands where its stored order puts it among the cards already showing.
    private void ShowRestoredCards(List<int> restoredIds, UndoStep step)
    {
        var criteria = BuildFilterCriteria();

        foreach (var group in _db.GetCards(onlyIds: restoredIds).GroupBy(c => c.ColumnId))
        {
            if (Columns.FirstOrDefault(c => c.Id == group.Key) is not { } column) continue;

            var position = StoredPositions(column);
            var items = group.OrderBy(c => position.GetValueOrDefault(c.Id, int.MaxValue)).ToList();
            using var bulk = items.Count > ColumnViewModel.BulkChangeThreshold ? column.BeginBulkChange() : null;

            foreach (var item in items)
            {
                var fresh = BuildCardViewModel(item);
                var card = FindCard(item.Id) ?? step.Removed.GetValueOrDefault(item.Id);
                if (card is null) card = fresh;
                else card.TakeValuesFrom(fresh);

                var currentColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
                if (currentColumn is null) card.IsSelected = false; // it may have been deleted while selected
                if (currentColumn != column)
                {
                    currentColumn?.Cards.Remove(card);
                    var myPosition = position.GetValueOrDefault(card.Id, int.MaxValue);
                    var index = column.Cards.Count(c => position.GetValueOrDefault(c.Id, int.MaxValue) < myPosition);
                    column.Cards.Insert(index, card);
                }

                SetCardVisible(card, Matches(card, criteria));
                ReconcileAttachmentLocations(card, column.Name == "Done" ? "Done" : null);
            }
        }
    }

    private Dictionary<int, int> StoredPositions(ColumnViewModel column) =>
        _db.GetColumnCardOrder(column.Id).Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i);

    private CardViewModel BuildCardViewModel(CardItem card) => new(card)
    {
        ProjectName = ResolveProjectName(card.ProjectId),
        GoalName = ResolveGoalName(card.GoalId),
        People = ResolvePeople(card.PeopleIds),
        Flags = ResolveFlags(card.FlagIds),
        SubTasks = card.SubTasks.Select(s => new SubTaskViewModel(s)).ToList(),
        Attachments = card.Attachments.Select(a => new AttachmentViewModel(a)).ToList(),
        LastUpdated = card.LastUpdated
    };
}
