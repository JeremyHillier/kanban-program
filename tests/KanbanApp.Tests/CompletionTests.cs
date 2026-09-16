using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// When a task is finished: stamped on the way into Done, cleared on the way out, and shown on the
// card, in the Archived list, and in reports.
[Collection(WpfCollection.Name)]
public sealed class CompletionTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, string column = "To Do") =>
        board.AddCard(title, Column(board, column), board.Projects.First(), "Normal", null, null, false, null, null);

    private static void AssertJustNow(DateTime? stamp, DateTime before)
    {
        Assert.NotNull(stamp);
        Assert.InRange(stamp.Value, before.AddSeconds(-1), DateTime.Now.AddSeconds(1));
    }

    [Fact]
    public void MovingToDone_StampsTheCompletionTime_AndItIsSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");
        Assert.Null(card.CompletedAt);

        var before = DateTime.Now;
        board.MoveCardCommand.Execute((card, Column(board, "Done")));

        AssertJustNow(card.CompletedAt, before);
        Assert.Equal(card.CompletedAt, Assert.Single(Column(OpenBoard(), "Done").Cards).CompletedAt);
    });

    [Fact]
    public void MovingOutOfDone_ClearsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");
        board.MoveCardCommand.Execute((card, Column(board, "Done")));

        board.MoveCardCommand.Execute((card, Column(board, "In Progress")));

        Assert.Null(card.CompletedAt);
        Assert.Null(Assert.Single(OpenBoard().Columns.SelectMany(c => c.Cards)).CompletedAt);
    });

    [Fact]
    public void MovingBetweenOtherColumns_NeverStampsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");

        board.MoveCardCommand.Execute((card, Column(board, "In Progress")));
        board.MoveCardCommand.Execute((card, Column(board, "Waiting")));

        Assert.Null(card.CompletedAt);
    });

    [Fact]
    public void CreatingATaskDirectlyInDone_StampsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var before = DateTime.Now;

        var card = Add(board, "Already finished", column: "Done");

        AssertJustNow(card.CompletedAt, before);
    });

    [Fact]
    public void EditingAFinishedTask_KeepsItsCompletionTime() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");
        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        var completedAt = new DateTime(2026, 1, 2, 3, 4, 5);
        card.CompletedAt = completedAt; // pretend it was finished a while ago
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_temp.File("board.db")}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Cards SET CompletedAt = '2026-01-02 03:04:05'";
            cmd.ExecuteNonQuery();
        }

        board.EditCard(card, "Write report (with notes)", Column(board, "Done"), board.Projects.First(), "High", null, null,
            false, null, null, card.Flags, card.SubTasks, "Sent to Sam", card.Attachments, card.ForceEditOnComplete, card.WebsiteUrl, card.DueTime);

        Assert.Equal(completedAt, card.CompletedAt);
        Assert.Equal(completedAt, Assert.Single(Column(OpenBoard(), "Done").Cards).CompletedAt);
    });

    [Fact]
    public void ArchivedTasks_KeepTheirCompletionTime_AndReactivatingClearsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");
        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        var completedAt = card.CompletedAt;

        board.ArchiveDoneTasks();

        var archived = Assert.Single(board.GetArchivedCards());
        Assert.Equal(completedAt, archived.CompletedAt);
        Assert.Equal(completedAt, Assert.Single(board.GetArchivedReportRows()).Card.CompletedAt);

        board.ReactivateCard(archived.Id, archived.Title);

        var reactivated = Assert.Single(Column(board, "To Do").Cards);
        Assert.Null(reactivated.CompletedAt);
    });

    [Fact]
    public void Card_ShowsCompletedOnceDone_OtherwiseUpdated() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");
        Assert.StartsWith("Updated ", card.StatusStampDisplay);

        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        Assert.Equal($"Completed {card.CompletedAt:MMM d, h:mm tt}", card.StatusStampDisplay);

        card.CompletedAt = new DateTime(2024, 11, 3, 16, 5, 0);
        Assert.Equal("Completed Nov 3, 2024, 4:05 PM", card.StatusStampDisplay);
    });

    [Fact]
    public void EditDialogStamps_ShowCompletedOnlyOnceFinished_AndAlwaysTheLastUpdate() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Write report");
        card.LastUpdated = new DateTime(2026, 9, 14, 8, 15, 0);
        Assert.Null(card.CompletedFullDisplay);
        Assert.Equal("Last updated Sep 14, 2026, 8:15 AM", card.UpdatedFullDisplay);

        card.CompletedAt = new DateTime(2026, 9, 15, 15, 25, 0);
        Assert.Equal("Completed Sep 15, 2026, 3:25 PM", card.CompletedFullDisplay);
        Assert.Equal("Last updated Sep 14, 2026, 8:15 AM", card.UpdatedFullDisplay);

        card.LastUpdated = null;
        Assert.Equal("Last updated: unknown", card.UpdatedFullDisplay);
    });

    [Fact]
    public void Reports_ShowTheCompletionTime_AndCanSortByIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var late = Add(board, "Finished later", column: "Done");
        var early = Add(board, "Finished earlier", column: "Done");
        var open = Add(board, "Still open");
        late.CompletedAt = new DateTime(2026, 9, 10, 15, 0, 0);
        early.CompletedAt = new DateTime(2026, 9, 1, 9, 30, 0);

        var rows = ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(),
            [], [], [], "All", "All", "All", null, null, false, null, "Completed Date", "None", "None");

        Assert.Equal(["Finished earlier", "Finished later", "Still open"], rows.Select(r => r.Title));
        Assert.Contains("Completed Sep 1, 2026 9:30 AM", ReportService.BuildMetaParts(rows[0]));
        Assert.DoesNotContain(ReportService.BuildMetaParts(rows[2]), part => part.StartsWith("Completed"));
    });

    [Fact]
    public void ArchivedList_ShowsCompletedAndArchivedTimes()
    {
        var info = new ArchivedCardInfo { ArchivedAt = "2026-09-16 09:00:00", CompletedAt = new DateTime(2026, 9, 15, 14, 30, 0) };
        Assert.Equal("Completed Sep 15, 2026 2:30 PM • Archived Sep 16, 2026 9:00 AM", info.StampsDisplay);

        var unknown = new ArchivedCardInfo { ArchivedAt = "Unknown" };
        Assert.Equal("Completion date unknown • Archived Unknown", unknown.StampsDisplay);
    }
}
