using System.Text.Json.Serialization;

namespace KanbanApp.Models;

// One Alt+0..Alt+9 filter slot: a named snapshot of every board filter, stored as JSON in the
// settings table. An empty Name means the slot is unused.
public class CustomFilter
{
    public string Name { get; set; } = string.Empty;
    public string Project { get; set; } = "All";
    public string Priority { get; set; } = "All";
    public string Who { get; set; } = "All";
    public string Goal { get; set; } = "All";
    public string Flag { get; set; } = "All";
    public string Due { get; set; } = "All";

    // Stored as yyyy-MM-dd strings rather than DateTime so the persisted JSON stays culture-proof.
    public string? DueFrom { get; set; }
    public string? DueTo { get; set; }

    public string Keyword { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsDefined => !string.IsNullOrWhiteSpace(Name);

    // Plain-English description of what the slot actually narrows by, for the manage window.
    [JsonIgnore]
    public string Summary
    {
        get
        {
            if (!IsDefined) return "Empty - not assigned";

            var parts = new List<string>();
            if (Project != "All") parts.Add($"Project: {Project}");
            if (Priority != "All") parts.Add($"Priority: {Priority}");
            if (Who != "All") parts.Add($"Who: {Who}");
            if (Goal != "All") parts.Add($"Goal: {Goal}");
            if (Flag != "All") parts.Add($"Flag: {Flag}");
            if (Due != "All") parts.Add($"Due: {Due}");
            if (!string.IsNullOrEmpty(DueFrom)) parts.Add($"From: {DueFrom}");
            if (!string.IsNullOrEmpty(DueTo)) parts.Add($"To: {DueTo}");
            if (!string.IsNullOrWhiteSpace(Keyword)) parts.Add($"Keyword: \"{Keyword}\"");

            return parts.Count == 0 ? "Everything (no filters applied)" : string.Join("   •   ", parts);
        }
    }
}
