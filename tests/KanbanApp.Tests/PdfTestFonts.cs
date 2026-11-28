using System.IO;
using System.Runtime.CompilerServices;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// PDFsharp takes its report fonts once per run, from whichever PDF is made first, so the font every PDF in this
// run uses would depend on which test class happened to run first (PdfFontTests saw Segoe UI whenever
// SubTaskReportTests went before it). Pointing the font folder at one that doesn't exist before any test runs
// means every PDF made by the tests uses the bundled font, whatever the order.
internal static class PdfTestFonts
{
    internal static readonly string NoFontsFolder = Path.Combine(Path.GetTempPath(), "KanbanApp.Tests-no-fonts-" + Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void UseTheBundledFont() => PdfFontResolver.FontsFolderOverride = NoFontsFolder;
}
