using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Which tasks the Timeline's filter lets through, and how it is saved.
public sealed class TimelineFilterTests
{
    private static PersonViewModel Person(int id, string name) => new(new Person { Id = id, Name = name });

    private static FlagViewModel Flag(int id, string name) => new(new Flag { Id = id, Name = name });

    private static CardViewModel Card(string priority, PersonViewModel[]? people = null, FlagViewModel[]? flags = null)
    {
        var card = new CardViewModel(new CardItem { Title = "T", Priority = priority });
        if (people is not null) card.People = people;
        if (flags is not null) card.Flags = [.. flags];
        return card;
    }

    private static TimelineFilter Only(int[]? people = null, string[]? priorities = null, int[]? flags = null) =>
        new(new HashSet<int>(people ?? []), new HashSet<string>(priorities ?? []), new HashSet<int>(flags ?? []));

    private static readonly PersonViewModel Sam = Person(1, "Sam");
    private static readonly PersonViewModel Pat = Person(2, "Pat");
    private static readonly FlagViewModel Urgent = Flag(7, "Urgent");

    [Fact]
    public void AnEmptyFilter_ShowsEverything()
    {
        Assert.True(TimelineFilter.None.IsEmpty);
        Assert.True(TimelineFilter.None.Matches(Card("Normal")));
        Assert.True(TimelineFilter.None.Matches(Card("High", [Sam], [Urgent])));
    }

    [Fact]
    public void WithinOneList_AnyTickedEntryMatches()
    {
        var filter = Only(people: [1, 2]);

        Assert.True(filter.Matches(Card("Normal", [Pat])));
        Assert.True(filter.Matches(Card("Normal", [Sam, Person(3, "Kim")])));   // one of several people is enough
        Assert.False(filter.Matches(Card("Normal", [Person(3, "Kim")])));
        Assert.False(filter.Matches(Card("Normal")));                           // nobody assigned
    }

    [Fact]
    public void AcrossLists_ATaskMustMatchEachListThatHasSomethingTicked()
    {
        var filter = Only(people: [1], priorities: ["High"], flags: [7]);

        Assert.True(filter.Matches(Card("High", [Sam], [Urgent])));
        Assert.False(filter.Matches(Card("Normal", [Sam], [Urgent])));
        Assert.False(filter.Matches(Card("High", [Pat], [Urgent])));
        Assert.False(filter.Matches(Card("High", [Sam])));
        Assert.Equal(3, filter.Count);
    }

    [Fact]
    public void TheFilter_IsSavedAndReadBack_AndAnythingUnreadableIsNoFilter()
    {
        var filter = Only(people: [2, 1], priorities: ["High", "Low"], flags: [7]);
        var back = TimelineFilter.FromJson(filter.ToJson());

        Assert.Equal([1, 2], back.PersonIds.Order());
        Assert.Equal(["High", "Low"], back.Priorities.Order());
        Assert.Equal([7], back.FlagIds);

        Assert.True(TimelineFilter.FromJson(null).IsEmpty);
        Assert.True(TimelineFilter.FromJson("not json").IsEmpty);
    }

    [Fact]
    public void ItIsDescribedByName_LeavingOutAnEntryDeletedSince()
    {
        var text = Only(people: [1, 99], priorities: ["High"], flags: [7]).Describe([Sam, Pat], [Urgent]);

        Assert.Equal("Who: Sam; Priority: High; Flags: Urgent", text);
    }
}
