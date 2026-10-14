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
    // Types one character the way the keyboard does: the preview event, then the text going in.
    private static void Type(TextBox box, string text)
    {
        foreach (var c in text)
        {
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, c.ToString()))
            {
                RoutedEvent = UIElement.PreviewTextInputEvent
            };
            box.RaiseEvent(args);
            if (!args.Handled && (box.MaxLength == 0 || box.Text.Length < box.MaxLength))
            {
                var at = box.CaretIndex;
                box.Text = box.Text.Insert(at, c.ToString());
                box.CaretIndex = at + 1;
            }
        }
    }

    private static TextBox Box(bool on = true, string own = "") => new TextBox().Also(b =>
        AutoCorrectBox.Attach(b, () => on, () => AutoCorrector.ParseEntries(own)));

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
}

internal static class AutoCorrectTestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
