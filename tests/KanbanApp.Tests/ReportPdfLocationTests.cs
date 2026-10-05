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
