using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp.Tests;

public class AutoCorrectorTests
{
    private static string Type(string typed, string? own = null)
    {
        var entries = AutoCorrector.ParseEntries(own);
        return AutoCorrector.TryCorrect(typed, typed.Length, entries, out var start, out var length, out var replacement)
            ? typed[..start] + replacement + typed[(start + length)..]
            : typed;
    }

    [Theory]
    [InlineData("call teh", "call the")]
    [InlineData("Teh", "The")]
    [InlineData("TEH", "THE")]
    [InlineData("tomorow", "tomorrow")]
    [InlineData("see you wensday", "see you Wednesday")]
    [InlineData("i", "I")]
    [InlineData("dont", "don't")]
    [InlineData("(teh", "(the")]
    [InlineData("it\u2019s dont", "it\u2019s don't")]
    public void AKnownSlip_IsFixed_KeepingItsCapitals(string typed, string expected) =>
        Assert.Equal(expected, Type(typed));

    [Theory]
    [InlineData("the")]            // already right
    [InlineData("Paris")]          // not a slip
    [InlineData("www.teh")]        // part of an address
    [InlineData("a/teh")]
    [InlineData("sam@teh")]
    [InlineData("C:\\teh")]
    [InlineData("3teh")]           // part of a number or code
    [InlineData("under_teh")]
    [InlineData("")]
    public void ThingsThatAreNotWords_AreLeftAlone(string typed) => Assert.Equal(typed, Type(typed));

    [Fact]
    public void YourOwnEntries_AreAdded_AndWinOverTheBuiltInOnes()
    {
        Assert.Equal("Kanban Program", Type("kbp", "kbp = Kanban Program"));
        Assert.Equal("Kanban Program", Type("KBP", "KBP=Kanban Program"));
        Assert.Empty(AutoCorrector.ParseEntries("teh = teh"));            // a no-op line is dropped
        Assert.Equal("tea", Type("teh", "teh = tea"));
    }

    [Fact]
    public void BadLines_AreSkipped()
    {
        var entries = AutoCorrector.ParseEntries("no equals\n= nothing\nword =\ntwo words = x\nok = fine\r\n");
        Assert.Equal(["ok"], entries.Keys);
        Assert.Equal("fine", entries["ok"]);
    }

    [Fact]
    public void EveryBuiltInRightHandSide_IsNotItselfASlip()
    {
        foreach (var (wrong, right) in AutoCorrector.BuiltIn)
        {
            if (wrong.Equals(right, StringComparison.OrdinalIgnoreCase)) continue; // i -> I only changes capitals
            Assert.False(AutoCorrector.BuiltIn.ContainsKey(right), $"{wrong} -> {right} would loop");
            Assert.NotEqual(wrong, right, StringComparer.OrdinalIgnoreCase);
        }
    }
}

[Collection(WpfCollection.Name)]
public sealed class AutoCorrectBoxTests(WpfDispatcherFixture wpf)
{
    // Types one character at a time the way a keyboard does: a plain insertion at the caret.
    private static void Type(TextBox box, string text)
    {
        foreach (var c in text)
        {
            if (box.MaxLength > 0 && box.Text.Length >= box.MaxLength) continue;
            Insert(box, c.ToString());
        }
    }

    // Text arriving as it does from a touch keyboard, a picked suggestion, dictation or a paste.
    private static void Insert(TextBox box, string text)
    {
        var at = box.CaretIndex;
        var before = box.Text.Length;
        box.Select(at, 0);
        box.SelectedText = text;
        box.CaretIndex = at + (box.Text.Length - before); // after what went in, whatever a correction did to its length
    }

    private static TextBox Box(bool on = true, string own = "") => new TextBox().Also(b =>
        AutoCorrectBox.Attach(b, () => on, () => AutoCorrector.ParseEntries(own), requireFocus: false));

    [Fact]
    public void TypingASlipAndASpace_FixesIt() => wpf.Run(() =>
    {
        var box = Box();
        Type(box, "Call teh bank adn pay. ");
        Assert.Equal("Call the bank and pay. ", box.Text);
    });

