using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Task templates: what is kept from a task, and saving, replacing, renaming and deleting them.
[Collection(WpfCollection.Name)]
public sealed class TaskTemplateTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void ATemplateKeepsWhatIsReusable_AndTurnsDatesIntoDaysFromToday() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPerson("Sam Lee");
        board.AddFlag("Urgent");
        var card = board.AddCard("Monthly report", board.Columns.Single(c => c.Name == "In Progress"), board.Projects.First(), "High",
            DateTime.Today.AddDays(10), board.People.Single(), true, "Monthly", null, [board.Flags.Single()],
            [new SubTaskViewModel(new SubTaskItem { Title = "Pull the numbers", IsDone = true }), new SubTaskViewModel(new SubTaskItem { Title = "Send" })],
            "Use the new format", forceEditOnComplete: true, websiteUrl: "https://example.com", dueTime: "14:30",
            startDate: DateTime.Today.AddDays(7), waitingOn: "finance");

        var template = MainViewModel.TemplateFromCard(card);

        Assert.Equal("Monthly report", template.Title);
        Assert.Equal("High", template.Priority);
        Assert.Equal(card.ProjectId, template.ProjectId);
        Assert.Equal(card.WhoId, template.WhoId);
        Assert.Equal([board.Flags.Single().Id], template.FlagIds);
        Assert.Equal(["Pull the numbers", "Send"], template.SubTasks); // titles only - ticks are not kept
        Assert.Equal("Use the new format", template.Notes);
        Assert.True(template.IsRecurring);
        Assert.Equal("Monthly", template.RecurrencePattern);
        Assert.True(template.ForceEditOnComplete);
        Assert.Equal("https://example.com", template.WebsiteUrl);
        Assert.Equal("14:30", template.DueTime);
        Assert.Equal(10, template.DueInDays);
        Assert.Equal(7, template.StartInDays);
        Assert.Equal(DateTime.Today.AddDays(10), template.DueDateFromToday);
    });

    [Fact]
    public void DatesAlreadyPast_AreNotKept() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Old", board.Columns.First(), board.Projects.First(), "Normal", DateTime.Today.AddDays(-3), null, false, null, null,
            dueTime: "09:00", startDate: DateTime.Today.AddDays(-9));

        var template = MainViewModel.TemplateFromCard(card);

        Assert.Null(template.DueInDays);
        Assert.Null(template.StartInDays);
        Assert.Null(template.DueDateFromToday);
    });

    [Fact]
    public void Templates_AreSaved_ListedByName_AndSurviveReopening() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveTaskTemplate("Weekly review", new TaskTemplate { Title = "Review the week", SubTasks = ["Inbox", "Calendar"], DueInDays = 2 });
        board.SaveTaskTemplate("  client onboarding ", new TaskTemplate { Title = "Onboard" });

        Assert.Equal(["client onboarding", "Weekly review"], board.TaskTemplates.Select(t => t.Name));

        var reopened = OpenBoard().TaskTemplates.Single(t => t.Name == "Weekly review");
        Assert.Equal("Review the week", reopened.Title);
        Assert.Equal(["Inbox", "Calendar"], reopened.SubTasks);
        Assert.Equal(2, reopened.DueInDays);
    });

    [Fact]
    public void SavingUnderAnExistingName_ReplacesIt_WhateverTheCapitals() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveTaskTemplate("Weekly review", new TaskTemplate { Title = "First" });

        board.SaveTaskTemplate("WEEKLY REVIEW", new TaskTemplate { Title = "Second" });

        var only = Assert.Single(OpenBoard().TaskTemplates);
        Assert.Equal("Second", only.Title);
        Assert.Equal("WEEKLY REVIEW", only.Name);
    });

    [Fact]
    public void RenameAndDelete() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var a = board.SaveTaskTemplate("A", new TaskTemplate { Title = "a" });
        board.SaveTaskTemplate("B", new TaskTemplate { Title = "b" });

        Assert.False(board.RenameTaskTemplate(a, "b")); // taken
        Assert.False(board.RenameTaskTemplate(a, "   "));
        Assert.True(board.RenameTaskTemplate(a, "Alpha"));
        Assert.Equal(["Alpha", "B"], OpenBoard().TaskTemplates.Select(t => t.Name));
        Assert.Equal("a", OpenBoard().TaskTemplates.First().Title);

        board.DeleteTaskTemplate(board.TaskTemplates.Single(t => t.Name == "B"));
        Assert.Equal(["Alpha"], OpenBoard().TaskTemplates.Select(t => t.Name));
    });

    [Fact]
    public void ANewTaskFile_StartsWithNoTemplates() => wpf.Run(() =>
    {
        Assert.Empty(OpenBoard().TaskTemplates);
    });
}
