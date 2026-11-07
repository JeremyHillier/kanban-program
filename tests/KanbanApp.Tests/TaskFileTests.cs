using System.IO;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The catalogue of task files on this PC (AppConfig): registering, switching, the startup file, and
// the migration of the old recent list. Nothing here touches the real config file.
public sealed class TaskFileCatalogueTests
{
    [Fact]
    public void SwitchTo_RegistersTheFile_AndTheOpenOneLeadsTheList()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };
        config.Normalise();

        config.SwitchTo(@"C:\files\b.db");
        config.SwitchTo(@"C:\files\c.db");

        Assert.Equal(@"C:\files\c.db", config.DbPath);
        Assert.Equal([@"C:\files\c.db", @"C:\files\b.db", @"C:\files\a.db"], config.FilesByRecency.Select(f => f.Path));
        Assert.Equal("c", config.Current.DisplayName);
    }

    [Fact]
    public void AFile_IsRegisteredOnce_WhateverTheCapitals_AndANameAndNoteStick()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };
        config.Normalise();

        config.Register(@"C:\files\Work.db", "Work", "Client work");
        config.Register(@"c:\FILES\work.db");
        config.SwitchTo(@"c:\FILES\work.db");

        Assert.Equal(2, config.Files.Count);
        Assert.Equal(("Work", "Client work"), (config.Current.DisplayName, config.Current.Description));
    }

    [Fact]
    public void TheStartupFile_IsTheChosenOne_WhileItIsOnTheList_ElseTheLastOpen()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };
        config.Normalise();
        config.Register(@"C:\files\home.db", "Home");

        Assert.Equal(@"C:\files\a.db", config.FileToOpenAtStartup);
        config.StartupDbPath = @"C:\files\home.db";
        Assert.Equal(@"C:\files\home.db", config.FileToOpenAtStartup);

        config.Remove(@"C:\files\home.db");
        Assert.Null(config.StartupDbPath);
        Assert.Equal(@"C:\files\a.db", config.FileToOpenAtStartup);
    }

    [Fact]
    public void TheOpenFile_CannotBeRemoved_AndRepointFollowsAMove()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };
        config.Normalise();
        config.StartupDbPath = @"C:\files\a.db";

        Assert.False(config.Remove(@"C:\files\a.db"));
        config.Repoint(@"C:\files\a.db", @"D:\moved\a.db");

        Assert.Equal(@"D:\moved\a.db", config.DbPath);
        Assert.Equal(@"D:\moved\a.db", config.StartupDbPath);
        Assert.Single(config.Files);
    }

    [Fact]
    public void AnOlderConfig_RecentList_BecomesEntries()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db", RecentDbPaths = [@"C:\files\b.db", @"C:\files\c.db"] };

        config.Normalise();

        Assert.Null(config.RecentDbPaths);
        Assert.Equal(3, config.Files.Count);
        Assert.NotNull(config.Entry(@"C:\files\c.db"));
        Assert.NotNull(config.Current.LastOpened);
    }
}

// Reading a file before it is opened for real, the identity each file carries, and whole-file copies.
[Collection(WpfCollection.Name)]
public sealed class TaskFileIdentityTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ATaskFile_IsRecognised_WithItsFormatIdentityAndTaskCount() => wpf.Run(() =>
    {
        var path = _temp.File("board.db");
        var board = new MainViewModel(new DatabaseService(path));
        board.AddCard("Paint the fence", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);

        var info = TaskFileInspector.Inspect(path);

        Assert.True(info.IsTaskFile, info.Problem);
        Assert.False(info.IsFromNewerApp);
        Assert.False(string.IsNullOrEmpty(info.FileId));
        Assert.Equal(1, info.OpenTaskCount);
        Assert.False(info.IsTemplate);
    });

    [Fact]
    public void NotATaskFile_SaysSo_AndANewerFileIsFlagged()
    {
        File.WriteAllText(_temp.File("notes.txt"), "hello");
        Assert.False(TaskFileInspector.Inspect(_temp.File("notes.txt")).IsTaskFile);
        Assert.False(TaskFileInspector.Inspect(_temp.File("missing.db")).IsTaskFile);

        var db = new DatabaseService(_temp.File("future.db"));
        db.SetSetting("FileFormat", "999");
        Assert.True(TaskFileInspector.Inspect(_temp.File("future.db")).IsFromNewerApp);
    }

    [Fact]
    public void ADuplicate_HasTheSameTasks_ButItsOwnIdentity() => wpf.Run(() =>
    {
        var source = _temp.File("a\\board.db");
        var board = new MainViewModel(new DatabaseService(source));
        board.AddCard("Paint the fence", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        var sourceId = TaskFileInspector.Inspect(source).FileId;

        var copy = _temp.File("b\\board.db");
        TaskFileService.DuplicateAsNewFile(source, copy);

        var info = TaskFileInspector.Inspect(copy);
        Assert.Equal(1, info.OpenTaskCount);
        Assert.NotEqual(sourceId, info.FileId);
        Assert.Equal(sourceId, TaskFileInspector.Inspect(source).FileId); // the original keeps its own
    });

    [Fact]
    public void APlainCopy_KeepsTheIdentity_SoAMovedFileIsKnownAgain() => wpf.Run(() =>
    {
        var source = _temp.File("a\\board.db");
        _ = new DatabaseService(source);
        var copy = _temp.File("b\\board.db");

        TaskFileService.CopyTaskFile(source, copy);

        Assert.Equal(TaskFileInspector.Inspect(source).FileId, TaskFileInspector.Inspect(copy).FileId);
    });

    [Fact]
    public void TheBoard_NamesItsFile() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("Client Work.db")));
        Assert.Equal("Client Work", board.TaskFileName);
        Assert.EndsWith(" - Client Work", board.WindowTitle);
    });
}