    [Fact]
    public void NothingChanges_UntilTheWordIsFinished_OrWhenSwitchedOff() => wpf.Run(() =>
    {
        var box = Box();
        Type(box, "teh");
        Assert.Equal("teh", box.Text);

        var off = Box(on: false);
        Type(off, "teh ");
        Assert.Equal("teh ", off.Text);
    });

    [Fact]
    public void TheCorrection_FitsTheBoxLimit() => wpf.Run(() =>
    {
        var box = Box();
        box.MaxLength = 5;                  // "tomorow" is typed, but "tomorrow " would not fit
        box.Text = "";
        box.Text = "tomo";
        box.CaretIndex = 4;
        Type(box, "r");
        Assert.Equal("tomor", box.Text);
        Type(box, " ");
        Assert.Equal("tomor", box.Text);
    });

    [Fact]
    public void YourOwnEntry_IsUsed() => wpf.Run(() =>
    {
        var box = Box(own: "kbp = Kanban Program");
        Type(box, "Work on kbp, then ");
        Assert.Equal("Work on Kanban Program, then ", box.Text);
    });

    // The entry exactly as a user typed it in Settings, saved the way Settings saves it.
    [Fact]
    public void ANoteToSelfEntry_FromSettings_IsUsed() => wpf.Run(() =>
    {
        var board = new KanbanApp.ViewModels.MainViewModel(new KanbanApp.Services.DatabaseService(Path.Combine(Path.GetTempPath(), $"ac-{Guid.NewGuid():N}.db")));
        board.SetAutoCorrectEntries("nts = Note to Self");

        var box = new TextBox();
        AutoCorrectBox.Attach(box, () => board.AutoCorrectEnabled, () => board.AutoCorrectOwn, requireFocus: false);

        Type(box, "Call Sam nts ");
        Assert.Equal("Call Sam Note to Self ", box.Text);
    });


    [Fact]
    public void TextArrivingAWordAtATime_IsCorrectedToo() => wpf.Run(() =>
    {
        var box = Box(own: "nts = Note to Self");
        Type(box, "test ");
        Insert(box, "nts ");                     // the touch keyboard, a picked suggestion, dictation or a paste
        Assert.Equal("test Note to Self ", box.Text);
        Assert.Equal(box.Text.Length, box.CaretIndex);

        Insert(box, "and teh.");
        Assert.Equal("test Note to Self and the.", box.Text);
    });

    [Fact]
    public void LoadingATitle_IsNotTypingAWord_AndTypingOverASelectionIsNotEither() => wpf.Run(() =>
    {
        // The default rule: only a box the user is in (it has the keyboard) is corrected.
        var loaded = new TextBox();
        AutoCorrectBox.Attach(loaded, () => true, () => AutoCorrector.ParseEntries("nts = Note to Self"));
        loaded.Text = "call nts.";
        Assert.Equal("call nts.", loaded.Text);

        var box = Box(own: "nts = Note to Self");
        box.Text = "xx nts yy";
        box.Select(6, 2);
        box.SelectedText = " ";                  // replaces a selection: nothing was put in after a word
        Assert.Equal("xx nts y", box.Text);
    });

    [Fact]
    public void TheCursorStaysWhereTheUserWas_AfterACorrectionInTheMiddle() => wpf.Run(() =>
    {
        var box = Box(own: "nts = Note to Self");
        box.Text = "start end";
        box.CaretIndex = 6;                      // after "start " - typing a word before "end"
        Type(box, "nts ");
        Assert.Equal("start Note to Self end", box.Text);
        Assert.Equal("start Note to Self ".Length, box.CaretIndex);
    });

    [Fact]
    public void ASlipEndingTheBox_IsFixedWhenTheUserMovesOnOrSaves() => wpf.Run(() =>
    {
        var box = new TextBox();
        var fixTheEnd = AutoCorrectBox.Attach(box, () => true, () => AutoCorrector.ParseEntries("nts = Note to Self"), requireFocus: false);

        Type(box, "Call Sam nts");
        Assert.Equal("Call Sam nts", box.Text);          // nothing typed after it yet

        fixTheEnd();                                      // what Save does
        Assert.Equal("Call Sam Note to Self", box.Text);
    });
}

internal static class AutoCorrectTestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
