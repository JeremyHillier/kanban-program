using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A task's link can be a file or folder as well as a web or email address - but never a program or
// script, because a task file can come from someone else.
public class PathLinkRuleTests
{
    [Theory]
    [InlineData(@"C:\Clients\Acme\Contract.pdf")]
    [InlineData(@"D:\Projects")]
    [InlineData(@"C:\Users\Sam Lee\OneDrive\Plan 2026.xlsx")]
    [InlineData("C:/Clients/Acme/Contract.pdf")]
    [InlineData(@"\\server\share\Reports\Q3.docx")]
    [InlineData(@"\\server\share")]
    [InlineData(@"""C:\Clients\Acme\Contract.pdf""")]        // Explorer's "Copy as path"
    [InlineData("file:///C:/Clients/Acme/My%20File.pdf")]
    public void AFileOrFolderPath_IsAllowed(string typed)
    {
        Assert.NotNull(UrlLauncher.ValidLink(typed));
        Assert.True(UrlLauncher.IsPathLink(typed));
    }

    [Fact]
    public void QuotesAndFileLinks_AreTidiedIntoThePlainPath()
    {
        Assert.Equal(@"C:\Clients\Acme\Contract.pdf", UrlLauncher.ValidLink(@" ""C:\Clients\Acme\Contract.pdf"" "));
        Assert.Equal(@"C:\Clients\Acme\My File.pdf", UrlLauncher.ValidLink("file:///C:/Clients/Acme/My%20File.pdf"));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData(@"C:\tools\run.BAT")]
    [InlineData(@"C:\tools\run.cmd")]
    [InlineData(@"C:\tools\run.ps1")]
    [InlineData(@"C:\tools\run.vbs")]
    [InlineData(@"C:\tools\run.js")]
    [InlineData(@"C:\tools\setup.msi")]
    [InlineData(@"C:\Users\Sam\Desktop\App.lnk")]
    [InlineData(@"C:\tools\run.exe.")]                      // Windows ignores the trailing dot
    [InlineData(@"C:\tools\run.exe ")]
    [InlineData(@"\\server\share\run.exe")]
    [InlineData(@"\\server\share\macro.hta")]
    public void AProgramOrScript_IsRefused(string typed)
    {
        Assert.Null(UrlLauncher.ValidLink(typed));
        Assert.False(UrlLauncher.IsPathLink(typed));
    }

    [Theory]
    [InlineData(@"\\?\C:\Windows\System32\calc.exe")]       // device path
    [InlineData(@"\\.\pipe\x")]
    [InlineData(@"\\server@SSL\share\file.pdf")]            // WebDAV over the internet
    [InlineData(@"\\server")]                               // no share
    [InlineData(@"C:\file.txt:hidden")]                     // a data stream
    [InlineData(@"C:\*.pdf")]
    [InlineData(@"C:\a|b")]
    [InlineData(@"C:file.pdf")]                             // relative to a drive's current folder
    [InlineData(@"Clients\Contract.pdf")]
    [InlineData("C:")]
    public void OddPaths_AreRefused(string typed) => Assert.Null(UrlLauncher.ValidLink(typed));

    [Fact]
    public void WebAndEmailLinks_StillWork_AsBefore()
    {
        Assert.Equal("example.com/report", UrlLauncher.ValidLink(" example.com/report "));
        Assert.Equal("name@example.com", UrlLauncher.ValidLink("name@example.com"));
        Assert.False(UrlLauncher.IsPathLink("example.com"));
        Assert.Null(UrlLauncher.ValidLink("ms-settings:"));
        Assert.Null(UrlLauncher.ValidLink("shell:::{x}"));
    }

    [Theory]
    [InlineData(@"\\server\share\a.pdf", true)]
    [InlineData(@"C:\a.pdf", false)]
    public void OnlyNetworkPaths_AskFirst(string path, bool network) => Assert.Equal(network, UrlLauncher.IsNetworkPath(path));
}

[Collection(WpfCollection.Name)]
public sealed class PathLinkTaskTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private string FileFormat() => new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat")!;

    [Fact]
    public void ASavedFileLink_StampsTheFile_SoOlderCopiesWarn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var col = board.Columns.First();
        board.AddCard("Web", col, board.Projects.First(), "Normal", null, null, false, null, null, websiteUrl: "example.com");
        Assert.Equal("5", FileFormat());

        board.AddCard("Contract", col, board.Projects.First(), "Normal", null, null, false, null, null, websiteUrl: @"C:\Clients\Acme\Contract.pdf");
        Assert.Equal("7", FileFormat());
    });

    [Fact]
    public void AnImportedLink_KeepsAPath_AndDropsAProgram() => wpf.Run(() =>
    {
        var board = OpenBoard();

        var cards = board.ImportCards(
        [
            new KanbanApp.Models.ImportedTaskRow { Title = "a", WebsiteUrl = @"""D:\Projects\Acme""" },
            new KanbanApp.Models.ImportedTaskRow { Title = "b", WebsiteUrl = @"\\server\share\run.exe" },
            new KanbanApp.Models.ImportedTaskRow { Title = "c", WebsiteUrl = "file:///C:/Clients/Plan.xlsx" },
        ]);

        Assert.Equal(@"D:\Projects\Acme", cards[0].WebsiteUrl);
        Assert.Null(cards[1].WebsiteUrl);
        Assert.Equal(@"C:\Clients\Plan.xlsx", cards[2].WebsiteUrl);
    });

    [Fact]
    public void ALinkTravelsByEmail_UnderTheOldHeadingToo() => wpf.Run(() =>
    {
        var path = _temp.File("link.xlsx");
        ImportService.SaveSingleTaskFile(path, new KanbanApp.Models.ImportedTaskRow { Title = "Contract", WebsiteUrl = @"C:\Clients\Contract.pdf" });

        using (var workbook = new ClosedXML.Excel.XLWorkbook(path))
        {
            Assert.Contains("Website", workbook.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString())); // older copies read this heading
        }

        Assert.Equal(@"C:\Clients\Contract.pdf", Assert.Single(ImportService.ReadTasks(path)).WebsiteUrl);
    });
}