// Task-file templates: a file saved with only the chosen parts, and new files made from it.
[Collection(WpfCollection.Name)]
public sealed class TaskFileTemplateTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string ABoardWithEverything()
    {
        var path = _temp.File("source\\board.db");
        var board = new MainViewModel(new DatabaseService(path));
        var project = board.AddProject("Garden").Item!;
        var person = board.AddPerson("Sam").Item!;
        var todo = board.Columns.First(c => c.Name == "To Do");
        var done = board.DoneColumn!;
        board.AddCard("Paint the fence", todo, project, "Normal", null, person, false, null, null);
        board.AddCard("Buy paint", done, project, "Normal", null, null, false, null, null);
        return path;
    }

    [Fact]
    public void ATemplateWithoutTasks_KeepsTheLists_AndANewFileFromIt_IsItsOwn() => wpf.Run(() =>
    {
        var source = ABoardWithEverything();
        var folder = _temp.File("templates");
        var templatePath = TaskFileTemplates.PathFor(folder, "Garden jobs");

        var template = TaskFileTemplates.Save(source, templatePath, "Garden jobs", "The usual set-up", TemplateParts.Default);

        Assert.Equal(0, template.OpenTaskCount);
        var listed = Assert.Single(TaskFileTemplates.List(folder));
        Assert.Equal(("Garden jobs", "The usual set-up"), (listed.Name, listed.Description));

        var newPath = _temp.File("new\\garden.db");
        TaskFileTemplates.CreateTaskFile(templatePath, newPath);
        var board = new MainViewModel(new DatabaseService(newPath));
        Assert.Contains(board.Projects, p => p.Name == "Garden");
        Assert.Contains(board.People, p => p.Name == "Sam");
        Assert.Empty(board.Columns.SelectMany(c => c.Cards));
        var info = TaskFileInspector.Inspect(newPath);
        Assert.False(info.IsTemplate);
        Assert.NotEqual(TaskFileInspector.Inspect(source).FileId, info.FileId);
    });

    [Fact]
    public void ATemplateWithTasks_KeepsOnlyOpenOnes_AndLeavesOutWhatWasNotTicked() => wpf.Run(() =>
    {
        var source = ABoardWithEverything();
        var templatePath = TaskFileTemplates.PathFor(_temp.File("templates"), "With tasks");

        TaskFileTemplates.Save(source, templatePath, "With tasks", null, new TemplateParts(OpenTasks: true, People: false));

        var newPath = _temp.File("new\\board.db");
        TaskFileTemplates.CreateTaskFile(templatePath, newPath);
        var board = new MainViewModel(new DatabaseService(newPath));
        var card = Assert.Single(board.Columns.SelectMany(c => c.Cards));
        Assert.Equal("Paint the fence", card.Title);
        Assert.Equal("Garden", card.ProjectName);
        Assert.Empty(board.People);
        Assert.Null(card.WhoId);
        Assert.Equal(new TemplateParts(OpenTasks: true, People: false), TaskFileTemplates.PartsOf(templatePath));
    });
}
