using KanbanApp.Models;

namespace KanbanApp.Tests;

// CardLinkRules.CountsByTask (one pass over the links) gives every task exactly what the per-task count it replaced
// gave: links of every kind, links to itself, to tasks no longer on the board, and to finished tasks.
public sealed class LinkCountsTests
{
    // The way the counts were worked out before: each task looking through every link.
    private static (int Related, int WaitingOn, int Blocking) PerTask(IReadOnlyList<CardLink> links, int id, ISet<int> onBoard, ISet<int> done)
    {
        var mine = links.Where(l => CardLinkRules.Involves(l, id)).ToList();
        var waitingOn = mine.Count(l => l.Kind == LinkKind.DependsOn && l.FromId == id && onBoard.Contains(l.ToId) && !done.Contains(l.ToId));
        var blocking = mine.Count(l => l.Kind == LinkKind.DependsOn && l.ToId == id && onBoard.Contains(l.FromId) && !done.Contains(l.FromId));
        return (mine.Count, waitingOn, blocking);
    }

    [Fact]
    public void OnePassOverTheLinks_CountsTheSameAsLookingFromEachTask()
    {
        var random = new Random(20261109);
        for (var round = 0; round < 300; round++)
        {
            var board = Enumerable.Range(1, random.Next(1, 40)).ToList();
            var onBoard = board.ToHashSet();
            var done = board.Where(_ => random.Next(4) == 0).ToHashSet();
            var ids = board.Concat([900, 901, 902]).ToList(); // 900+ are archived or deleted: not on the board
            var links = Enumerable.Range(0, random.Next(0, 60))
                .Select(_ => new CardLink(ids[random.Next(ids.Count)], ids[random.Next(ids.Count)], (LinkKind)random.Next(3)))
                .ToList();

            var counts = CardLinkRules.CountsByTask(links, onBoard, done);

            foreach (var id in board)
            {
                var c = counts.GetValueOrDefault(id);
                Assert.Equal(PerTask(links, id, onBoard, done), (c.Related, c.WaitingOn, c.Blocking));
            }
        }
    }

    [Fact]
    public void ALinkToItself_CountsOnce_AndATaskWithNoLinksHasNone()
    {
        var counts = CardLinkRules.CountsByTask([new CardLink(1, 1, LinkKind.DependsOn)], new HashSet<int> { 1, 2 }, new HashSet<int>());

        Assert.Equal(new LinkCounts(1, 1, 1), counts[1]);
        Assert.False(counts.ContainsKey(2));
    }
}
