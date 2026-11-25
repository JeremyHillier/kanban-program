using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// crash.log keeps one entry per burst of the same error, and a layout error that keeps coming sets the blocks aside.
[Collection(WpfCollection.Name)]
public sealed class CrashLogTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly DateTime Start = new(2026, 11, 25, 14, 42, 17);

    private static InvalidOperationException LayoutError() => new(
        "Layout measurement override of element 'System.Windows.Controls.WrapPanel' should not return PositiveInfinity as its DesiredSize, even if Infinity is passed in as available size.");

    [Fact]
    public void TheSameErrorAgainAndAgain_IsWrittenOnce_ThenCounted()
    {
        var log = new CrashLog();
        var texts = new List<string?>();
        var inARow = 0;
        for (var i = 0; i < 250; i++)
        {
            (var text, inARow) = log.Record(LayoutError(), Start.AddMilliseconds(50 * i), "0.178.2 (Kanban Task Board)");
            texts.Add(text);
        }

        Assert.Equal(250, inARow);
        Assert.Contains("version 0.178.2 (Kanban Task Board)", texts[0]);
        Assert.Contains("WrapPanel", texts[0]);
        Assert.Equal(2, texts.Skip(1).Count(t => t is not null)); // "100 more" and "200 more" only
        Assert.Contains("the same error 100 more times so far", texts[100]);

        var (next, count) = log.Record(new ArgumentException("Something else"), Start.AddSeconds(20), "0.178.2");
        Assert.Equal(1, count);
        Assert.StartsWith("2026-11-25 14:42:29  (the error above came 249 more times, the last at this time)", next);
        Assert.Contains("Something else", next);
    }

    [Fact]
    public void TheSameErrorLater_IsWrittenInFullAgain()
    {
        var log = new CrashLog();
        log.Record(LayoutError(), Start, "1");
        var (text, inARow) = log.Record(LayoutError(), Start + CrashLog.Burst + TimeSpan.FromSeconds(1), "1");

        Assert.Equal(1, inARow);
        Assert.Contains("WrapPanel", text);
    }

    [Fact]
    public void OnlyLayoutErrors_CountAsLayoutErrors()
    {
        Assert.True(CrashLog.IsLayoutError(LayoutError()));
        Assert.True(CrashLog.IsLayoutError(new InvalidOperationException("Layout measurement override of element 'X' should not return NaN values as its DesiredSize.")));
        Assert.False(CrashLog.IsLayoutError(new InvalidOperationException("Collection was modified.")));
        Assert.False(CrashLog.IsLayoutError(new ArgumentException("Layout measurement override of element 'X'")));
    }

    [Fact]
    public void SettingTheBlocksAside_DrawsColumns_UntilTheyAreSwitchedOnAgain() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        board.SetShowColumnsAsBlocks(true);
        Assert.True(board.IsBlocksOn);

        board.SetAsideBlocksForThisSession();
        Assert.False(board.IsBlocksOn);
        Assert.True(board.IsPlainBoardOn);
        Assert.True(board.ShowColumnsAsBlocks); // the setting is kept for next time
        Assert.True(new MainViewModel(new DatabaseService(_temp.File("board.db"))).IsBlocksOn);

        board.SetShowColumnsAsBlocks(true);
        Assert.True(board.IsBlocksOn);
    });
}
