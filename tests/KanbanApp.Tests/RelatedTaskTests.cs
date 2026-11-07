using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The rules for links between tasks, on their own: how a picked relation is stored and read back, and
// which new links are refused.
public sealed class CardLinkRulesTests
{
    [Theory]
    [InlineData(RelationType.RelatedTo)]
    [InlineData(RelationType.DependsOn)]
    [InlineData(RelationType.Blocks)]
    [InlineData(RelationType.ChildOf)]
    [InlineData(RelationType.ParentOf)]
    public void APickedRelation_IsReadBackTheSameWay_ByTheTaskThatPickedIt_AndInverseByTheOther(RelationType type)
    {
        var link = CardLinkRules.FromPerspective(7, type, 3);

        Assert.Equal((type, 3), CardLinkRules.Describe(link, 7));

        var inverse = type switch
        {
            RelationType.DependsOn => RelationType.Blocks,
            RelationType.Blocks => RelationType.DependsOn,
            RelationType.ChildOf => RelationType.ParentOf,
            RelationType.ParentOf => RelationType.ChildOf,
            _ => RelationType.RelatedTo
        };
        Assert.Equal((inverse, 7), CardLinkRules.Describe(link, 3));
    }

    [Fact]
    public void Related_IsStoredOnce_WhicheverTaskPicksIt()
    {
        Assert.Equal(CardLinkRules.FromPerspective(7, RelationType.RelatedTo, 3), CardLinkRules.FromPerspective(3, RelationType.RelatedTo, 7));
        Assert.Equal(new CardLink(3, 7, LinkKind.Related), CardLinkRules.FromPerspective(7, RelationType.RelatedTo, 3));
    }

    [Fact]
    public void ATask_CannotBeLinkedToItself_OrToATaskItIsAlreadyLinkedTo()
    {
        Assert.NotNull(CardLinkRules.Validate([], new CardLink(4, 4, LinkKind.Related)));

        List<CardLink> existing = [CardLinkRules.FromPerspective(1, RelationType.DependsOn, 2)];
        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(1, RelationType.RelatedTo, 2)));
        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(2, RelationType.RelatedTo, 1)));
        Assert.Null(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(1, RelationType.RelatedTo, 3)));
    }

    [Fact]
    public void Dependencies_CannotMakeACircle_AtAnyLength()
    {
        List<CardLink> existing = [CardLinkRules.FromPerspective(1, RelationType.DependsOn, 2), CardLinkRules.FromPerspective(2, RelationType.DependsOn, 3)];

        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(3, RelationType.DependsOn, 1)));
        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(1, RelationType.Blocks, 3))); // 3 would depend on 1
        Assert.Null(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(1, RelationType.DependsOn, 3)));
        Assert.Null(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(4, RelationType.DependsOn, 1)));
    }

    [Fact]
    public void AParent_CannotBeItsOwnDescendant_AndAChildHasOnlyOneParent()
    {
        List<CardLink> existing = [CardLinkRules.FromPerspective(1, RelationType.ParentOf, 2), CardLinkRules.FromPerspective(2, RelationType.ParentOf, 3)];

        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(3, RelationType.ParentOf, 1)));
        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(3, RelationType.ChildOf, 1))); // 3 already has parent 2
        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(9, RelationType.ParentOf, 3))); // 3 already has parent 2
        Assert.Null(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(9, RelationType.ParentOf, 4)));
    }

    [Fact]
    public void DependencyAndParentLinks_AreCheckedSeparately()
    {
        List<CardLink> existing = [CardLinkRules.FromPerspective(1, RelationType.ParentOf, 2), CardLinkRules.FromPerspective(2, RelationType.DependsOn, 3)];

        Assert.Null(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(3, RelationType.ParentOf, 1))); // not a parent circle: 1 -> 2 only
        Assert.NotNull(CardLinkRules.Validate(existing, CardLinkRules.FromPerspective(3, RelationType.DependsOn, 2)));
    }
}

