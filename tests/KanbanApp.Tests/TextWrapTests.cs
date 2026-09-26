using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The word-wrapping shared by the report preview, the report PDF and the printed Timeline. Measured here
// one unit per character, so the widths are easy to read.
public sealed class TextWrapTests
{
    private static double Chars(string s) => s.Length;

    [Fact]
    public void Words_BreakAtTheLastSpaceThatFits()
    {
        Assert.Equal(["one two", "three four", "five"], TextWrap.Words("one two three four five", Chars, 10));
    }

    [Fact]
    public void Words_AWordTooLongForTheLineKeepsALineToItself()
    {
        Assert.Equal(["a", "extraordinarily", "b"], TextWrap.Words("a extraordinarily b", Chars, 5));
    }

    [Fact]
    public void Words_EmptyTextIsOneEmptyLine()
    {
        Assert.Equal([""], TextWrap.Words("", Chars, 10));
    }

    [Fact]
    public void Lines_EachLineBreakStartsANewLine_AndABlankLineIsKept()
    {
        Assert.Equal(["first line", "wraps here", "second", "", "fourth"],
            TextWrap.Lines("first line wraps here\r\nsecond\n\nfourth", Chars, 10));
    }

    [Fact]
    public void Priorities_HighestFirst_AndAnUnknownOneSortsWithNormal()
    {
        Assert.Equal([0, 1, 2, 3], Priorities.All.Select(Priorities.Rank));
        Assert.Equal(Priorities.Rank("Normal"), Priorities.Rank("Urgent"));
    }
}
