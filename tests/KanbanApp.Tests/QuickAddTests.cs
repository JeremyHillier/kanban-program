using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Quick Add: the one-line codes, and the task it makes. "Today" in the parser tests is Wed 7 Oct 2026.
[Collection(WpfCollection.Name)]
public sealed class QuickAddTests(WpfDispatcherFixture wpf) : IDisposable
{
    private static readonly DateTime Today = new(2026, 10, 7);
    private static readonly string[] People = ["Sam Lee", "Sara Jones", "Priya Patel"];
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static QuickAddResult Parse(string text) => QuickAddParser.Parse(text, People, Today);

    [Fact]
    public void PlainText_IsJustTheTitle()
    {
        var result = Parse("  Call the   bank about the mortgage ");

        Assert.Equal(new QuickAddResult("Call the bank about the mortgage", null, null, null), result);
    }

    [Theory]
    [InlineData("!high", "High")]
    [InlineData("!H", "High")]
    [InlineData("!medium", "Medium")]
    [InlineData("!m", "Medium")]
    [InlineData("!low", "Low")]
    [InlineData("!n", "Normal")]
    public void PriorityCodes(string code, string expected)
    {
        var result = Parse($"Pay invoice {code}");

        Assert.Equal("Pay invoice", result.Title);
        Assert.Equal(expected, result.Priority);
    }

    [Theory]
    [InlineData("/today", 2026, 10, 7)]
    [InlineData("/tomorrow", 2026, 10, 8)]
    [InlineData("/fri", 2026, 10, 9)]
    [InlineData("/Friday", 2026, 10, 9)]
    [InlineData("/wed", 2026, 10, 14)]      // today is Wednesday: the next one
    [InlineData("/mon", 2026, 10, 12)]
    [InlineData("/+3", 2026, 10, 10)]
    [InlineData("/10-15", 2026, 10, 15)]
    [InlineData("/3-1", 2027, 3, 1)]        // already gone by this year
    [InlineData("/2026-12-25", 2026, 12, 25)]
    public void DateCodes(string code, int year, int month, int day)
    {
        var result = Parse($"{code} Book flights");

        Assert.Equal("Book flights", result.Title);
        Assert.Equal(new DateTime(year, month, day), result.DueDate);
    }

    [Fact]
    public void Who_MatchesTheStartOfOnePersonsName_OrTheFullNameRunTogether()
    {
        Assert.Equal("Priya Patel", Parse("Review deck @pri").WhoName);
        Assert.Equal("Sam Lee", Parse("Review deck @sam").WhoName);
        Assert.Equal("Sara Jones", Parse("Review deck @SaraJones").WhoName);
    }

    [Fact]
    public void AnAmbiguousOrUnknownName_StaysInTheTitle()
    {
        var ambiguous = Parse("Review deck @sa");
        Assert.Null(ambiguous.WhoName);
        Assert.Equal("Review deck @sa", ambiguous.Title);

        Assert.Equal("Email @nobody", Parse("Email @nobody").Title);
    }

    [Theory]
    [InlineData("Decide and/or escalate")]
    [InlineData("Split it 50/50")]
    [InlineData("Email jane@example.com")]
    [InlineData("Check the /etc folder")]
    [InlineData("Wow !important")]
    [InlineData("Fix /13-40 typo")]
    public void ThingsThatOnlyLookLikeCodes_AreLeftAlone(string text)
    {
        Assert.Equal(new QuickAddResult(text, null, null, null), Parse(text));
    }

    [Fact]
    public void AllTheCodesTogether_AnywhereInTheLine()
    {
        var result = Parse("!high Send the contract @priya to legal /fri");

        Assert.Equal(new QuickAddResult("Send the contract to legal", "High", "Priya Patel", new DateTime(2026, 10, 9)), result);
    }

    [Fact]
    public void QuickAdd_MakesATaskInToDo_AndRemembersTheProject() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        board.AddProject("Website");
        board.AddPerson("Sam Lee");
        var website = board.Projects.Single(p => p.Name == "Website");

        var card = board.QuickAdd("Fix the contact form @sam !high /tomorrow", website)!;

        Assert.Equal("Fix the contact form", card.Title);
        Assert.Equal("Website", card.ProjectName);
        Assert.Equal("High", card.Priority);
        Assert.Equal("Sam Lee", card.WhoName);
        Assert.Equal(DateTime.Today.AddDays(1), card.DueDate);
        Assert.Contains(card, board.Columns.Single(c => c.Name == "To Do").Cards);

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal("Website", reopened.QuickAddDefaultProject?.Name);
        Assert.Single(reopened.Columns.Single(c => c.Name == "To Do").Cards);
    });

    [Fact]
    public void QuickAdd_WithNothingButCodes_MakesNothing_AndARealOneCanBeUndone() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));

        Assert.Null(board.QuickAdd("  !high /tomorrow ", null));
        Assert.Empty(board.Columns.SelectMany(c => c.Cards));

        var card = board.QuickAdd("Buy stamps", null)!;
        Assert.Equal("Normal", card.Priority);
        Assert.NotNull(card.ProjectId); // a project is always chosen

        Assert.Equal("Add \"Buy stamps\"", board.Undo());
        Assert.Empty(board.Columns.SelectMany(c => c.Cards));
    });

    [Fact]
    public void TheHotkeySetting_IsRemembered() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.True(board.QuickAddHotkeyEnabled);

        board.SetQuickAddHotkeyEnabled(false);

        Assert.False(new MainViewModel(new DatabaseService(_temp.File("board.db"))).QuickAddHotkeyEnabled);
    });
}
