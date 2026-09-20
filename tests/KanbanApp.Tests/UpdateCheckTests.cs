using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The check for a newer version: reading the reply, deciding what counts as newer, how often it
// runs, and the settings around it. Nothing here touches the internet.
[Collection(WpfCollection.Name)]
public sealed class UpdateCheckTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Theory]
    [InlineData("0.104.0", "0.103.0", true)]
    [InlineData("v0.104.0", "v0.103.0", true)]
    [InlineData("0.103.1", "0.103.0", true)]
    [InlineData("1.0.0", "0.999.9", true)]
    [InlineData("0.100.0", "0.99.0", true)]   // by number, not as text
    [InlineData("0.103.0", "0.103.0", false)]
    [InlineData("0.103", "0.103.0", false)]   // the same release written two ways
    [InlineData("0.102.0", "0.103.0", false)]
    [InlineData("banana", "0.103.0", false)]
    [InlineData("", "0.103.0", false)]
    [InlineData(null, "0.103.0", false)]
    [InlineData("0.104.0", "unknown", false)]
    public void WhatCountsAsNewer(string? latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));
    }

    [Fact]
    public void TheCheckRunsAtMostOnceADay_AndAClockSetBackwardsDoesNotStopIt()
    {
        var now = new DateTime(2026, 9, 20, 9, 0, 0);

        Assert.True(UpdateChecker.IsDue(null, now));
        Assert.False(UpdateChecker.IsDue(now.AddHours(-23), now));
        Assert.True(UpdateChecker.IsDue(now.AddHours(-24), now));
        Assert.True(UpdateChecker.IsDue(now.AddDays(3), now)); // "last checked" in the future
    }

    [Fact]
    public void AReleaseReply_GivesTheVersion_AndItsBulletsAsNotes()
    {
        const string json = """
            { "tag_name": "v0.104.0", "draft": false, "prerelease": false,
              "html_url": "https://evil.example/not-used",
              "body": "- New: first thing\r\n- Improved: second thing\r\n\r\nSome other line\r\n* A star bullet\r\n-   \r\n" }
            """;

        var update = UpdateChecker.ParseLatestRelease(json)!;

        Assert.Equal("0.104.0", update.Version);
        Assert.Equal(["New: first thing", "Improved: second thing", "A star bullet"], update.Notes);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v0.104.0", "draft": true }""")]
    [InlineData("""{ "tag_name": "v0.104.0", "prerelease": true }""")]
    [InlineData("""{ "tag_name": "latest" }""")]
    [InlineData("""{ "tag_name": 104 }""")]
    [InlineData("""{ "message": "Not Found" }""")]
    [InlineData("""["v0.104.0"]""")]
    [InlineData("<html>Sign in to the hotel wifi</html>")]
    [InlineData("")]
    public void AReplyThatIsNotAUsableRelease_IsIgnored(string json)
    {
        Assert.Null(UpdateChecker.ParseLatestRelease(json));
    }

    [Fact]
    public void VeryLongNotes_AreCutDownToSize()
    {
        var body = string.Join("\\n", Enumerable.Range(1, 100).Select(i => $"- {new string('x', 1000)} {i}"));
        var update = UpdateChecker.ParseLatestRelease($$"""{ "tag_name": "v9.0.0", "body": "{{body}}" }""")!;

        Assert.Equal(40, update.Notes.Count);
        Assert.All(update.Notes, note => Assert.True(note.Length <= 601));
    }

    [Fact]
    public void ANoteWithoutBullets_StillOffersTheVersion()
    {
        var update = UpdateChecker.ParseLatestRelease("""{ "tag_name": "v9.0.0", "body": null }""")!;

        Assert.Equal("9.0.0", update.Version);
        Assert.Empty(update.Notes);
    }

    [Fact]
    public void TheSetting_StartsOn_IsRemembered_AndSwitchesTheAutomaticCheckOff() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.True(board.CheckForUpdatesEnabled);
        Assert.True(board.IsUpdateCheckDue(DateTime.Now));

        board.SetCheckForUpdatesEnabled(false);

        var reopened = OpenBoard();
        Assert.False(reopened.CheckForUpdatesEnabled);
        Assert.False(reopened.IsUpdateCheckDue(DateTime.Now));
    });

    [Fact]
    public void ACheckIsRecorded_SoTheNextStartTodayStaysQuiet() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var now = new DateTime(2026, 9, 20, 9, 0, 0);

        board.RecordUpdateCheck(now);

        var reopened = OpenBoard();
        Assert.Equal(now, reopened.LastUpdateCheck);
        Assert.False(reopened.IsUpdateCheckDue(now.AddHours(5)));
        Assert.True(reopened.IsUpdateCheckDue(now.AddHours(25)));
    });

    [Fact]
    public void ASkippedVersionIsNotOfferedAgain_ButTheNextOneIs_AndAskingByHandAlwaysAnswers() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var newer = new UpdateInfo("999.0.0", ["New: something"]);
        Assert.True(board.ShouldOfferUpdate(newer));
        Assert.False(board.ShouldOfferUpdate(null));
        Assert.False(board.ShouldOfferUpdate(new UpdateInfo("0.0.1", [])));
        Assert.False(board.ShouldOfferUpdate(new UpdateInfo(board.AppVersion, []))); // the one already installed

        board.SkipUpdateVersion("999.0.0");

        var reopened = OpenBoard();
        Assert.False(reopened.ShouldOfferUpdate(newer));
        Assert.True(reopened.ShouldOfferUpdate(newer, askedByHand: true));
        Assert.True(reopened.ShouldOfferUpdate(new UpdateInfo("999.0.1", [])));
    });

    [Fact]
    public void TheCheckAsksOnlyThePublicDownloadsList_AndSendsPeopleOnlyToTheDownloadPage()
    {
        Assert.Equal("https://api.github.com/repos/JeremyHillier/kanban-task-board-downloads/releases/latest", UpdateChecker.LatestReleaseUrl);
        Assert.Equal("https://hillierconsulting.ca/kanban.html", AppInfo.DownloadPageUrl);
    }
}
