namespace KanbanApp.Models;

// How two tasks are linked, as stored: one row per pair, read in one direction. Related has no
// direction (stored smaller id first). DependsOn reads "From depends on To"; ParentOf reads "From
// is the parent of To". The other two relations a user can pick are the same rows seen from the
// other end: "Blocks" is DependsOn the other way round, "Child of" is ParentOf the other way round.
public enum LinkKind { Related, DependsOn, ParentOf }

public sealed record CardLink(int FromId, int ToId, LinkKind Kind);

// A task's link counts: all its links, the open tasks it waits on, and the open tasks waiting on it.
public readonly record struct LinkCounts(int Related, int WaitingOn, int Blocking);

// A relation as one task's task screen shows and picks it: this task ... the other.
public enum RelationType { RelatedTo, DependsOn, Blocks, ChildOf, ParentOf }

// A linked task as the task screen and the card menu list it, from one task's point of view.
public sealed record RelatedTask(int OtherId, RelationType Type, string Title, string Status, bool OnBoard);

// What is shown about a task that is not necessarily on the board any more.
public sealed record TaskBrief(int Id, string Title, string ColumnName, bool IsArchived, bool IsDeleted)
{
    public bool OnBoard => !IsArchived && !IsDeleted;
    public bool IsDone => OnBoard && ColumnName == "Done";
    public string Status => IsDeleted ? "Deleted" : IsArchived ? "Archived" : ColumnName;
}

// The rules for links, free of the database so they can be checked on their own: how a picked
// relation becomes a stored row and back, and which new links are refused.
public static class CardLinkRules
{
    public static readonly IReadOnlyList<RelationType> Types = [RelationType.RelatedTo, RelationType.DependsOn, RelationType.Blocks, RelationType.ChildOf, RelationType.ParentOf];

    public static string Label(RelationType type) => type switch
    {
        RelationType.RelatedTo => "Related to",
        RelationType.DependsOn => "Depends on",
        RelationType.Blocks => "Blocks",
        RelationType.ChildOf => "Child of",
        RelationType.ParentOf => "Parent of",
        _ => type.ToString()
    };

    // The sentence the picker offers, "This task ..." followed by the other task.
    public static string Phrase(RelationType type) => type switch
    {
        RelationType.RelatedTo => "is related to",
        RelationType.DependsOn => "depends on",
        RelationType.Blocks => "blocks",
        RelationType.ChildOf => "is a child of",
        RelationType.ParentOf => "is the parent of",
        _ => type.ToString()
    };

    // The row stored for "this task <type> the other task".
    public static CardLink FromPerspective(int cardId, RelationType type, int otherId) => type switch
    {
        RelationType.RelatedTo => new CardLink(Math.Min(cardId, otherId), Math.Max(cardId, otherId), LinkKind.Related),
        RelationType.DependsOn => new CardLink(cardId, otherId, LinkKind.DependsOn),
        RelationType.Blocks => new CardLink(otherId, cardId, LinkKind.DependsOn),
        RelationType.ParentOf => new CardLink(cardId, otherId, LinkKind.ParentOf),
        RelationType.ChildOf => new CardLink(otherId, cardId, LinkKind.ParentOf),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    // A stored row, as the task `cardId` (one end of it) sees it.
    public static (RelationType Type, int OtherId) Describe(CardLink link, int cardId)
    {
        var isFrom = link.FromId == cardId;
        return link.Kind switch
        {
            LinkKind.Related => (RelationType.RelatedTo, isFrom ? link.ToId : link.FromId),
            LinkKind.DependsOn => isFrom ? (RelationType.DependsOn, link.ToId) : (RelationType.Blocks, link.FromId),
            _ => isFrom ? (RelationType.ParentOf, link.ToId) : (RelationType.ChildOf, link.FromId)
        };
    }

    public static bool Involves(CardLink link, int cardId) => link.FromId == cardId || link.ToId == cardId;

    // For every task with links: how many it has, how many of the tasks it depends on are still open (on the board and
    // not done), and how many open tasks depend on it. One pass over the links, each counting for both its tasks - a
    // pass over every link for each task made each change slow on a large board with many links.
    public static Dictionary<int, LinkCounts> CountsByTask(IEnumerable<CardLink> links, IReadOnlySet<int> onBoard, IReadOnlySet<int> done)
    {
        var counts = new Dictionary<int, LinkCounts>();
        void Add(int id, int related, int waitingOn, int blocking)
        {
            var c = counts.GetValueOrDefault(id);
            counts[id] = new LinkCounts(c.Related + related, c.WaitingOn + waitingOn, c.Blocking + blocking);
        }
        bool IsOpen(int id) => onBoard.Contains(id) && !done.Contains(id);

        foreach (var link in links)
        {
            Add(link.FromId, 1, 0, 0);
            if (link.ToId != link.FromId) Add(link.ToId, 1, 0, 0); // a link to itself is still one link
            if (link.Kind != LinkKind.DependsOn) continue;
            if (IsOpen(link.ToId)) Add(link.FromId, 0, 1, 0);
            if (IsOpen(link.FromId)) Add(link.ToId, 0, 0, 1);
        }
        return counts;
    }

    // Null when the link may be added to the existing ones; otherwise why not, in words for the user.
    public static string? Validate(IReadOnlyCollection<CardLink> existing, CardLink link)
    {
        if (link.FromId == link.ToId) return "A task cannot be linked to itself.";

        if (existing.Any(l => (l.FromId == link.FromId && l.ToId == link.ToId) || (l.FromId == link.ToId && l.ToId == link.FromId)))
            return "These two tasks are already linked. Remove the link first to change how they are related.";

        if (link.Kind == LinkKind.ParentOf && existing.Any(l => l.Kind == LinkKind.ParentOf && l.ToId == link.ToId))
            return "That task already has a parent. A task can have only one.";

        if (link.Kind != LinkKind.Related && Reaches(existing.Where(l => l.Kind == link.Kind), link.ToId, link.FromId))
            return link.Kind == LinkKind.DependsOn
                ? "That would make the tasks wait on each other in a circle."
                : "That would make a task its own ancestor.";

        return null;
    }

    // Whether following the links From -> To leads from `start` to `target`.
    private static bool Reaches(IEnumerable<CardLink> links, int start, int target)
    {
        var next = links.ToLookup(l => l.FromId, l => l.ToId);
        var seen = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (id == target) return true;
            if (!seen.Add(id)) continue;
            foreach (var to in next[id]) stack.Push(to);
        }
        return false;
    }
}