// Links on a real board: saved, read back from the other end, shown on the cards, and removed with the task.
[Collection(WpfCollection.Name)]
public sealed class RelatedTaskBoardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private (MainViewModel Board, CardViewModel A, CardViewModel B, CardViewModel C) Board()
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var todo = board.Columns.Single(c => c.Name == "To Do");
        CardViewModel Add(string title) => board.AddCard(title, todo, board.Projects.First(), "Normal", null, null, false, null, null);
        return (board, Add("Write report"), Add("Gather figures"), Add("Send it"));
    }

    [Fact]
    public void ALink_IsKeptInTheFile_SeenFromBothEnds_AndStampsItAsNewer() => wpf.Run(() =>
    {
        var (board, a, b, _) = Board();

        Assert.Equal(0, board.SetCardLinks(a, [CardLinkRules.FromPerspective(a.Id, RelationType.DependsOn, b.Id)]));

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var fromA = Assert.Single(reopened.RelatedTasksOf(a.Id));
        Assert.Equal((RelationType.DependsOn, "Gather figures"), (fromA.Type, fromA.Title));
        var fromB = Assert.Single(reopened.RelatedTasksOf(b.Id));
        Assert.Equal((RelationType.Blocks, "Write report"), (fromB.Type, fromB.Title));
        Assert.Equal(DatabaseService.RelationshipFileFormat, new DatabaseService(_temp.File("board.db")).FileFormatAtOpen);
    });

    [Fact]
    public void ACardWaitingOnAnOpenTask_IsBlocked_UntilThatTaskIsDone() => wpf.Run(() =>
    {
        var (board, a, b, _) = Board();
        board.SetCardLinks(a, [CardLinkRules.FromPerspective(a.Id, RelationType.DependsOn, b.Id)]);

        Assert.True(a.IsBlocked);
        Assert.Equal(1, b.BlockingCount);
        Assert.Contains("Blocked", a.RelatedDisplay);

        board.MoveCardCommand.Execute((b, board.DoneColumn!));

        Assert.False(a.IsBlocked);
        Assert.True(a.HasRelated);
    });

    [Fact]
    public void ARefusedLink_IsLeftOut_AndNothingElseChanges() => wpf.Run(() =>
    {
        var (board, a, b, c) = Board();
        board.SetCardLinks(a, [CardLinkRules.FromPerspective(a.Id, RelationType.DependsOn, b.Id)]);
        board.SetCardLinks(b, [CardLinkRules.FromPerspective(b.Id, RelationType.DependsOn, c.Id), CardLinkRules.FromPerspective(b.Id, RelationType.Blocks, a.Id)]); // keeps a -> b

        // c depends on a would close the circle a -> b -> c -> a.
        var refused = board.SetCardLinks(c, [CardLinkRules.FromPerspective(c.Id, RelationType.Blocks, b.Id), CardLinkRules.FromPerspective(c.Id, RelationType.DependsOn, a.Id)]); // keeps b -> c

        Assert.Equal(1, refused);
        Assert.Empty(board.RelatedTasksOf(c.Id).Where(r => r.Type == RelationType.DependsOn));
    });

    [Fact]
    public void Links_GoWithATaskDeletedForGood_ButNotOneOnlyArchived() => wpf.Run(() =>
    {
        var (board, a, b, _) = Board();
        board.SetCardLinks(a, [CardLinkRules.FromPerspective(a.Id, RelationType.RelatedTo, b.Id)]);

        var db = new DatabaseService(_temp.File("board.db"));
        db.PermanentlyDeleteCard(b.Id, b.Title, "To Do");

        Assert.Empty(db.GetCardLinks());
    });

    [Fact]
    public void ASavedTaskScreen_ReplacesTheTasksLinks_OnlyTheirOwnEnds() => wpf.Run(() =>
    {
        var (board, a, b, c) = Board();
        board.SetCardLinks(a, [CardLinkRules.FromPerspective(a.Id, RelationType.RelatedTo, b.Id), CardLinkRules.FromPerspective(a.Id, RelationType.ParentOf, c.Id)]);

        board.SetCardLinks(a, [CardLinkRules.FromPerspective(a.Id, RelationType.RelatedTo, b.Id)]);

        Assert.Single(board.RelatedTasksOf(a.Id));
        Assert.Empty(board.RelatedTasksOf(c.Id));
    });
}
