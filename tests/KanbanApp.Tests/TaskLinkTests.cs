using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Tests;

// A link to a task, put in an Outlook event or an email, that opens the task when clicked.
public class TaskLinkRuleTests
{
    private const string Task = "0f8fad5bd9cb469fa16570867728950e";
    private const string File = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    [Fact]
    public void ALink_ReadsBackToTheTwoIDs()
    {
        var link = TaskLinks.Build(Task, File);

        Assert.Equal($"{TaskLinks.Scheme}://task/{Task}?file={File}", link);
        Assert.True(TaskLinks.TryParse(link, out var task, out var file));
        Assert.Equal((Task, File), (task, file));
        Assert.True(TaskLinks.IsLink(link));
    }

    [Theory]
    [InlineData("  {0}  ")]                      // spaces round it
    [InlineData("\"{0}\"")]                      // Windows hands a clicked link over in quotes
    [InlineData("{0}/")]                         // some programs add a trailing slash
    public void Windows_AndMailPrograms_MayDecorateALink(string shape)
    {
        var link = shape.Replace("{0}", TaskLinks.Build(Task.ToUpperInvariant(), File.ToUpperInvariant()));

        Assert.True(TaskLinks.TryParse(link, out var task, out var file));
        Assert.Equal((Task, File), (task, file));         // ids are lower-cased
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("https://example.com/task/0f8fad5bd9cb469fa16570867728950e?file=a1b2c3d4e5f60718293a4b5c6d7e8f90")]
    [InlineData("kanbantask-other://task/0f8fad5bd9cb469fa16570867728950e?file=a1b2c3d4e5f60718293a4b5c6d7e8f90")]
    [InlineData("{S}://card/0f8fad5bd9cb469fa16570867728950e?file=a1b2c3d4e5f60718293a4b5c6d7e8f90")]       // not a task
    [InlineData("{S}://task/0f8fad5bd9cb469fa1657086772895?file=a1b2c3d4e5f60718293a4b5c6d7e8f90")]        // short id
    [InlineData("{S}://task/0f8fad5bd9cb469fa16570867728950e?file=a1b2c3")]                                  // short file id
    [InlineData("{S}://task/0f8fad5bd9cb469fa16570867728950e")]                                              // no file
    [InlineData("{S}://task/0f8fad5bd9cb469fa16570867728950e?file=a1b2c3d4e5f60718293a4b5c6d7e8f90&x=1")]   // extra
    [InlineData("{S}://task/../x?file=a1b2c3d4e5f60718293a4b5c6d7e8f90")]
    [InlineData("{S}://task/0f8fad5bd9cb469fa16570867728950g?file=a1b2c3d4e5f60718293a4b5c6d7e8f90")]       // not hex
    [InlineData("{S}://task/0f8fad5bd9cb469fa16570867728950e?file=a1b2c3d4e5f60718293a4b5c6d7e8f90 --evil")]
    public void AnythingElse_IsRefused(string? text)
    {
        text = text?.Replace("{S}", TaskLinks.Scheme);
        Assert.False(TaskLinks.TryParse(text, out var task, out var file));
        Assert.Equal((string.Empty, string.Empty), (task, file));
        Assert.False(TaskLinks.IsLink(text));
    }

    [Fact]
    public void TheCommand_QuotesThePath_AndPassesTheLinkAsOneArgument()
    {
        Assert.Equal("\"C:\\Program Files\\Kanban\\KanbanApp.exe\" \"%1\"", TaskLinks.CommandFor(@"C:\Program Files\Kanban\KanbanApp.exe"));
    }

    [Fact]
    public void Registering_WritesOnce_AndPutsRightWhatWasLeftByAnotherInstall()
    {
        TaskLinks.UseInMemoryRegistry(out var stored);
        var writes = 0;
        var write = TaskLinks.WriteRegistration;
        TaskLinks.WriteRegistration = (exe, command) => { writes++; write(exe, command); };

        TaskLinks.Register();
        var expected = TaskLinks.CommandFor(Environment.ProcessPath!);
        Assert.Equal(expected, stored());
        Assert.Equal(1, writes);

        TaskLinks.Register();                                   // already right: nothing written
        Assert.Equal(1, writes);

        TaskLinks.WriteRegistration = (_, _) => writes++;       // an older install's path is there now
        TaskLinks.ReadCommand = () => "\"D:\\Old\\KanbanApp.exe\" \"%1\"";
        TaskLinks.Register();
        Assert.Equal(2, writes);

        TaskLinks.UseInMemoryRegistry(out _);                   // leave the test registry as it was
    }
}

