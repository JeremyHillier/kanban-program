using KanbanApp.Views;

namespace KanbanApp.Tests;

// The name a report's PDF is given, and when it goes straight into the default export folder
// rather than through a Save dialog.
public sealed class ReportPdfLocationTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly DateTime Now = new(2026, 10, 5, 14, 7, 9);

    [Theory]
    [InlineData("Kanban Task Report", "Kanban Task Report_20261005_140709.pdf")]
    [InlineData("Q4: Plan/Review? <draft>", "Q4_ Plan_Review_ _draft__20261005_140709.pdf")]
    [InlineData("Status | Week *1*", "Status _ Week _1__20261005_140709.pdf")]
    [InlineData("a\\b\"c", "a_b_c_20261005_140709.pdf")]
    public void TheFileName_IsTheTitleWithUnsafeCharactersReplaced_PlusTheTime(string title, string expected) =>
        Assert.Equal(expected, ReportPdfLocation.FileName(title, Now));

    [Fact]
    public void AnExistingDefaultFolder_IsUsedDirectly() =>
        Assert.Equal(System.IO.Path.Combine(_temp.Path, "r.pdf"), ReportPdfLocation.InDefaultFolder(_temp.Path, "r.pdf"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankDefaultFolder_MeansAsking(string folder) =>
        Assert.Null(ReportPdfLocation.InDefaultFolder(folder, "r.pdf"));

    [Fact]
    public void AMissingDefaultFolder_MeansAsking() =>
        Assert.Null(ReportPdfLocation.InDefaultFolder(_temp.File("gone"), "r.pdf"));
}

// Email... in the Report Builder and Quick Report: what the email says, and that both windows offer it.
public sealed class ReportEmailTests
{
    [Fact]
    public void TheEmailBody_NamesTheReportAndItsDate() =>
        Assert.Equal("Attached is the report \"Weekly review\", as of October 24, 2026.",
            ReportEmail.Body("Weekly review", new DateTime(2026, 10, 24, 9, 30, 0)));

    [Theory]
    [InlineData("ReportBuilderWindow.xaml")]
    [InlineData("QuickReportWindow.xaml")]
    public void BothReportWindows_HaveAnEmailButton(string file)
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "KanbanApp", "Views", file));

        Assert.Contains("Content=\"_Email...\"", xaml);
        Assert.Contains("Click=\"Email_Click\"", xaml);
    }
}
