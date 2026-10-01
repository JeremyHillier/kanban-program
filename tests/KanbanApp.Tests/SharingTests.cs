using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A task remembers each time it was emailed, so the task screen can say when it was last shared.
[Collection(WpfCollection.Name)]
public sealed class SharingTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void EachEmail_IsRemembered_NewestFirst_AndSurvivesReopening() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Order the parts", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        Assert.Empty(board.GetEmailHistory(card));

        board.RecordCardEmailed(card, "sam@example.com", "in Outlook");
        board.RecordCardEmailed(card, " sam@example.com; priya@example.com ", "in your email app");

        var reopened = OpenBoard();
        var again = reopened.Columns.SelectMany(c => c.Cards).Single(c => c.Title == "Order the parts");
        var history = reopened.GetEmailHistory(again);

        Assert.Equal(2, history.Count);
        Assert.Equal("to sam@example.com; priya@example.com, in your email app", history[0].Details);
        Assert.Equal("to sam@example.com, in Outlook", history[1].Details);
        Assert.True(history[0].When >= history[1].When);
    });

    [Fact]
    public void TheHistory_IsOnlyEverThatTasks() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var a = board.AddCard("A", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        var b = board.AddCard("B", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        board.RecordCardEmailed(a, "sam@example.com", "in Outlook");

        Assert.Single(board.GetEmailHistory(a));
        Assert.Empty(board.GetEmailHistory(b));
    });

    [Fact]
    public void TheStampLine_SaysTheLatest_AndHowManyTimes()
    {
        Assert.Null(MainViewModel.EmailStampText([]));

        var once = new List<CardEmailRecord> { new(new DateTime(2026, 9, 30, 14, 15, 0), "to sam@example.com, in Outlook") };
        Assert.Equal("Emailed Sep 30, 2026, 2:15 PM to sam@example.com", MainViewModel.EmailStampText(once));

        var thrice = new List<CardEmailRecord>
        {
            new(new DateTime(2026, 10, 1, 9, 0, 0), "to priya@example.com, in Outlook"),
            new(new DateTime(2026, 9, 30, 14, 15, 0), "to sam@example.com, in Outlook"),
            new(new DateTime(2026, 9, 29, 8, 5, 0), "to sam@example.com, in your email app"),
        };
        Assert.Equal("Emailed Oct 1, 2026, 9:00 AM to priya@example.com (3 times)", MainViewModel.EmailStampText(thrice));
        Assert.Equal(
            "Oct 1, 2026, 9:00 AM to priya@example.com, in Outlook\nSep 30, 2026, 2:15 PM to sam@example.com, in Outlook\nSep 29, 2026, 8:05 AM to sam@example.com, in your email app",
            MainViewModel.EmailHistoryText(thrice));
    }
}
