using System.Text.Json;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The Report Builder's title search: only tasks whose title contains the text, on top of every other filter, on the
// board and in the archive, in task lists and in statistics.
[Collection(WpfCollection.Name)]
public sealed class ReportTitleSearchTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static List<ReportRow> Rows(MainViewModel board, string? search, ReportArchiveScope scope = ReportArchiveScope.BoardOnly,
        List<CustomFilter>? union = null) =>
        ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(), [], [], [], "All", "All", "All", null, null, false,
            union, "None", "None", "None", scope, scope == ReportArchiveScope.BoardOnly ? null : board.GetArchivedReportRows(), titleSearch: search);

    private static void Add(MainViewModel board, string title) =>
        board.AddCard(title, board.ToDoColumn!, null, "Normal", null, null, false, null, null);

    [Fact]
    public void OnlyTasksWhoseTitleContainsTheSearch_AreInTheReport_WhateverTheCapitalsOrSpacesRoundIt() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Add(board, "Pay Hydro bill");
        Add(board, "Call plumber");
        Add(board, "hydro meter reading");

        Assert.Equal(["Hydro meter reading".ToLowerInvariant(), "pay hydro bill"], Rows(board, "  HYDRO ").Select(r => r.Title.ToLowerInvariant()).Order());
        Assert.Equal(3, Rows(board, null).Count);
        Assert.Equal(3, Rows(board, "   ").Count); // blank is no search
        Assert.Empty(Rows(board, "furnace"));
    });

    [Fact]
    public void TheSearch_AlsoNarrowsArchivedTasks() => wpf.Run(() =>
    {
        var path = _temp.File("board.db");
        var db = new DatabaseService(path);
        var first = new MainViewModel(db);
        Add(first, "Old hydro bill");
        Add(first, "Old plumber visit");
        foreach (var card in first.ToDoColumn!.Cards.ToList()) db.ArchiveCard(card.Id, card.Title, "To Do");

        var board = new MainViewModel(new DatabaseService(path));
        Assert.Equal(2, Rows(board, null, ReportArchiveScope.ArchivedOnly).Count);
        Assert.Equal(["Old hydro bill"], Rows(board, "hydro", ReportArchiveScope.ArchivedOnly).Select(r => r.Title));
    });

    [Fact]
    public void Statistics_CountOnlyTheTasksTheSearchPicks() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Add(board, "Pay Hydro bill");
        Add(board, "Hydro meter reading");
        Add(board, "Call plumber");

        var all = ReportRunner.BuildStatistics(board, new SavedReportView { ReportType = SavedReportView.StatisticsType }, DateTime.Today);
        var searched = ReportRunner.BuildStatistics(board, new SavedReportView { ReportType = SavedReportView.StatisticsType, TitleSearch = "hydro" }, DateTime.Today);

        Assert.Equal(3, all.TaskCount);
        Assert.Equal(2, searched.TaskCount);
    });

    [Fact]
    public void ASavedView_KeepsTheSearch_AnOlderOneHasNone_AndTheParametersLineSaysWhatWasSearched()
    {
        var view = new SavedReportView { TitleSearch = "hydro" };
        var again = JsonSerializer.Deserialize<SavedReportView>(JsonSerializer.Serialize(view))!;
        Assert.Equal("hydro", again.TitleSearch);

        var old = JsonSerializer.Deserialize<SavedReportView>("""{"Name":"Old"}""")!;
        Assert.Equal(string.Empty, old.TitleSearch);
        Assert.DoesNotContain("Title contains", ReportRunner.Summarise(old, DateTime.Today));

        Assert.Contains("Title contains: \"hydro\"", ReportRunner.Summarise(again, DateTime.Today));
        again.ReportType = SavedReportView.StatisticsType;
        Assert.Contains("Title contains: \"hydro\"", ReportRunner.Summarise(again, DateTime.Today));
    }
}
