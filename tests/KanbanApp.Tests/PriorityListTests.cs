using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The priority list on its own: order, the default, names and colours, and how it is stored.
public class PriorityListTests
{
    [Fact]
    public void ANewList_IsTheStandardFour_HighestFirst_WithNormalTheDefault()
    {
        var list = new PriorityList();

        Assert.Equal(["High", "Medium", "Normal", "Low"], list.Names);
        Assert.Equal("Normal", list.Default);
        Assert.True(list.IsStandard);
        Assert.Equal([0, 1, 2, 3], list.Names.Select(list.Rank));
        Assert.Equal(["Red", "Amber", "Grey", "Blue"], list.Names.Select(list.ColorKey));
    }

    [Fact]
    public void ANameThatIsNotOnTheList_SortsWithTheDefault_AndIsGrey()
    {
        var list = new PriorityList();

        Assert.Equal(list.Rank("Normal"), list.Rank("Someday"));
        Assert.Equal("Grey", list.ColorKey("Someday"));
        Assert.Null(list.Find("Someday"));
        Assert.Equal("Normal", list.Resolve("Someday"));
        Assert.Equal("Normal", list.Resolve(null));
    }

    [Fact]
    public void NamesAreFound_WhateverTheirCapitals()
    {
        var list = new PriorityList();

        Assert.Equal("High", list.Find(" hIGH "));
        Assert.Equal("High", list.Resolve("HIGH"));
        Assert.Equal(0, list.Rank("high"));
    }

    [Fact]
    public void Adding_PutsItAtTheBottom_InAColourNothingElseUses()
    {
        var list = new PriorityList();

        Assert.True(list.Add("  Urgent "));

        Assert.Equal(["High", "Medium", "Normal", "Low", "Urgent"], list.Names);
        Assert.Equal("Green", list.ColorKey("Urgent"));
        Assert.False(list.IsStandard);
    }

    [Fact]
    public void Adding_IsRefused_ForABlankName_OrOneAlreadyThere()
    {
        var list = new PriorityList();

        Assert.False(list.Add("   "));
        Assert.False(list.Add("medium"));
        Assert.Equal(4, list.Levels.Count);
    }

    [Fact]
    public void Renaming_KeepsItsPlaceAndColour_AndTheDefaultFollows()
    {
        var list = new PriorityList();

        Assert.True(list.Rename("Normal", "Routine"));

        Assert.Equal(["High", "Medium", "Routine", "Low"], list.Names);
        Assert.Equal("Routine", list.Default);
        Assert.Equal("Grey", list.ColorKey("Routine"));
    }

    [Fact]
    public void Renaming_IsRefused_OntoAnotherName_ButCapitalsAloneCanChange()
    {
        var list = new PriorityList();

        Assert.False(list.Rename("Low", "HIGH"));
        Assert.False(list.Rename("Low", "  "));
        Assert.False(list.Rename("Nope", "Something"));
        Assert.True(list.Rename("Low", "LOW"));
        Assert.Equal("LOW", list.Names[3]);
    }

    [Fact]
    public void TheDefault_AndTheLastOne_CannotBeRemoved()
    {
        var list = new PriorityList();

        Assert.False(list.Remove("Normal"));
        Assert.True(list.Remove("High"));
        Assert.True(list.Remove("Medium"));
        Assert.True(list.Remove("Low"));
        Assert.False(list.CanRemove("Normal"));
        Assert.Equal(["Normal"], list.Names);
    }

    [Fact]
    public void Moving_ChangesTheOrder_AndStopsAtTheEnds()
    {
        var list = new PriorityList();
        list.Add("Urgent");

        Assert.True(list.Move("Urgent", -10));
        Assert.Equal(["Urgent", "High", "Medium", "Normal", "Low"], list.Names);
        Assert.False(list.Move("Urgent", -1));

        Assert.True(list.Move("High", 1));
        Assert.Equal(["Urgent", "Medium", "High", "Normal", "Low"], list.Names);
        Assert.False(list.Move("Low", 1));
    }

