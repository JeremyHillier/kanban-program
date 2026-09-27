using System.IO;
using System.Reflection;

namespace KanbanApp.Services;

// The licence agreement and privacy note, carried inside the exe (Legal/*.txt, embedded by
// KanbanApp.csproj) so the About screen can show them on any installation. The installer shows the
// same EULA.txt on its licence page. Both name AppInfo.Company and AppInfo.SupportEmail in their
// text - LegalDocumentTests fails if those drift apart.
public static class LegalDocuments
{
    public static string Eula => Read("EULA.txt");
    public static string Privacy => Read("PRIVACY.txt");

    private static string Read(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Legal." + fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
