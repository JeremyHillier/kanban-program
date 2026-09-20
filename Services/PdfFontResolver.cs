using System.IO;
using System.Reflection;
using PdfSharp.Fonts;

namespace KanbanApp.Services;

// Gives PDFsharp the font files for a PDF report. Segoe UI is used when Windows has it, so the PDF
// matches the on-screen preview. Segoe UI can't be shipped with the app (it's Microsoft's), so
// when any of its files is missing or can't be read, the report uses Lato instead - a free font
// (SIL Open Font License, Assets/Fonts/Lato-OFL.txt) carried inside the exe. It is all one or all
// the other: a report never mixes the two.
internal sealed class PdfFontResolver : IFontResolver
{
    public const string FamilyName = "Report";

    private static readonly string[] Faces = ["Regular", "Bold", "Italic", "BoldItalic"];

    // Tests point this at an empty folder to get the bundled font. Read once, on the first report.
    internal static string? FontsFolderOverride { get; set; }

    private readonly Lazy<Dictionary<string, byte[]>> _fonts = new(Load);

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new((isBold, isItalic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular"
        });

    public byte[] GetFont(string faceName) => _fonts.Value.TryGetValue(faceName, out var font) ? font : _fonts.Value["Regular"];

    internal static bool UsesBundledFont(string fontsFolder) => LoadSegoeUi(fontsFolder) is null;

    private static Dictionary<string, byte[]> Load() =>
        LoadSegoeUi(FontsFolderOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.Fonts)) ?? LoadBundled();

    private static Dictionary<string, byte[]>? LoadSegoeUi(string folder)
    {
        try
        {
            var files = new Dictionary<string, string>
            {
                ["Regular"] = "segoeui.ttf", ["Bold"] = "segoeuib.ttf", ["Italic"] = "segoeuii.ttf", ["BoldItalic"] = "segoeuiz.ttf"
            };
            var fonts = files.ToDictionary(f => f.Key, f => File.ReadAllBytes(Path.Combine(folder, f.Value)));
            return fonts.Values.All(bytes => bytes.Length > 0) ? fonts : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static Dictionary<string, byte[]> LoadBundled() => Faces.ToDictionary(face => face, face => ReadResource($"Lato-{face}.ttf"));

    private static byte[] ReadResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
