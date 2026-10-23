using System.IO;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The user's own spell-check words: kept in the task file, written to a file WPF's spell checker reads,
// and added from a flagged word's right-click menu.
public class UserDictionaryWordTests
{
    [Fact]
    public void TheWords_AreTidied_Deduplicated_AndSorted()
    {
        var words = UserDictionary.ParseWords("  Kanban,  hillier\r\nZorbulax;kanban\n\n  Zorbulax  \t Nts\n");

        Assert.Equal(["hillier", "Kanban", "Nts", "Zorbulax"], words);          // first spelling of a repeat is kept
    }

    [Theory]
    [InlineData("O'Brien", "O'Brien")]
    [InlineData("co-op", "co-op")]
    [InlineData("-leading", "leading")]
    [InlineData("trailing'", "trailing")]
    [InlineData("Zörbulax", "Zörbulax")]
    [InlineData("A1B2", "A1B2")]
    public void WhatMayBeAWord(string typed, string expected) => Assert.Equal([expected], UserDictionary.ParseWords(typed));

    [Theory]
    [InlineData("#LID 1033")]               // a line that means something to the dictionary file
    [InlineData("x@y.com")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData("   ")]
    public void WhatMayNot(string typed) => Assert.DoesNotContain(UserDictionary.ParseWords(typed), w => w.Contains('#') || w.Contains('@') || w.Contains('/'));

    [Fact]
    public void ALongString_IsNotAWord() => Assert.Empty(UserDictionary.ParseWords(new string('a', 61)));
}

[Collection(WpfCollection.Name)]
public sealed class UserDictionaryTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Folder => Path.Combine(_temp.File("anything.db"), "..", "dictionary");

    [Fact]
    public void SettingTheWords_WritesTheFileWpfReads() => wpf.Run(() =>
    {
        var dictionary = new UserDictionary(Folder);

        dictionary.Set("zorbulax\nKanbanish");

        Assert.Equal(["Kanbanish", "zorbulax"], dictionary.Words);
        Assert.Equal("Kanbanish\nzorbulax", dictionary.AsText);
        // Each word as typed and in lower case: the lower-case entry covers ordinary capitals, the typed one a word
        // with a capital inside it ("PayWorks"), which a lower-case entry does not match.
        Assert.Equal(["Kanbanish", "kanbanish", "zorbulax"], File.ReadAllLines(dictionary.FilePath));
        Assert.Equal(["PayWorks", "payworks", "kefron"], UserDictionary.FileLines(["PayWorks", "kefron"]));
        Assert.False(File.ReadAllText(dictionary.FilePath).StartsWith('﻿'));       // no byte-order mark
    });

    [Fact]
    public void AddingAWord_KeepsTheRest_RefusesADuplicate_AndTellsTheOwner() => wpf.Run(() =>
    {
        var dictionary = new UserDictionary(Folder);
        dictionary.Set("alpha");
        var told = 0;
        dictionary.WordsAdded += () => told++;

        Assert.True(dictionary.Add("Beta"));
        Assert.False(dictionary.Add("beta"));                                   // already there, whatever its capitals
        Assert.False(dictionary.Add("two words"));                              // not one word
        Assert.False(dictionary.Add("#"));

        Assert.Equal(["alpha", "Beta"], dictionary.Words);
        Assert.Equal(1, told);
        Assert.True(dictionary.Contains("BETA"));
    });

    [Fact]
    public void ABox_IsGivenTheWords_AndTheDictionariesItHadOfItsOwn() => wpf.Run(() =>
    {
        var dictionary = new UserDictionary(Folder);
        File.WriteAllText(_temp.File("american.lex"), "color\r\n");
        var american = new Uri(_temp.File("american.lex"));
        var box = new TextBox();
        SpellCheck_Enable(box);

        dictionary.Attach(box, american);                                       // no words yet: only its own
        Assert.Equal([american], System.Windows.Controls.SpellCheck.GetCustomDictionaries(box).Cast<Uri>());

        dictionary.Set("zorbulax");                                             // a word added later reaches the box
        Assert.Equal([american, new Uri(dictionary.FilePath)], System.Windows.Controls.SpellCheck.GetCustomDictionaries(box).Cast<Uri>());

        dictionary.Set("");                                                     // all taken out again
        Assert.Equal([american], System.Windows.Controls.SpellCheck.GetCustomDictionaries(box).Cast<Uri>());
    });

    private static void SpellCheck_Enable(TextBox box) => System.Windows.Controls.SpellCheck.SetIsEnabled(box, true);

    [Fact]
    public void TheTaskFile_KeepsTheWords_AndAddingFromTheMenuIsSaved() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var board = new MainViewModel(db);
        board.SetCustomDictionary("alpha\nbeta");

        board.Dictionary.Add("Gamma");                                          // as the right-click menu does

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal("alpha\nbeta\nGamma", reopened.CustomDictionaryText);
        Assert.Equal(["alpha", "beta", "Gamma"], reopened.Dictionary.Words);
    });
}
