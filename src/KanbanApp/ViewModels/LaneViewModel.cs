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

    public bool Matches(CardViewModel card) => Mode switch
    {
        SwimlaneMode.Project => card.ProjectId == Id,
        SwimlaneMode.Goal => card.GoalId == Id,
        SwimlaneMode.AssignedTo => card.WhoId == Id,
        _ => false
    };

    // The lane a task is in, for counting in one pass.
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
                    _view?.DetachFromSourceCollection();
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
        view.IsLiveFiltering = true;
        return view;
    }

    public void Detach()
    {
        Source.PropertyChanged -= _sourceChanged;
        _view?.DetachFromSourceCollection();
        _view = null;
    }
}
