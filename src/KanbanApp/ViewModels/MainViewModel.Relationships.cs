using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Links between tasks (Models/CardLink): related to, depends on / blocks, parent / child. The list of
// every link is held here so each card can show a small indicator and which tasks it is waiting on;
// the rules live in CardLinkRules and the storage in DatabaseService.CardLinks.
public partial class MainViewModel
{
    private List<CardLink> _links = [];

    public IReadOnlyList<CardLink> Links => _links;

    public void LoadLinks()
    {
        _links = _db.GetCardLinks();
        RefreshLinkInfo();
    }

    // Each card's count of links, and how many of the tasks it depends on are still open (on the board, not Done).
    public void RefreshLinkInfo()
    {
        var cards = Columns.SelectMany(c => c.Cards).ToList();
        if (_links.Count == 0 && cards.All(c => c.RelatedCount == 0)) return;

        var byId = cards.ToDictionary(c => c.Id);
        var doneIds = DoneColumn?.Cards.Select(c => c.Id).ToHashSet() ?? [];
        foreach (var card in cards)
        {
            var mine = _links.Where(l => CardLinkRules.Involves(l, card.Id)).ToList();
            var waitingOn = mine.Count(l => l.Kind == LinkKind.DependsOn && l.FromId == card.Id && byId.ContainsKey(l.ToId) && !doneIds.Contains(l.ToId));
            var blocking = mine.Count(l => l.Kind == LinkKind.DependsOn && l.ToId == card.Id && byId.ContainsKey(l.FromId) && !doneIds.Contains(l.FromId));
            card.SetLinkInfo(mine.Count, waitingOn, blocking);
        }
    }

    public CardViewModel? FindCardById(int id) => Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Id == id);

    public ColumnViewModel? ColumnOf(CardViewModel card) => Columns.FirstOrDefault(c => c.Cards.Contains(card));

    // A task's links as it sees them, with the other task's title and where it is now.
    public IReadOnlyList<RelatedTask> RelatedTasksOf(int cardId)
    {
        var mine = _links.Where(l => CardLinkRules.Involves(l, cardId)).Select(l => CardLinkRules.Describe(l, cardId)).ToList();
        var briefs = _db.GetTaskBriefs(mine.Select(m => m.OtherId));
        return mine
            .OrderBy(m => CardLinkRules.Types.ToList().IndexOf(m.Type)).ThenBy(m => briefs.TryGetValue(m.OtherId, out var b) ? b.Title : "", StringComparer.CurrentCultureIgnoreCase)
            .Select(m => briefs.TryGetValue(m.OtherId, out var b)
                ? new RelatedTask(m.OtherId, m.Type, b.Title, b.Status, b.OnBoard)
                : new RelatedTask(m.OtherId, m.Type, "(task no longer exists)", "Gone", false))
            .ToList();
    }

    public TaskBrief? BriefOf(int id) => _db.GetTaskBriefs([id]).GetValueOrDefault(id);

    // The tasks the picker offers: everything on the board except the task itself.
    public IReadOnlyList<LinkCandidate> LinkCandidates(int? exceptId) =>
        Columns.SelectMany(c => c.Cards.Select(card => new LinkCandidate(card.Id, card.Title, card.ProjectName, c.DisplayName)))
            .Where(c => c.Id != exceptId)
            .OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    // Replaces the task's links with `wanted`, leaving out any the rules refuse (a circle, a second parent, a
    // repeat). Returns how many were left out; the task screen checks as each is added, so this is the last word.
    public int SetCardLinks(CardViewModel card, IReadOnlyList<CardLink> wanted)
    {
        var others = _links.Where(l => !CardLinkRules.Involves(l, card.Id)).ToList();
        var accepted = new List<CardLink>();
        foreach (var link in wanted)
        {
            if (!CardLinkRules.Involves(link, card.Id) || CardLinkRules.Validate([.. others, .. accepted], link) is not null) continue;
            accepted.Add(link);
        }

        var before = _links.Where(l => CardLinkRules.Involves(l, card.Id)).OrderBy(l => l.FromId).ThenBy(l => l.ToId).ToList();
        if (!before.SequenceEqual(accepted.OrderBy(l => l.FromId).ThenBy(l => l.ToId)))
        {
            _db.SetCardLinks(card.Id, accepted);
            _links = [.. others, .. accepted];
            RefreshLinkInfo();
        }
        return wanted.Count - accepted.Count;
    }
}

// A task the picker can offer to link to.
public sealed record LinkCandidate(int Id, string Title, string ProjectName, string ColumnName)
{
    public string Detail => $"{ProjectName}  ·  {ColumnName}";
}