    [Fact]
    public void Colour_AndDefault_CanBeChanged()
    {
        var list = new PriorityList();

        Assert.True(list.SetColor("Low", "purple"));
        Assert.Equal("Purple", list.ColorKey("Low"));
        Assert.False(list.SetColor("Low", "Purple"));          // already that colour
        Assert.True(list.SetColor("Low", "no such colour"));   // falls back to grey
        Assert.Equal("Grey", list.ColorKey("Low"));

        Assert.True(list.SetDefault("low"));
        Assert.Equal("Low", list.Default);
        Assert.False(list.SetDefault("Low"));
        Assert.False(list.SetDefault("Nope"));
    }

    [Fact]
    public void TheList_SurvivesBeingStoredAndReadBack()
    {
        var list = new PriorityList();
        list.Add("Urgent");
        list.Move("Urgent", -4);
        list.Rename("Medium", "Soon");
        list.SetColor("Low", "Teal");
        list.SetDefault("Low");

        var back = PriorityList.FromJson(list.ToJson());

        Assert.Equal(["Urgent", "High", "Soon", "Normal", "Low"], back.Names);
        Assert.Equal("Low", back.Default);
        Assert.Equal(list.Names.Select(list.ColorKey), back.Names.Select(back.ColorKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"Default\":\"X\",\"Levels\":[]}")]
    [InlineData("{\"Levels\":[{\"Name\":\"  \",\"Color\":\"Red\"}]}")]
    public void AMissingOrDamagedSetting_GivesTheStandardList(string? json)
    {
        Assert.True(PriorityList.FromJson(json).IsStandard);
    }

    [Fact]
    public void AStoredList_IsTidiedOnTheWayIn()
    {
        var list = PriorityList.FromJson(
            "{\"Default\":\"gone\",\"Levels\":[{\"Name\":\" Now \",\"Color\":\"Puce\"},{\"Name\":\"now\",\"Color\":\"Red\"},{\"Name\":\"Later\",\"Color\":\"blue\"}]}");

        Assert.Equal(["Now", "Later"], list.Names);      // trimmed, and the repeat dropped
        Assert.Equal("Grey", list.ColorKey("Now"));      // an unknown colour is grey
        Assert.Equal("Blue", list.ColorKey("Later"));
        Assert.Equal("Now", list.Default);               // a default that isn't there becomes the first
    }

    [Fact]
    public void EveryColour_HasABadgeAndBothChartColours_AndTheKeysAreDistinct()
    {
        Assert.Equal(PriorityColors.All.Count, PriorityColors.All.Select(c => c.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(PriorityColors.All.Count, PriorityColors.All.Select(c => c.Badge).Distinct().Count());
        Assert.Contains(PriorityColors.All, c => c.Key == PriorityColors.Fallback);
    }
}

// The letters on each card's move buttons come from what the columns are called.
public class ColumnLettersTests
{
    [Fact]
    public void TheStandardNames_GiveTheLettersTheyAlwaysHad()
    {
        Assert.Equal(["T", "P", "H", "W"], ColumnLetters.Assign(["To Do", "In Progress", "On Hold", "Waiting"]));
    }

    [Fact]
    public void TheGtdNames_GetTheirOwnLetters()
    {
        Assert.Equal(["I", "N", "P", "W"], ColumnLetters.Assign(["Inbox", "Next Actions", "In Progress", "Waiting"]));
    }

    [Fact]
    public void TwoColumnsNeverShareALetter()
    {
        // Doing takes D; Deferred falls to its next letter; "Do Later" to its second word; a lone
        // "D" has nothing left, so it takes its position on the board.
        Assert.Equal(["D", "E", "L", "4"], ColumnLetters.Assign(["Doing", "Deferred", "Do Later", "D"]));
    }

    [Fact]
    public void X_IsLeftForTheDeleteButton()
    {
        Assert.Equal(["R"], ColumnLetters.Assign(["X-ray"]));
    }

    [Theory]
    [InlineData("backlog", "B")]
    [InlineData("  the queue ", "Q")]
    [InlineData("1. Inbox", "1")]
    [InlineData("In", "I")]          // a small word is still used when it is all there is
    [InlineData("(Review)", "R")]
    public void TheLetter_IsTheFirstOfTheFirstRealWord_InCapitals(string name, string expected)
    {
        Assert.Equal([expected], ColumnLetters.Assign([name]));
    }

    [Fact]
    public void ANameWithNoLettersAtAll_TakesItsPosition()
    {
        Assert.Equal(["A", "2"], ColumnLetters.Assign(["Alpha", "***"]));
    }
}
