using System.Collections.ObjectModel;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Swimlanes: the board drawn as horizontal lanes (one per project, goal or lead person), each with the
// workflow columns across it. The grouping and which lanes are collapsed are remembered in the task file.
// Nothing is added to a task: a lane is just the tasks whose Project, Goal or lead matches.
public partial class MainViewModel
{
    private const string SwimlaneModeKey = "SwimlaneMode";
    private const string SwimlaneCollapsedKey = "SwimlaneCollapsed";

    public ObservableCollection<LaneViewModel> Lanes { get; } = [];

    private SwimlaneMode _swimlaneMode;
    public SwimlaneMode SwimlaneMode
    {
        get => _swimlaneMode;
        private set
        {
            if (!SetField(ref _swimlaneMode, value)) return;
            OnPropertyChanged(nameof(IsSwimlanesOn));
            OnPropertyChanged(nameof(IsSwimlanesOff));
            OnPropertyChanged(nameof(SwimlaneButtonLabel));
        }
    }

    public bool IsSwimlanesOn => SwimlaneMode != SwimlaneMode.Off;
    public bool IsSwimlanesOff => !IsSwimlanesOn;

    public string SwimlaneButtonLabel => SwimlaneMode switch
    {
        SwimlaneMode.Project => "Lanes: Project",
        SwimlaneMode.Goal => "Lanes: Goal",
        SwimlaneMode.AssignedTo => "Lanes: Who",
        _ => "Swim lanes"
    };

    // Lanes the user collapsed, by LaneViewModel.Key (which includes the grouping, so each grouping keeps its own).
    private HashSet<string> _collapsedLanes = [];

    private void LoadSwimlanes()
    {
        _collapsedLanes = (_db.GetSetting(SwimlaneCollapsedKey) ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        SwimlaneMode = Enum.TryParse<SwimlaneMode>(_db.GetSetting(SwimlaneModeKey), out var mode) ? mode : SwimlaneMode.Off;
        RebuildLanes();
    }

    public void SetSwimlaneMode(SwimlaneMode mode)
    {
        if (mode == SwimlaneMode) return;
        SwimlaneMode = mode;
        _db.SetSetting(SwimlaneModeKey, mode.ToString());
        RebuildLanes();
    }

    public void SetAllLanesExpanded(bool expanded)
    {
        foreach (var lane in Lanes) lane.IsExpanded = expanded;
    }

    private void OnLaneExpandedChanged(LaneViewModel lane)
    {
        if (lane.IsExpanded) _collapsedLanes.Remove(lane.Key); else _collapsedLanes.Add(lane.Key);
        _db.SetSetting(SwimlaneCollapsedKey, string.Join("|", _collapsedLanes.Order()));
    }

    // The lanes for the current grouping: every active project, goal or person, then a last lane for tasks with none.
    private List<(int? Id, string Title)> DesiredLanes() => SwimlaneMode switch
    {
        SwimlaneMode.Project => [.. Projects.Where(p => p.IsActive).Select(p => ((int?)p.Id, p.Name)), (null, "No Project")],
        SwimlaneMode.Goal => [.. Goals.Where(g => g.IsOffered).Select(g => ((int?)g.Id, g.Name)), (null, "No Goal")],
        SwimlaneMode.AssignedTo => [.. People.Where(p => p.IsActive).Select(p => ((int?)p.Id, p.Name)), (null, "Unassigned")],
        _ => []
    };

    private void RebuildLanes()
    {
        foreach (var lane in Lanes) lane.Detach();
        Lanes.Clear();
        foreach (var (id, title) in DesiredLanes())
        {
            Lanes.Add(new LaneViewModel(SwimlaneMode, id, title, Columns, isExpanded: !_collapsedLanes.Contains(LaneViewModel.KeyOf(SwimlaneMode, id)), OnLaneExpandedChanged));
        }
        RefreshLaneCounts();
    }

    // Called after any change (RefreshDashboardStats): rebuilds only when the set of lanes changed, else just
    // keeps names and counts current.
    private void RefreshLanes()
    {
        if (!IsSwimlanesOn) return;

        var desired = DesiredLanes();
        if (!desired.Select(d => d.Id).SequenceEqual(Lanes.Select(l => l.Id)))
        {
            RebuildLanes();
            return;
        }

        for (var i = 0; i < desired.Count; i++) Lanes[i].Title = desired[i].Title;
        RefreshLaneCounts();
    }

    // One pass over the tasks, rather than one per lane.
    private void RefreshLaneCounts()
    {
        if (!IsSwimlanesOn) return;
        var counts = new Dictionary<int, int>();
        var none = 0;
        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.IsVisible))
        {
            if (LaneViewModel.IdOf(SwimlaneMode, card) is { } id) counts[id] = counts.GetValueOrDefault(id) + 1; else none++;
        }
        foreach (var lane in Lanes) lane.Count = lane.Id is { } id ? counts.GetValueOrDefault(id) : none;
    }

    // Puts tasks in a lane by changing the field it groups by (project, goal or lead). Tasks already in it are left alone.
    private void AssignToLane(LaneViewModel lane, IReadOnlyList<CardViewModel> cards)
    {
        var changing = cards.Where(c => !lane.Matches(c)).ToList();
        if (changing.Count == 0) return;

        switch (lane.Mode)
        {
            case SwimlaneMode.Project when lane.Id is { } projectId && Projects.FirstOrDefault(p => p.Id == projectId) is { } project:
                SetCardsProject(changing, project);
                break;
            case SwimlaneMode.Project:
                ChangeCards("Clear project of", changing, _ => true, c => { c.ProjectId = null; c.ProjectName = "No Project"; });
                break;
            case SwimlaneMode.Goal:
                SetCardsGoal(changing, lane.Id is { } goalId ? Goals.FirstOrDefault(g => g.Id == goalId) : null);
                break;
            case SwimlaneMode.AssignedTo:
                SetCardsWho(changing, lane.Id is { } personId ? People.FirstOrDefault(p => p.Id == personId) : null);
                break;
        }
    }

    // Tasks dropped on a column of a lane: moved to that workflow column and put in that lane, as one Undo step.
    // With reorderIndex, tasks already in the column are also placed there (a drop between cards).
    public List<CardViewModel> DropOnLane(LaneColumnViewModel target, IReadOnlyList<CardViewModel> cards, int? reorderIndex = null)
    {
        using var undo = RecordUndo(DescribeAction("Move", cards, $"to {target.Lane.Title}"), cards);
        var moved = MoveCards(cards, target.Source);
        AssignToLane(target.Lane, cards);
        if (reorderIndex is { } index && cards.All(target.Source.Cards.Contains)) ReorderCardsWithinColumn(cards, target.Source, index);
        return moved;
    }
}
