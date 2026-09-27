using System.IO;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// What's New reads CHANGELOG.md: a version's "> - " summary when it has one, otherwise its "- " lines
// (the entries written before the summary layout).
public sealed class ReleaseNotesTests
{
    private const string Sample =
        "# Changelog\n\n- An intro bullet, not a release.\n\n" +
        "## 0.2.0 — 2026-09-27\n\n> - Improved: the point.\n> - New: another point.\n\n- A detail for the record.\n\n" +
        "## 0.1.0 — 2026-09-20\r\n- Fixed: an older entry, all bullets.\r\n- A second bullet.\r\n";

    [Fact]
    public void ASummaryIsAllThatIsShown_AndAnOlderEntryShowsItsBullets()
    {
        var notes = ReleaseNotes.Parse(Sample);
        Assert.Equal(["0.2.0", "0.1.0"], notes.Select(n => n.Version));
        Assert.Equal("2026-09-27", notes[0].Date);
        Assert.Equal(["Improved: the point.", "New: another point."], notes[0].Items);
        Assert.Equal(["Fixed: an older entry, all bullets.", "A second bullet."], notes[1].Items);
    }

    [Fact]
    public void OnlyTheNewestVersionsAreRead()
    {
        Assert.Equal(["0.2.0"], ReleaseNotes.Parse(Sample, maxVersions: 1).Select(n => n.Version));
    }

    [Fact]
    public void EveryVersionTheScreenShowsHasSomethingToSay()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var notes = ReleaseNotes.Parse(File.ReadAllText(Path.Combine(dir.FullName, "CHANGELOG.md")));
        Assert.Equal(5, notes.Count);
        Assert.All(notes, n => Assert.NotEmpty(n.Items));
    }
}
