using System.IO;

namespace KanbanApp.Tests;

// Settings is three tabs, so no page is long, and Help describes each one.
public sealed class SettingsTabsTests
{
    private static string Source(params string[] path)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine([dir!.FullName, "src", "KanbanApp", .. path]));
    }

    [Theory]
    [InlineData("Board", "Board tab")]
    [InlineData("General", "General tab")]
    [InlineData("Data and files", "Data and files tab")]
    public void EachSettingsTab_IsDescribedInHelp(string header, string helpHeading)
    {
        Assert.Contains($"<TabItem Header=\"{header}\"", Source("Views", "SettingsWindow.xaml"));
        Assert.Contains($"Text=\"{helpHeading}\"", Source("Views", "HelpWindow.xaml"));
    }

    [Fact]
    public void EverySectionOfSettings_SitsInsideATab()
    {
        var xaml = Source("Views", "SettingsWindow.xaml");
        var firstTab = xaml.IndexOf("<TabItem", StringComparison.Ordinal);
        var firstSection = xaml.IndexOf("SectionHeaderStyle}\"/>", firstTab - 1, StringComparison.Ordinal);

        Assert.True(firstTab > 0 && firstSection > firstTab);
        // The window's own scroll viewer is gone: each tab scrolls on its own.
        Assert.DoesNotContain("<ScrollViewer Grid.Row=\"0\"", xaml);
    }
}

// Help's "Where your files are" names every file and folder the app keeps, and the names are the real ones.
public sealed class HelpFilesSectionTests
{
    private static string Help()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "KanbanApp", "Views", "HelpWindow.xaml"));
    }

    [Fact]
    public void EveryFileTheAppKeeps_IsNamedInHelp_UnderItsRealName()
    {
        var help = Help();
        var db = Path.Combine("C:", "Tasks", "kanban.db");

        Assert.Contains("HelpSection_WhereFiles", help);
        Assert.Contains(Path.GetFileName(KanbanApp.Services.AppConfig.SettingsFilePath), help);                        // config.json
        Assert.Contains(Path.GetFileName(new KanbanApp.Services.UserDictionary("C:\\x").FilePath), help);             // UserDictionary.lex
        Assert.Contains(Path.GetFileName(KanbanApp.Services.BackupService.GetBackupsDir(db)), help);                     // Backups
        Assert.Contains(Path.GetFileName(KanbanApp.Services.DatabaseService.GetAttachmentsDir(db)), help);               // Attachments
        Assert.Contains(Path.GetFileName(KanbanApp.Services.ProblemReport.CrashLogPath(db)), help);                      // crash.log
        Assert.Contains(Path.GetFileName(KanbanApp.Services.AppConfig.DefaultDbPath), help);                             // kanban.db
    }
}
