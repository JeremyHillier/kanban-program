using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A card's background colour (right-click, Colour): one of a fixed set, kept in the task file, undone like any
// other change, and never written by the task window's own save.
public sealed class CardColorsTests
{
    [Fact]
    public void EveryColour_HasALightAndADarkShade_AndIsFoundWhateverTheCapitals()
    {
        Assert.True(CardColors.All.Count >= 6);
        Assert.All(CardColors.All, c => Assert.NotEqual(c.Light, c.Dark));
        Assert.Equal("Yellow", CardColors.Get("yellow")!.Key);
        Assert.Null(CardColors.Get("Chartreuse"));
        Assert.Null(CardColors.Get(null));
        Assert.Equal("CardColor.Blue", CardColors.ResourceKey("Blue"));
    }
}

[Collection(WpfCollection.Name)]
public sealed class CardColorBoardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private (MainViewModel Board, CardViewModel Card) BoardWithACard(DatabaseService db)
    {
        var board = new MainViewModel(db);
        var card = board.AddCard("Paint the fence", board.Columns.Single(c => c.Name == "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);
        return (board, card);
    }

    [Fact]
    public void AColour_IsKeptInTheTaskFile_AndStampsItAsNewer() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var (board, card) = BoardWithACard(db);
        Assert.False(card.HasColor);

        board.SetCardColor(card, "yellow");

        Assert.Equal("Yellow", card.Color); // as the list spells it
        Assert.True(card.HasColor);
        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal("Yellow", reopened.Columns.SelectMany(c => c.Cards).Single(c => c.Title == "Paint the fence").Color);
        Assert.Equal(DatabaseService.CardColorFileFormat, new DatabaseService(_temp.File("board.db")).FileFormatAtOpen);
    });

    [Fact]
    public void AnUnknownColour_MeansNone_AndSavingTheTaskKeepsTheColour() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var (board, card) = BoardWithACard(db);
        board.SetCardColor(card, "Blue");

        board.SetCardPriority(card, "High"); // goes through the ordinary save, which must leave the colour alone
        Assert.Equal("Blue", new MainViewModel(new DatabaseService(_temp.File("board.db"))).Columns.SelectMany(c => c.Cards).Single().Color);

        board.SetCardColor(card, "Chartreuse");
        Assert.Null(card.Color);
    });

    [Fact]
    public void Undo_PutsTheOldColourBack() => wpf.Run(() =>
    {
        var (board, card) = BoardWithACard(new DatabaseService(_temp.File("board.db")));
        board.SetCardColor(card, "Green");
        board.SetCardColor(card, "Pink");

        board.Undo();
        Assert.Equal("Green", card.Color);
        board.Undo();
        Assert.Null(card.Color);
    });

    [Fact]
    public void ColourAll_ChangesEveryPickedCard_AndNoneTakesItOff() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var (board, first) = BoardWithACard(db);
        var second = board.AddCard("Mow the lawn", board.Columns.Single(c => c.Name == "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);

        board.SetCardsColor([first, second], "Teal");
        Assert.Equal(["Teal", "Teal"], new[] { first.Color, second.Color });

        board.SetCardsColor([first, second], null);
        Assert.All(new[] { first, second }, c => Assert.Null(c.Color));
    });
}
