using System.Collections.ObjectModel;
using System.Text.Json;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Named, full Report Builder snapshots (see SavedReportView), persisted as one JSON array in the
// settings table - unbounded count, unlike the fixed ten Alt+0-9 CustomFilter slots.
public partial class MainViewModel
{
    private const string SavedReportViewsKey = "SavedReportViews";

    public ObservableCollection<SavedReportView> SavedReportViews { get; } = [];

    private void LoadSavedReportViews()
    {
        SavedReportViews.Clear();

        var json = _db.GetSetting(SavedReportViewsKey);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var views = JsonSerializer.Deserialize<List<SavedReportView>>(json);
            if (views is null) return;
            foreach (var view in views.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
            {
                SavedReportViews.Add(view);
            }
        }
        catch
        {
            // A corrupted setting shouldn't take the app down - treat it as no saved views.
        }
    }

    private void PersistSavedReportViews() =>
        _db.SetSetting(SavedReportViewsKey, JsonSerializer.Serialize(SavedReportViews.ToList()));

    // Overwrites an existing view of the same name, or adds a new one, keeping the list sorted.
    public void SaveReportView(SavedReportView view)
    {
        var existingIndex = -1;
        for (var i = 0; i < SavedReportViews.Count; i++)
        {
            if (string.Equals(SavedReportViews[i].Name, view.Name, StringComparison.OrdinalIgnoreCase)) { existingIndex = i; break; }
        }

        if (existingIndex >= 0)
        {
            SavedReportViews[existingIndex] = view;
        }
        else
        {
            var insertAt = 0;
            while (insertAt < SavedReportViews.Count &&
                   string.Compare(SavedReportViews[insertAt].Name, view.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertAt++;
            }
            SavedReportViews.Insert(insertAt, view);
        }

        PersistSavedReportViews();
    }

    public void DeleteReportView(string name)
    {
        var existing = SavedReportViews.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return;

        SavedReportViews.Remove(existing);
        PersistSavedReportViews();
    }
}
