using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace KanbanApp.Services;

public record ReleaseNote(string Version, string Date, List<string> Items);

// Reads the release notes shown by the What's New screen straight out of the CHANGELOG.md embedded
// in the exe (see the Resource include in KanbanApp.csproj), so there's never a second hand-kept
// copy of the same notes to drift out of sync with the real changelog.
//
// The same layout as the Personal Finance and Accounting programs (from 0.116.5): each version's
// summary is its "> - " lines, and only those reach What's New; the "- " lines under them are the
// detail. Versions written before then have no summary, so their "- " lines are shown instead.
public static partial class ReleaseNotes
{
    // Matches a version heading: "## 0.67.3 — 2026-09-01". The separator is an em dash in practice,
    // but a plain hyphen is accepted too so a hand-typed entry still parses.
    [GeneratedRegex(@"^##\s+(?<version>\S+)\s*[—–-]\s*(?<date>.+?)\s*$")]
    private static partial Regex VersionHeading();

    public static List<ReleaseNote> Load(int maxVersions = 5) => ReadChangelog() is { } text ? Parse(text, maxVersions) : [];

    internal static List<ReleaseNote> Parse(string text, int maxVersions = 5)
    {
        var notes = new List<ReleaseNote>();
        ReleaseNote? current = null;
        List<string> summary = [], detail = [];

        void Finish()
        {
            if (current is null) return;
            current.Items.AddRange(summary.Count > 0 ? summary : detail);
            summary = [];
            detail = [];
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();

            var heading = VersionHeading().Match(line);
            if (heading.Success)
            {
                Finish();
                if (notes.Count == maxVersions) return notes;
                current = new ReleaseNote(heading.Groups["version"].Value, heading.Groups["date"].Value, []);
                notes.Add(current);
                continue;
            }

            // Bullets before the first heading belong to the file's intro blurb, not a release.
            if (current is null) continue;
            if (line.StartsWith("> - ")) summary.Add(line[4..].Trim());
            else if (line.StartsWith("- ")) detail.Add(line[2..].Trim());
        }

        Finish();
        return notes;
    }

    private static string? ReadChangelog()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("CHANGELOG.md", UriKind.Relative));
            if (resource is null) return null;

            using var reader = new StreamReader(resource.Stream);
            return reader.ReadToEnd();
        }
        catch
        {
            // Release notes are a nicety - never let a missing or unreadable resource break startup.
            return null;
        }
    }
}
