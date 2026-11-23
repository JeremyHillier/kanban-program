using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// A short report keeps each task's priority and due date on the title's line, right-aligned, when they fit.
public sealed class ReportShortLineTests
{
    [Fact]
    public void TheDetails_ShareTheTitleLine_OnlyWhenBothFitWithTheGap()
    {
        Assert.True(ReportService.SharesTitleLine(300, 150, 500));
        Assert.True(ReportService.SharesTitleLine(326, 150, 500));
        Assert.False(ReportService.SharesTitleLine(327, 150, 500));
    }
}

[Collection(WpfCollection.Name)]
public sealed class ReportShortLinePreviewTests(WpfDispatcherFixture wpf)
{
    private const string Details = "Priority: High   •   Due Oct 20, 2026";

    private static ReportRow Row(string title, bool brief = true) => new()
    {
        Title = title, ColumnName = "To Do", ProjectName = "General", Priority = "High", GoalName = "No Goal",
        DueDate = new DateTime(2026, 10, 20), Brief = brief, DueBucket = "Overdue"
    };

    private static IEnumerable<TextBlock> TextBlocks(DependencyObject root) =>
        LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()
            .SelectMany(child => child is TextBlock block ? Enumerable.Repeat(block, 1) : TextBlocks(child));

    [Fact]
    public void AShortRow_PutsItsDetailsAtTheRightOfItsTitle_OrUnderALongOne() => wpf.Run(() =>
    {
        var longTitle = string.Join(" ", Enumerable.Repeat("Send colonoscopy data to the doctor", 4));
        var document = ReportService.BuildFixedDocument("Today and This Week", [Row("Send colonoscopy data to doctor"), Row(longTitle)], "Due", false, false);
        var blocks = TextBlocks(document.Pages[0].Child).ToList();

        var details = blocks.Where(b => b.Text == Details).ToList();
        Assert.Equal(2, details.Count);

        var shortTitle = blocks.Single(b => b.Text == "Send colonoscopy data to doctor");
        Assert.Equal(Canvas.GetTop(shortTitle) + 3, Canvas.GetTop(details[0]));         // on the title's line
        Assert.True(Canvas.GetLeft(details[0]) > Canvas.GetLeft(shortTitle) + 200);     // over at the right

        var longFirstLine = blocks.First(b => b.Text.StartsWith("Send colonoscopy data to the doctor", StringComparison.Ordinal));
        Assert.True(Canvas.GetTop(details[1]) > Canvas.GetTop(longFirstLine) + 18);    // under a title too long to share
        Assert.Equal(Canvas.GetLeft(shortTitle), Canvas.GetLeft(details[1]));           // at the left, as before
    });

    [Fact]
    public void AFullRow_KeepsItsDetailsUnderTheTitle() => wpf.Run(() =>
    {
        var document = ReportService.BuildFixedDocument("Tasks", [Row("Call the bank", brief: false)], "None", false, false);
        var blocks = TextBlocks(document.Pages[0].Child).ToList();

        var title = blocks.Single(b => b.Text == "Call the bank");
        var details = blocks.Single(b => b.Text.StartsWith("Status: To Do", StringComparison.Ordinal));
        Assert.Equal(Canvas.GetLeft(title), Canvas.GetLeft(details));
        Assert.True(Canvas.GetTop(details) > Canvas.GetTop(title));
    });
}