[Collection(WpfCollection.Name)]
public sealed class TaskIdentityTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string name = "board.db") => new(new DatabaseService(_temp.File(name)));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    [Fact]
    public void EveryTask_HasItsOwnPermanentID_FromTheStart() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = Enumerable.Range(1, 5).Select(i => board.AddCard($"Task {i}", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null)).ToList();

        Assert.All(cards, c => Assert.Matches("^[0-9a-f]{32}$", c.ShareId!));
        Assert.Equal(5, cards.Select(c => c.ShareId).Distinct().Count());
        Assert.Equal(cards.Select(c => c.ShareId), OpenBoard().Columns.SelectMany(c => c.Cards).OrderBy(c => c.Title).Select(c => c.ShareId)); // kept
    });

    [Fact]
    public void ATaskFileFromBefore_IsGivenItsIDsOnce_AndOlderCopiesWarnFromThen() => wpf.Run(() =>
    {
        var path = _temp.File("old.db");
        var db = new DatabaseService(path);
        var board = new MainViewModel(db);
        board.AddCard("One", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);
        board.AddCard("Two", Column(board, "Done"), board.Projects.First(), "Normal", null, null, false, null, null);
        board.ArchiveDoneTasks();

        using (var connection = new SqliteConnection($"Data Source={path}"))   // a file made before IDs: take them away
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Cards SET ShareId = NULL;";
            cmd.ExecuteNonQuery();
        }

        var reopened = new DatabaseService(path);
        Assert.Equal(2, reopened.CountTasksWithoutId());                       // the archived one too

        Assert.True(reopened.EnsureEveryTaskHasAnId());
        Assert.Equal(0, reopened.CountTasksWithoutId());
        Assert.False(reopened.EnsureEveryTaskHasAnId());                       // nothing left to do
        Assert.All(new MainViewModel(reopened).Columns.SelectMany(c => c.Cards), c => Assert.Matches("^[0-9a-f]{32}$", c.ShareId!));
    });

    [Fact]
    public void TheTaskFile_HasAnIdentityOfItsOwn_ThatSurvivesBeingOpenedAgain() => wpf.Run(() =>
    {
        var first = new DatabaseService(_temp.File("a.db")).GetTaskFileId();

        Assert.Matches("^[0-9a-f]{32}$", first);
        Assert.Equal(first, new DatabaseService(_temp.File("a.db")).GetTaskFileId());
        Assert.NotEqual(first, new DatabaseService(_temp.File("b.db")).GetTaskFileId());
    });

    [Fact]
    public void ATaskIsFoundWhereverItIs_AndTheLinkNamesIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var onBoard = board.AddCard("On the board", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);
        var archived = board.AddCard("Put away", Column(board, "Done"), board.Projects.First(), "Normal", null, null, false, null, null);
        var deleted = board.AddCard("Thrown out", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);
        board.ArchiveDoneTasks();
        board.DeleteCard(deleted);

        var here = board.FindTask(onBoard.ShareId!)!;
        Assert.True(here.IsOnBoard);
        Assert.Equal("On the board", here.Title);

        var away = board.FindTask(archived.ShareId!)!;
        Assert.True(away.IsArchived);
        Assert.False(away.IsOnBoard);
        Assert.InRange(away.PutAwayAt!.Value, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(1));

        Assert.True(board.FindTask(deleted.ShareId!)!.IsDeleted);
        Assert.Null(board.FindTask(new string('0', 32)));

        var link = board.TaskLinkFor(onBoard);
        Assert.True(TaskLinks.TryParse(link, out var shareId, out var fileId));
        Assert.Equal(onBoard.ShareId, shareId);
        Assert.Equal(board.TaskFileId, fileId);
        Assert.Same(onBoard, board.FindSharedCard(shareId));                    // what a click would open
    });

    [Fact]
    public void TheEmailsAndTheCalendarFile_CarryTheLink() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Draft the plan", Column(board, "To Do"), board.Projects.First(), "Normal", new DateTime(2030, 3, 1), null, false, null, null);
        var link = board.TaskLinkFor(card);

        Assert.Contains($"Open in Kanban Task Board: {link}\r\n", OutlookEmailHelper.BuildPlainTextBody(card, null, "To Do", link));
        Assert.DoesNotContain("Open in Kanban Task Board:", OutlookEmailHelper.BuildPlainTextBody(card, null, "To Do"));

        var ics = CalendarFile.BuildIcs("Draft the plan", "Project: General", CalendarFile.For(card.DueDate, null, DateTime.Now), DateTime.UtcNow, "x", link);
        Assert.Contains("\r\nURL:" + link[..40], ics);                          // folded after 75 bytes, so check the start
        var unfolded = ics.Replace("\r\n ", string.Empty);
        Assert.Contains($"\r\nURL:{link}\r\n", unfolded);
        Assert.Contains($"Open in Kanban Task Board: {link}", unfolded.Replace("\\n", "\n"));
    });
}
