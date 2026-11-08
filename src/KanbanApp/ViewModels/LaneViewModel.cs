using System.ComponentModel;
using System.Windows.Data;

namespace KanbanApp.ViewModels;

// What the board's horizontal lanes are grouped by. Off shows the board as it always was.
public enum SwimlaneMode { Off, Project, Goal, AssignedTo }

// One horizontal lane of the swimlane board: every workflow column again, showing only the tasks that belong to
// this project, goal or lead person (Id null is the lane for tasks with none). It only ever shows the board's own
// tasks, so filters, sorting, colours and everything on a card work as they do in the plain board.
public sealed class LaneViewModel : ObservableObject
{
    private readonly Action<LaneViewModel> _expandedChanged;

    public LaneViewModel(SwimlaneMode mode, int? id, string title, IEnumerable<ColumnViewModel> sources, bool isExpanded, Action<LaneViewModel> expandedChanged)
    {
        Mode = mode;
        Id = id;
        _title = title;
        _isExpanded = isExpanded;
        _expandedChanged = expandedChanged;
        Columns = sources.Select(s => new LaneColumnViewModel(s, this)).ToList();
    }

    public SwimlaneMode Mode { get; }
    public int? Id { get; }

    // Stable across renames, so a lane's expanded or collapsed state is kept.
    public string Key => KeyOf(Mode, Id);

    public static string KeyOf(SwimlaneMode mode, int? id) => $"{mode}:{(id is { } value ? value.ToString() : "none")}";

    public IReadOnlyList<LaneColumnViewModel> Columns { get; }

    private string _title;
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetField(ref _isExpanded, value)) return;
            OnPropertyChanged(nameof(Glyph));
            _expandedChanged(this);
        }
    }

    public string Glyph => IsExpanded ? "▾" : "▸";

    private int _count;
    public int Count
    {
        get => _count;
        set
        {
            if (SetField(ref _count, value)) OnPropertyChanged(nameof(CountText));
        }
    }

    public string CountText => $"{Count} task{(Count == 1 ? "" : "s")}";

    // False while the board is filtered and none of this lane's tasks are showing: the lane is left out then.
    private bool _isShown = true;
    public bool IsShown
    {
        get => _isShown;
        set => SetField(ref _isShown, value);
    }

    public bool Matches(CardViewModel card) => Mode switch
    {
        SwimlaneMode.Project => card.ProjectId == Id,
        SwimlaneMode.Goal => card.GoalId == Id,
        // Every person on a task has a lane for it, so a task with two people shows in both; none is the Unassigned lane.
        SwimlaneMode.AssignedTo => Id is { } person ? card.IsAssignedTo(person) : card.People.Count == 0,
        _ => false
    };

    // The lane a task is in, for counting in one pass (not Assigned To: a task can be in several of those).
    public static int? IdOf(SwimlaneMode mode, CardViewModel card) => mode switch
    {
        SwimlaneMode.Project => card.ProjectId,
        SwimlaneMode.Goal => card.GoalId,
        SwimlaneMode.AssignedTo => card.WhoId,
        _ => null
    };

    public void Detach()
    {
        foreach (var column in Columns) column.Detach();
    }
}

// A workflow column inside a lane. To the board's column template it is an ordinary column (same name, colour and
// card list - narrowed to the lane); to everything that moves or orders tasks it is its Source, the real column.
public sealed class LaneColumnViewModel : ColumnViewModel
{
    private ListCollectionView? _view;
    private readonly PropertyChangedEventHandler _sourceChanged;

    public ColumnViewModel Source { get; }
    public LaneViewModel Lane { get; }

    public LaneColumnViewModel(ColumnViewModel source, LaneViewModel lane) : base(source.Model, source.Background)
    {
        Source = source;
        Lane = lane;
        _sourceChanged = (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CardsView): // the real column was re-sorted or re-filtered wholesale: build this view again
                    Retire(_view);
                    _view = null;
                    OnPropertyChanged(nameof(CardsView));
                    break;
                case nameof(Background): Background = source.Background; break;
                case nameof(DisplayName): OnPropertyChanged(nameof(DisplayName)); break;
            }
        };
        source.PropertyChanged += _sourceChanged;
    }

    public override ListCollectionView CardsView => _view ??= CreateLaneView();

    private ListCollectionView CreateLaneView()
    {
        var view = new ListCollectionView(Source.Cards) { Filter = item => item is CardViewModel c && c.IsVisible && Lane.Matches(c) };
        view.LiveFilteringProperties.Add(nameof(CardViewModel.IsVisible));
        view.LiveFilteringProperties.Add(nameof(CardViewModel.ProjectId));
        view.LiveFilteringProperties.Add(nameof(CardViewModel.GoalId));
        view.LiveFilteringProperties.Add(nameof(CardViewModel.WhoId));
        view.LiveFilteringProperties.Add(nameof(CardViewModel.People));
        view.IsLiveFiltering = true;
        ((System.Collections.Specialized.INotifyCollectionChanged)view).CollectionChanged += (_, e) =>
        {
            if (e.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Add or System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                QueueDuplicateCheck(view);
        };
        return view;
    }

    // WPF's live filtering can list a task twice. When a task's lane changes (its project, goal or person) and the
    // column then moves it - a drop between cards, or the sort - before WPF has caught up on the change, the move adds
    // it to this lane and the catch-up adds it again. So after anything is added, the list is checked once more, still
    // before the screen is next drawn, and built again if a task is in it twice.
    private bool _duplicateCheckQueued;

    private void QueueDuplicateCheck(ListCollectionView view)
    {
        if (_duplicateCheckQueued) return;
        _duplicateCheckQueued = true;
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            _duplicateCheckQueued = false;
            if (!ReferenceEquals(view, _view)) return; // replaced or retired meanwhile
            var seen = new HashSet<object>();
            foreach (var item in view)
            {
                if (seen.Add(item)) continue;
                view.Refresh();
                return;
            }
        }, System.Windows.Threading.DispatcherPriority.DataBind);
    }

    public void Detach()
    {
        Source.PropertyChanged -= _sourceChanged;
        Retire(_view);
        _view = null;
    }

    // A wholesale change to the real column flips many cards at once, and this view (still on the same cards) queues
    // live-filtering work for each. Cut loose at once, that queued work then fails on it - once per lane column. So it
    // stops filtering live now and is let go only after the queued work has run.
    private static void Retire(ListCollectionView? view)
    {
        if (view is null) return;
        view.IsLiveFiltering = false;
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(view.DetachFromSourceCollection, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }
}
