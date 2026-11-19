using System.IO;
using System.Windows;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Each task chooses which of the offered custom fields apply to it (a new task starts with none): a field that applies with nothing in it,
// what a repeating task's next occurrence keeps, the fields a project or a template starts a task with, and Mass Update.
[Collection(WpfCollection.Name)]
public sealed class CustomFieldPerTaskTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static Dictionary<int, string> Values(params (int Slot, string Value)[] values) => values.ToDictionary(v => v.Slot, v => v.Value);

    private static CardViewModel AddTask(MainViewModel board, string title, IReadOnlyDictionary<int, string>? values = null, bool recurring = false) =>
        board.AddCard(title, board.ToDoColumn!, board.Projects.First(), "Normal", recurring ? DateTime.Today : null, null, recurring, recurring ? "Daily" : null, null, customValues: values);

    private MainViewModel BoardWithFields()
    {
        var board = OpenBoard();
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);
        board.SetCustomField(2, "Hours spent", CustomFieldType.Number, true, keepOnRepeat: false);
        board.SetCustomField(3, "Review date", CustomFieldType.Date, true);
        return board;
    }

    // The tests' application has none of App.xaml's styles, which a window needs (as SimplifiedTaskWindowTests does).
    private static void EnsureAppResources()
    {
        if (Application.Current.Resources.Contains("HoverTextBoxStyle")) return;

        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, typeof(MainViewModel).Assembly);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "KanbanApp", "App.xaml"))) dir = dir.Parent;
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "src", "KanbanApp", "App.xaml"));
        var from = source.IndexOf("<Application.Resources>", StringComparison.Ordinal) + "<Application.Resources>".Length;
        var inside = source[from..source.IndexOf("</Application.Resources>", StringComparison.Ordinal)];
        var assembly = typeof(MainViewModel).Assembly.GetName().Name;
        var text = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" "
            + $"xmlns:local=\"clr-namespace:KanbanApp;assembly={assembly}\" xmlns:views=\"clr-namespace:KanbanApp.Views;assembly={assembly}\">{inside}</ResourceDictionary>";
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(text));
    }

    // ---------- a field that applies, with or without a value

    [Fact]
    public void AFieldThatAppliesWithNothingInIt_IsKept_AndDoesNotLockTheFieldsType() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        AddTask(board, "Quote", Values((1, "")));

        var loaded = OpenBoard().Columns.SelectMany(c => c.Cards).Single();
        Assert.True(loaded.CustomValues.ContainsKey(1));
        Assert.Equal(string.Empty, loaded.CustomValues[1]);
        Assert.Equal(0, board.CustomFieldUseCount(1));                      // it holds no value
        Assert.Null(board.SetCustomField(1, "Client", CustomFieldType.Date, true)); // so the type can still change
    });

    // ---------- repeating tasks

    [Fact]
    public void TheNextOccurrence_KeepsTheFields_ButOnlyTheValuesOfTheFieldsKeptOnRepeat() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        var card = AddTask(board, "Weekly review", Values((1, "Dupont"), (2, "3.5")), recurring: true);

        board.MoveCards([card], board.DoneColumn!); // finishing a repeating task makes the next one

        var next = board.ToDoColumn!.Cards.Single(c => c != card);
        Assert.Equal(Values((1, "Dupont"), (2, "")), next.CustomValues); // Hours spent is there, and empty
    });

    [Fact]
    public void KeepOnRepeat_IsSavedWithTheField_AndAFieldWithNoSayDefaultsToKept() => wpf.Run(() =>
    {
        var board = BoardWithFields();

        var reopened = OpenBoard();
        Assert.True(reopened.CustomFieldDefs[0].KeepOnRepeat);
        Assert.False(reopened.CustomFieldDefs[1].KeepOnRepeat);
        Assert.True(CustomFieldRules.LoadDefinitions("""[{"Slot":1,"Name":"Client","Type":"Text","IsActive":true}]""")[0].KeepOnRepeat);

        var rule = CustomFieldRules.ForNextOccurrence(Values((1, "a"), (2, "b"), (9, "c")), board.CustomFieldDefs);
        Assert.Equal(Values((1, "a"), (2, ""), (9, "c")), rule); // a field with no definition is kept as it was
    });

    // ---------- a project's fields, and a template's

    [Fact]
    public void AProjectCanListTheFieldsANewTaskInItStartsWith() => wpf.Run(() =>
    {
        EnsureAppResources();
        var board = BoardWithFields();
        foreach (var project in board.Projects) board.SetDefaultFields(project.Id, [1, 3]);

        Assert.Equal(["Client", "Review date"], board.DefaultFieldsOf(board.Projects.First().Id).Select(f => f.Name));
        Assert.Equal([1, 3], OpenBoard().DefaultFieldSlots(board.Projects.First().Id)); // kept in the file

        var added = new AddTaskWindow(board);
        Assert.Empty(added.CustomFieldsPanel.Children);                        // no project chosen yet
        added.ProjectComboBox.SelectedItem = added.ProjectComboBox.Items[0];   // choosing one brings its fields
        Assert.NotNull(added.CustomControlOf(1));
        Assert.NotNull(added.CustomControlOf(3));
        Assert.Null(added.CustomControlOf(2));

        var task = AddTask(board, "Plain");                                   // an existing task keeps the fields it has
        var edit = new AddTaskWindow(board, task, board.ToDoColumn!);
        Assert.Empty(edit.CustomFieldsPanel.Children);

        board.SetCustomField(3, "Review date", CustomFieldType.Date, false);  // one no longer offered is not started with
        Assert.Equal(["Client"], board.DefaultFieldsOf(board.Projects.First().Id).Select(f => f.Name));

        foreach (var project in board.Projects) board.SetDefaultFields(project.Id, []);
        Assert.Empty(board.DefaultFieldsOf(board.Projects.First().Id));
        Assert.Empty(OpenBoard().DefaultFieldSlots(board.Projects.First().Id));
    });

    [Fact]
    public void ATemplateCarriesTheFieldsThatApplied_AndANewTaskFromItStartsWithThem() => wpf.Run(() =>
    {
        EnsureAppResources();
        var board = BoardWithFields();
        var card = AddTask(board, "Monthly invoice", Values((1, "Dupont"), (3, "")));

        var template = board.SaveTaskTemplate("Invoice", MainViewModel.TemplateFromCard(card));

        Assert.Equal(Values((1, "Dupont"), (3, "")), OpenBoard().FindTaskTemplate("Invoice")!.CustomValues); // kept in the file

        var window = new AddTaskWindow(board);
        window.StartFromTemplate(template);
        Assert.Equal("Dupont", ((System.Windows.Controls.TextBox)window.CustomControlOf(1)!).Text);
        Assert.NotNull(window.CustomControlOf(3));
        Assert.Null(window.CustomControlOf(2));
    });

    // ---------- Mass Update

    [Fact]
    public void MassUpdate_AppliesAFieldToTasksOrTakesItOff_InOneUndoStep() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        var field = board.CustomFieldDefs[0];
        var plain = AddTask(board, "Plain");
        var holding = AddTask(board, "Holding", Values((1, "Dupont")));

        board.ApplyMassUpdate([plain, holding], new MassUpdateChanges { CustomFieldAction = CustomFieldChange.Add, CustomField = field });
        Assert.Equal(Values((1, "")), plain.CustomValues);          // applied, empty
        Assert.Equal(Values((1, "Dupont")), holding.CustomValues);  // what it held is kept

        board.ApplyMassUpdate([plain, holding], new MassUpdateChanges { CustomFieldAction = CustomFieldChange.Add, CustomField = field, CustomValue = "Tremblay" });
        Assert.Equal("Tremblay", plain.CustomValues[1]);
        Assert.Equal("Tremblay", holding.CustomValues[1]);

        board.Undo();
        Assert.Equal(Values((1, "")), plain.CustomValues);
        Assert.Equal("Dupont", holding.CustomValues[1]);

        board.ApplyMassUpdate([plain, holding], new MassUpdateChanges { CustomFieldAction = CustomFieldChange.Remove, CustomField = field });
        Assert.Empty(plain.CustomValues);
        Assert.Empty(holding.CustomValues);
        Assert.Empty(OpenBoard().Columns.SelectMany(c => c.Cards).SelectMany(c => c.CustomValues));
    });

    [Fact]
    public void MassUpdate_SaysWhatItWillDo()
    {
        var field = new CustomFieldDef { Slot = 1, Name = "Client", Type = CustomFieldType.Text, IsActive = true };

        Assert.Equal(["Add the custom field Client"], new MassUpdateChanges { CustomFieldAction = CustomFieldChange.Add, CustomField = field }.Lines);
        Assert.Equal(["Add the custom field Client: Dupont"], new MassUpdateChanges { CustomFieldAction = CustomFieldChange.Add, CustomField = field, CustomValue = "Dupont" }.Lines);
        Assert.Equal(["Take the custom field Client off"], new MassUpdateChanges { CustomFieldAction = CustomFieldChange.Remove, CustomField = field }.Lines);
        Assert.True(new MassUpdateChanges().IsEmpty);
    }
}
