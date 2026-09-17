using System.IO;
using System.Text;
using KanbanApp.Services;
using Xunit;

namespace KanbanApp.Tests;

public class UrlLauncherTests
{
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("  www.example.com/page?x=1  ", "https://www.example.com/page?x=1")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("HTTPS://Example.com/a", "https://example.com/a")]
    [InlineData("http://localhost:8080/", "http://localhost:8080/")]
    [InlineData("name@example.com", "mailto:name@example.com")]
    [InlineData("mailto:name@example.com?subject=Hi", "mailto:name@example.com?subject=Hi")]
    // A bare program name only ever reaches the browser as a web address, never Windows' program launcher.
    [InlineData("calc.exe", "https://calc.exe/")]
    public void Web_and_email_links_are_allowed(string typed, string expected)
    {
        Assert.True(UrlLauncher.TryNormalize(typed, out var target));
        Assert.Equal(expected, target);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("C:\\Windows\\System32\\calc.exe")]
    [InlineData("C:/Windows/System32/calc.exe")]
    [InlineData("\\\\server\\share\\run.bat")]
    [InlineData("//server/share/run.bat")]
    [InlineData("ms-settings:")]
    [InlineData("search-ms:query=x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("shell:::{something}")]
    [InlineData("https://user:pw@example.com")]
    [InlineData("\"https://example.com\" --flag")]
    [InlineData("https://example.com/a b")]
    [InlineData("intranet")]
    [InlineData("mailto:")]
    [InlineData("mailto:bob")]
    [InlineData("mailto:a@b.com/../../evil")]
    [InlineData("@example.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void Anything_else_is_refused(string typed)
    {
        Assert.False(UrlLauncher.TryNormalize(typed, out var target));
        Assert.Equal(string.Empty, target);
    }
}

public class ProblemReportTests
{
    [Fact]
    public void Crash_log_sits_next_to_the_task_file()
    {
        Assert.Equal(Path.Combine(@"D:\Tasks", "crash.log"), ProblemReport.CrashLogPath(@"D:\Tasks\kanban.db"));
    }

    [Fact]
    public void No_log_means_no_attachment()
    {
        using var temp = new TempFolder();
        Assert.Null(ProblemReport.PrepareLogCopy(temp.File("crash.log"), temp.File("out")));

        File.WriteAllText(temp.File("crash.log"), "");
        Assert.Null(ProblemReport.PrepareLogCopy(temp.File("crash.log"), temp.File("out")));
    }

    [Fact]
    public void Small_log_is_copied_whole()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.File("crash.log"), "2026-09-17 10:00:00\nSystem.Exception: boom\n\n");

        var copy = ProblemReport.PrepareLogCopy(temp.File("crash.log"), temp.File("out"));

        Assert.NotNull(copy);
        Assert.NotEqual(temp.File("crash.log"), copy);
        Assert.Equal(File.ReadAllText(temp.File("crash.log")), File.ReadAllText(copy!));
    }

    [Fact]
    public void Big_log_keeps_only_the_newest_part_from_a_line_start()
    {
        using var temp = new TempFolder();
        var sb = new StringBuilder();
        for (var i = 0; sb.Length < ProblemReport.MaxLogBytes * 2; i++) sb.Append($"entry {i:D6} xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n");
        sb.Append("LAST ENTRY\n");
        File.WriteAllText(temp.File("crash.log"), sb.ToString());

        var text = File.ReadAllText(ProblemReport.PrepareLogCopy(temp.File("crash.log"), temp.File("out"))!);

        Assert.True(text.Length <= ProblemReport.MaxLogBytes);
        Assert.StartsWith("entry ", text);
        Assert.EndsWith("LAST ENTRY\n", text);
    }

    [Fact]
    public void Log_still_being_written_can_be_copied()
    {
        using var temp = new TempFolder();
        using var writer = new FileStream(temp.File("crash.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write("open entry\n"u8);
        writer.Flush();

        var copy = ProblemReport.PrepareLogCopy(temp.File("crash.log"), temp.File("out"));

        Assert.Equal("open entry\n", File.ReadAllText(copy!));
    }

    [Fact]
    public void Body_carries_version_and_says_whether_the_log_is_attached()
    {
        var withLog = ProblemReport.BuildBody("1.2.3", hasLog: true, new DateTime(2026, 9, 17, 14, 5, 0));
        Assert.Contains("version 1.2.3", withLog);
        Assert.Contains("Reported: 2026-09-17 14:05", withLog);
        Assert.Contains("crash.log) is attached", withLog);

        var withoutLog = ProblemReport.BuildBody("1.2.3", hasLog: false, DateTime.Now);
        Assert.Contains("No error log was found", withoutLog);
        Assert.DoesNotContain("attached", withoutLog);
    }

    [Fact]
    public void Support_address_is_a_valid_email_link()
    {
        Assert.True(UrlLauncher.TryNormalize(AppInfo.SupportEmail, out var target));
        Assert.StartsWith("mailto:", target);
    }
}
