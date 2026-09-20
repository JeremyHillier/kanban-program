using System.IO;
using System.Text;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The PDF report's fonts: Segoe UI from Windows when it's there, the bundled Lato when it isn't.
public sealed class PdfFontTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AllFourBundledFaces_AreInsideTheApp_AndAreRealFontFiles()
    {
        var fonts = PdfFontResolver.LoadBundled();

        Assert.Equal(["Bold", "BoldItalic", "Italic", "Regular"], fonts.Keys.Order());
        Assert.All(fonts.Values, bytes =>
        {
            Assert.True(bytes.Length > 100_000);
            Assert.Equal(new byte[] { 0, 1, 0, 0 }, bytes[..4]); // the TrueType signature
        });
    }

    [Fact]
    public void AFolderWithoutSegoeUi_OrWithOnlySomeOfIt_MeansTheBundledFont()
    {
        var folder = _temp.Path;
        Assert.True(PdfFontResolver.UsesBundledFont(folder));

        File.WriteAllBytes(Path.Combine(folder, "segoeui.ttf"), [1, 2, 3]); // the other three are still missing
        Assert.True(PdfFontResolver.UsesBundledFont(folder));

        Assert.True(PdfFontResolver.UsesBundledFont(Path.Combine(folder, "no-such-folder")));
    }

    // The only test that makes a PDF: PDFsharp takes its fonts once per run, so a second one
    // wanting Segoe UI could not be told apart from this one.
    [Fact]
    public void WithNoSegoeUi_AReportIsStillMade_InTheBundledFont()
    {
        PdfFontResolver.FontsFolderOverride = _temp.Path;
        var path = _temp.File("report.pdf");
        var rows = new List<ReportRow>
        {
            new()
            {
                Title = "Send the contract", ColumnName = "To Do", ProjectName = "Legal", Priority = "High", GoalName = "",
                DueDate = new DateTime(2026, 10, 9), WaitingOn = "Sam", Notes = "Use the new wording",
                SubTasks = [("Draft", true), ("Sign", false)]
            }
        };

        ReportService.SavePdf("Task Report", rows, "Project", includeNotes: true, includeSubTasks: true, path);

        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert.StartsWith("%PDF", pdf);
        Assert.Contains("Lato", pdf);
        Assert.DoesNotContain("Segoe", pdf);
    }
}
