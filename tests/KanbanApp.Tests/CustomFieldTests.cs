using System.Globalization;
using System.IO;
using System.Windows;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Custom fields: the rules for what a value must look like, the definitions the user makes, what a task holds, how it is kept
// (saved, undone, copied, repeated), what the task window shows - and that older files and older callers lose nothing.
[Collection(WpfCollection.Name)]
public sealed class CustomFieldTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string file = "board.db") => new(new DatabaseService(_temp.File(file)));

    private static Dictionary<int, string> Values(params (int Slot, string Value)[] values) => values.ToDictionary(v => v.Slot, v => v.Value);

    private static CardViewModel AddTask(MainViewModel board, string title, IReadOnlyDictionary<int, string>? values = null, bool recurring = false) =>
        board.AddCard(title, board.ToDoColumn!, board.Projects.First(), "Normal", recurring ? DateTime.Today : null, null, recurring, recurring ? "Daily" : null, null, customValues: values);

    // ---------- the rules

    [Theory]
    [InlineData(CustomFieldType.Number, " 12.50 ", "12.5")]
    [InlineData(CustomFieldType.Number, "-3", "-3")]
    [InlineData(CustomFieldType.Number, "", null)]
    [InlineData(CustomFieldType.Date, "2026-12-01", "2026-12-01")]
    [InlineData(CustomFieldType.YesNo, "yes", "Yes")]
    [InlineData(CustomFieldType.YesNo, "no", null)]
    [InlineData(CustomFieldType.Text, "  Dupont  ", "Dupont")]
    public void WhatIsTyped_IsStoredTheSameWayOnEveryPc(CustomFieldType type, string input, string? expected)
    {
        Assert.True(CustomFieldRules.TryNormalise(type, input, out var stored));
        Assert.Equal(expected, stored);
    }

    [Theory]
    [InlineData("twelve")]
    [InlineData("1,000")]
    [InlineData("12 hours")]
    public void ANumberThatIsNotOne_IsRefused(string input) =>
        Assert.False(CustomFieldRules.TryNormalise(CustomFieldType.Number, input, out _));

    [Fact]
    public void Values_ReadAndWriteAsText_AndDamagedTextReadsAsNone()
    {
        var text = CustomFieldRules.SerializeValues(Values((3, "2026-12-01"), (1, "Dupont"), (2, "  "), (11, "out of range")));
        Assert.Equal("""{"1":"Dupont","2":"","3":"2026-12-01"}""", text); // a field that applies with nothing in it is kept
        Assert.Equal(Values((1, "Dupont"), (2, ""), (3, "2026-12-01")), CustomFieldRules.ParseValues(text));

        Assert.Null(CustomFieldRules.SerializeValues(new Dictionary<int, string>()));
        Assert.Empty(CustomFieldRules.ParseValues("not json"));
        Assert.Empty(CustomFieldRules.ParseValues(null));
    }

    [Fact]
    public void Definitions_AreAlwaysTenSlots_WhateverIsStored()
    {
        Assert.Equal(Enumerable.Range(1, 10), CustomFieldRules.LoadDefinitions(null).Select(f => f.Slot));
        Assert.Equal(10, CustomFieldRules.LoadDefinitions("damaged").Count);

        var loaded = CustomFieldRules.LoadDefinitions("""[{"Slot":4,"Name":"Billable","Type":"YesNo","IsActive":true}]""");
        Assert.Equal(("Billable", CustomFieldType.YesNo, true), (loaded[3].Name, loaded[3].Type, loaded[3].IsActive));
        Assert.False(loaded[0].IsDefined);
    }

    [Fact]
    public void ValuesReadOnScreen_FollowTheType()
    {
        Assert.Equal("12.5", CustomFieldRules.Display(CustomFieldType.Number, "12.5").Replace(',', '.'));
        Assert.Equal(new DateTime(2026, 12, 1).ToString("MMM d, yyyy", CultureInfo.CurrentCulture), CustomFieldRules.Display(CustomFieldType.Date, "2026-12-01"));
        Assert.Equal("Yes", CustomFieldRules.Display(CustomFieldType.YesNo, "Yes"));
        Assert.Equal(string.Empty, CustomFieldRules.Display(CustomFieldType.Text, null));
    }

    // ---------- the definitions

    [Fact]
    public void AFieldThatIsNamedAndOn_IsActive_AndTheDefinitionsAreKeptInTheFile() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Empty(board.ActiveCustomFields);

        Assert.Null(board.SetCustomField(1, "Client", CustomFieldType.Text, true));
        Assert.Null(board.SetCustomField(2, "Estimate", CustomFieldType.Number, true));
        Assert.Null(board.SetCustomField(3, "Review date", CustomFieldType.Date, false));

        Assert.Equal(["Client", "Estimate"], board.ActiveCustomFields.Select(f => f.Name));

        var again = OpenBoard();
        Assert.Equal(["Client", "Estimate"], again.ActiveCustomFields.Select(f => f.Name));
        Assert.Equal(("Review date", CustomFieldType.Date, false), (again.CustomFieldDefs[2].Name, again.CustomFieldDefs[2].Type, again.CustomFieldDefs[2].IsActive));
        Assert.Equal(10, again.CustomFieldDefs.Count);
    });

    [Fact]
    public void ANameIsNeededToSwitchOn_AndCannotRepeatOrTakeATasksOwnHeading() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);

        Assert.NotNull(board.SetCustomField(2, "", CustomFieldType.Text, true));
        Assert.NotNull(board.SetCustomField(2, "client", CustomFieldType.Text, false));      // another field's name, any capitals
        Assert.NotNull(board.SetCustomField(2, "Notes", CustomFieldType.Text, false));       // a heading in the task's Excel files
        Assert.NotNull(board.SetCustomField(2, "Description", CustomFieldType.Text, false)); // another name Excel files read as Notes
        Assert.NotNull(board.SetCustomField(2, new string('x', CustomFieldRules.MaxNameLength + 1), CustomFieldType.Text, false));
        Assert.Single(board.CustomFieldDefs, f => f.IsDefined);
    });

    [Fact]
    public void AFieldsTypeIsFixed_OnceATaskHoldsAValue_ButItCanBeRenamedAndSwitchedOff() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetCustomField(1, "Estimate", CustomFieldType.Number, true);
        Assert.Null(board.SetCustomField(1, "Estimate", CustomFieldType.Text, true)); // nothing holds a value yet

        AddTask(board, "Quote", Values((1, "12")));

        Assert.Equal(1, board.CustomFieldUseCount(1));
        Assert.NotNull(board.SetCustomField(1, "Estimate", CustomFieldType.Date, true));
        Assert.Null(board.SetCustomField(1, "Hours", CustomFieldType.Text, false));
        Assert.Equal("12", board.FindCardById(board.Columns.SelectMany(c => c.Cards).First().Id)!.CustomValues[1]); // still there, hidden
    });

    // ---------- what a task holds

    [Fact]
    public void ATasksValues_AreSavedWithIt_AndComeBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);
        var card = AddTask(board, "Quote", Values((1, "Dupont"), (4, "Yes")));

        Assert.Equal("Dupont", card.CustomValues[1]);
        var reopened = OpenBoard();
        var loaded = reopened.Columns.SelectMany(c => c.Cards).Single();
        Assert.Equal(Values((1, "Dupont"), (4, "Yes")), loaded.CustomValues);
        Assert.Equal("Dupont", MainViewModel.CustomValueText(loaded, reopened.CustomFieldDefs[0]));
    });

    [Fact]
    public void EditingATask_SavesTheValues_AndNoneGivenLeavesThemAlone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddTask(board, "Quote", Values((1, "Dupont")));
        var column = board.ToDoColumn!;

        void Edit(IReadOnlyDictionary<int, string>? values) =>
            board.EditCard(card, "Quote", column, board.Projects.First(), "Normal", null, [], false, null, null, [], [], null, [], false, null, null, null, null, null, values);

        Edit(null);                                  // a caller that knows nothing of the fields
        Assert.Equal("Dupont", OpenBoard().Columns.SelectMany(c => c.Cards).Single().CustomValues[1]);

        Edit(Values((1, "Tremblay"), (2, "3.5")));   // the task window
        Assert.Equal(Values((1, "Tremblay"), (2, "3.5")), OpenBoard().Columns.SelectMany(c => c.Cards).Single().CustomValues);

        Edit(new Dictionary<int, string>());         // every box cleared
        Assert.Empty(OpenBoard().Columns.SelectMany(c => c.Cards).Single().CustomValues);
    });

    [Fact]
    public void OtherChangesToATask_KeepItsValues() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddTask(board, "Quote", Values((1, "Dupont")));

        board.SetCardPriority(card, "High");
        board.MoveCards([card], board.InProgressColumn!);

        Assert.Equal("Dupont", OpenBoard().Columns.SelectMany(c => c.Cards).Single().CustomValues[1]);
    });

    [Fact]
    public void Undo_PutsBackTheValuesTheTaskHadBeforeAnEdit() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddTask(board, "Quote", Values((1, "Dupont")));
        board.EditCard(card, "Quote", board.ToDoColumn!, board.Projects.First(), "Normal", null, [], false, null, null, [], [], null, [], false, null, null, null, null, null,
            Values((1, "Tremblay")));

        board.Undo();

        Assert.Equal("Dupont", card.CustomValues[1]);
        Assert.Equal("Dupont", OpenBoard().Columns.SelectMany(c => c.Cards).Single().CustomValues[1]);
    });

    [Fact]
    public void ADuplicate_AndTheNextOccurrenceOfARepeatingTask_KeepTheValues() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddTask(board, "Weekly review", Values((1, "Dupont")), recurring: true);

        var copy = board.DuplicateCard(card)!;
        Assert.Equal("Dupont", copy.CustomValues[1]);

        board.MoveCards([card], board.DoneColumn!); // finishing a repeating task makes the next one
        var next = board.ToDoColumn!.Cards.Single(c => c != copy && c != card);
        Assert.Equal("Dupont", next.CustomValues[1]);
    });

    [Fact]
    public void TheKeywordFilter_AlsoSearchesTheValues() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var withValue = AddTask(board, "Quote", Values((1, "Dupont")));
        var without = AddTask(board, "Other");

        board.KeywordFilter = "dupont";

        Assert.True(withValue.IsVisible);
        Assert.False(without.IsVisible);
    });

    [Fact]
    public void AChangeToAValue_IsInTheTasksHistory_ByTheFieldsName() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);
        var card = AddTask(board, "Quote");

        board.EditCard(card, "Quote", board.ToDoColumn!, board.Projects.First(), "Normal", null, [], false, null, null, [], [], null, [], false, null, null, null, null, null,
            Values((1, "Dupont")));

        var history = new DatabaseService(_temp.File("board.db")).GetCardHistory(card.Id);
        Assert.Contains(history, h => h.Details != null && h.Details.Contains("Client") && h.Details.Contains("Dupont"));
    });

    // ---------- the file

    [Fact]
    public void TheFileIsStampedAsNewer_OnlyOnceAFieldIsDefinedOrATaskHoldsAValue()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        Assert.NotEqual("19", db.GetSetting("FileFormat"));

        var column = db.GetColumns().First();
        db.AddCard(column.Id, "Plain", null, column.Name, "Normal", null, null, false, null, null);
        Assert.NotEqual("19", db.GetSetting("FileFormat")); // an ordinary task changes nothing

        db.AddCard(column.Id, "With a value", null, column.Name, "Normal", null, null, false, null, null, customValues: Values((1, "x")));
        Assert.Equal("19", db.GetSetting("FileFormat"));
    }

    [Fact]
    public void DefiningAField_StampsTheFile() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);

        Assert.Equal("19", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat"));
    });

    // ---------- the task window

    private MainViewModel OpenBoardForWindows()
    {
        EnsureAppResources();
        return OpenBoard();
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

    [Fact]
    public void ANewTask_StartsWithNoFields_AndThePlusOffersTheOnesNotYetOnIt() => wpf.Run(() =>
    {
        var board = OpenBoardForWindows();
        Assert.Equal(Visibility.Collapsed, new AddTaskWindow(board).CustomFieldsSection.Visibility); // none offered: no section

        board.SetCustomField(1, "Client", CustomFieldType.Text, true);
        board.SetCustomField(2, "Estimate", CustomFieldType.Number, true);
        board.SetCustomField(3, "Review date", CustomFieldType.Date, true);
        board.SetCustomField(4, "Billable", CustomFieldType.YesNo, true);
        board.SetCustomField(5, "Old reference", CustomFieldType.Text, false);

        var window = new AddTaskWindow(board);
        Assert.Equal(Visibility.Visible, window.CustomFieldsSection.Visibility);
        Assert.Empty(window.CustomFieldsPanel.Children);                       // by default no field applies
        Assert.Equal(Visibility.Visible, window.CustomFieldsHint.Visibility);
        Assert.True(window.AddCustomFieldButton.IsEnabled);

        window.AddCustomField(board.CustomFieldDefs[0]);
        window.AddCustomField(board.CustomFieldDefs[3]);

        Assert.Equal(2, window.CustomFieldsPanel.Children.Count);
        Assert.IsType<System.Windows.Controls.TextBox>(window.CustomControlOf(1));
        Assert.IsType<System.Windows.Controls.CheckBox>(window.CustomControlOf(4));
        Assert.Null(window.CustomControlOf(2));
        Assert.Null(window.CustomControlOf(5));                                // not offered
        Assert.Equal(Visibility.Collapsed, window.CustomFieldsHint.Visibility);
    });

    [Fact]
    public void EditingATask_ShowsOnlyTheFieldsOnIt_WithTheirValues() => wpf.Run(() =>
    {
        var board = OpenBoardForWindows();
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);
        board.SetCustomField(2, "Old reference", CustomFieldType.Text, true);
        board.SetCustomField(3, "Estimate", CustomFieldType.Number, true);
        var card = AddTask(board, "Quote", Values((1, "Dupont"), (2, "A-17"))); // Estimate is offered, but is not on this task
        board.SetCustomField(2, "Old reference", CustomFieldType.Text, false);  // and this one is no longer offered: kept, not shown

        var window = new AddTaskWindow(board, card, board.ToDoColumn!);

        Assert.Equal(1, window.CustomFieldsPanel.Children.Count);
        Assert.Equal("Dupont", ((System.Windows.Controls.TextBox)window.CustomControlOf(1)!).Text);
        Assert.Null(window.CustomControlOf(3));
    });

    [Fact]
    public void TheTabs_ShowOnePageAtATime_KeepBothInTheLayout_AndTheSimpleViewHasNoTabs() => wpf.Run(() =>
    {
        var board = OpenBoardForWindows();
        var window = new AddTaskWindow(board);

        Assert.Equal(Visibility.Visible, window.FormScrollViewer.Visibility);
        Assert.Equal(Visibility.Hidden, window.SecondaryScrollViewer.Visibility); // Hidden, not Collapsed: it keeps its room

        window.TaskTabs.SelectedItem = window.SecondaryTab;
        Assert.Equal(Visibility.Hidden, window.FormScrollViewer.Visibility);
        Assert.Equal(Visibility.Visible, window.SecondaryScrollViewer.Visibility);

        window.ViewModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(Visibility.Collapsed, window.TaskTabs.Visibility);
        Assert.Equal(Visibility.Visible, window.FormScrollViewer.Visibility);
        Assert.Equal(Visibility.Collapsed, window.SecondaryScrollViewer.Visibility);

        window.ViewModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(Visibility.Visible, window.TaskTabs.Visibility);
    });
}
