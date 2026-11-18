using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Custom fields beyond the task screen: reports (shown under each task, and searched), Excel files, emailed and imported tasks,
// copying to another task file, and Copy as Text. A file carries a field's name, not its slot, so two task files with the same
// field in different places still understand each other.
[Collection(WpfCollection.Name)]
public sealed class CustomFieldSharingTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string file = "board.db") => new(new DatabaseService(_temp.File(file)));

    private static Dictionary<int, string> Values(params (int Slot, string Value)[] values) => values.ToDictionary(v => v.Slot, v => v.Value);

    private static CardViewModel AddTask(MainViewModel board, string title, IReadOnlyDictionary<int, string>? values = null) =>
        board.AddCard(title, board.ToDoColumn!, board.Projects.First(), "Normal", null, null, false, null, null, customValues: values);

    // A board with four fields on: Client (text), Estimate (number), Review date (date) and Billable (yes / no).
    private MainViewModel BoardWithFields(string file = "board.db")
    {
        var board = OpenBoard(file);
        board.SetCustomField(1, "Client", CustomFieldType.Text, true);
        board.SetCustomField(2, "Estimate", CustomFieldType.Number, true);
        board.SetCustomField(3, "Review date", CustomFieldType.Date, true);
        board.SetCustomField(4, "Billable", CustomFieldType.YesNo, true);
        return board;
    }

    private static List<ReportRow> Rows(MainViewModel board, IReadOnlyList<CustomFieldDef>? include = null, CustomFieldDef? filterField = null, string? filterText = null) =>
        ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(), [], [], [], "All", "All", "All", null, null, false,
            null, "None", "None", "None", includeCustomFields: include, customFilterField: filterField, customFilterText: filterText);

    private static List<string> Texts(FixedDocument document) => document.Pages.SelectMany(p => TextsIn(p.Child)).ToList();

    private static IEnumerable<string> TextsIn(DependencyObject element) =>
        element is TextBlock text ? [text.Text] : LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>().SelectMany(TextsIn);

    // ---------- reports

    [Fact]
    public void ATaskListShowsTheChosenFields_ThatTheTaskHoldsAValueIn() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        AddTask(board, "Quote", Values((1, "Dupont"), (2, "12.5"), (4, "Yes")));
        AddTask(board, "Bare");
        var client = board.CustomFieldDefs[0];
        var estimate = board.CustomFieldDefs[1];

        var rows = Rows(board, [client]);

        Assert.Equal([("Client", "Dupont")], rows.Single(r => r.Title == "Quote").CustomFields);
        Assert.Empty(rows.Single(r => r.Title == "Bare").CustomFields);
        Assert.Equal(2, Rows(board, [client, estimate]).Single(r => r.Title == "Quote").CustomFields.Count);
        Assert.Empty(Rows(board).Single(r => r.Title == "Quote").CustomFields); // none chosen

        var texts = Texts(ReportService.BuildFixedDocument("Report", Rows(board, [client]), "None", false, false));
        Assert.Contains("Client: Dupont", texts);
        Assert.DoesNotContain(texts, t => t.StartsWith("Estimate"));
    });

    [Fact]
    public void AReportCanSearchOneField_TextByWhatItContains_AndYesNoByYesOrNo() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        AddTask(board, "Dupont job", Values((1, "Dupont Inc"), (4, "Yes")));
        AddTask(board, "Tremblay job", Values((1, "Tremblay")));
        var client = board.CustomFieldDefs[0];
        var billable = board.CustomFieldDefs[3];

        Assert.Equal(["Dupont job"], Rows(board, null, client, " DUPONT ").Select(r => r.Title));
        Assert.Equal(2, Rows(board, null, client, "").Count);            // no text, no search
        Assert.Equal(2, Rows(board, null, null, "Dupont").Count);        // no field, no search
        Assert.Equal(["Dupont job"], Rows(board, null, billable, "yes").Select(r => r.Title));
        Assert.Equal(["Tremblay job"], Rows(board, null, billable, "no").Select(r => r.Title));
    });

    [Fact]
    public void ASavedView_AsksForFieldsBySlot_AndOneSwitchedOffSinceDropsOut() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        AddTask(board, "Quote", Values((1, "Dupont"), (2, "12.5")));
        var view = new SavedReportView
        {
            IncludedColumns = board.Columns.Select(c => c.Name).ToList(), IncludedCustomFields = [1, 2], CustomFilterField = 1, CustomFilterText = "dup"
        };

        Assert.Equal(["Client", "Estimate"], Assert.Single(ReportRunner.BuildRows(board, view, DateTime.Today)).CustomFields.Select(f => f.Name));
        Assert.Contains("Client contains: \"dup\"", ReportRunner.Summarise(view, DateTime.Today, board.CustomFieldDefs));

        board.SetCustomField(2, "Estimate", CustomFieldType.Number, false);
        Assert.Equal(["Client"], Assert.Single(ReportRunner.BuildRows(board, view, DateTime.Today)).CustomFields.Select(f => f.Name));

        view.CustomFilterText = "zzz"; // the search applies in statistics too
        Assert.Equal(0, ReportRunner.BuildStatistics(board, view, DateTime.Today).TaskCount);
        view.CustomFilterText = "dup";
        Assert.Equal(1, ReportRunner.BuildStatistics(board, view, DateTime.Today).TaskCount);
    });

    [Fact]
    public void AnOlderSavedView_HasNoCustomFields()
    {
        var old = JsonSerializer.Deserialize<SavedReportView>("""{"Name":"Old"}""")!;
        Assert.Empty(old.IncludedCustomFields);
        Assert.Equal(0, old.CustomFilterField);
        Assert.Equal(string.Empty, old.CustomFilterText);
        Assert.DoesNotContain("contains", ReportRunner.Summarise(old, DateTime.Today));
    }

    // ---------- Excel files and sharing

    [Fact]
    public void AnExcelFile_CarriesEachFieldInItsOwnColumn_AsItsKindOfValue() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        var card = AddTask(board, "Quote", Values((1, "Dupont"), (2, "12.5"), (3, "2026-12-01"), (4, "Yes")));
        var path = _temp.File("tasks.xlsx");

        ImportService.SaveTasksFile(path, [OutlookEmailHelper.BuildImportRow(card, board)], board.ActiveCustomFields);
        var row = Assert.Single(ImportService.ReadTasks(path, board.ActiveCustomFields));

        Assert.Equal(new Dictionary<string, string> { ["Client"] = "Dupont", ["Estimate"] = "12.5", ["Review date"] = "2026-12-01", ["Billable"] = "Yes" }, row.CustomFields);
        Assert.Null(Assert.Single(ImportService.ReadTasks(path)).CustomFields); // read without the fields: ignored, not an error
    });

    [Fact]
    public void AFileWithoutAFieldsColumn_LeavesThatFieldAloneOnImport_ButAnEmptyCellClearsIt() => wpf.Run(() =>
    {
        var sender = BoardWithFields("sender.db");
        var card = AddTask(sender, "Quote", Values((1, "Dupont"), (2, "12.5")));
        var first = _temp.File("first.xlsx");
        ImportService.SaveSingleTaskFile(first, OutlookEmailHelper.BuildImportRow(card, sender), sender.ActiveCustomFields);

        var recipient = BoardWithFields("recipient.db");
        var received = Assert.Single(recipient.ImportCards(ImportService.ReadTasks(first, recipient.ActiveCustomFields)));
        Assert.Equal(Values((1, "Dupont"), (2, "12.5")), received.CustomValues);

        // The sender clears the client and sends it back; the recipient's copy follows, and keeps the field the file does not mention.
        sender.EditCard(card, "Quote", sender.ToDoColumn!, sender.Projects.First(), "Normal", null, [], false, null, null, [], [], null, [], false, null, null, null, null, null,
            Values((2, "12.5")));
        var second = _temp.File("second.xlsx");
        ImportService.SaveSingleTaskFile(second, OutlookEmailHelper.BuildImportRow(card, sender), sender.ActiveCustomFields);
        recipient.ImportCards(ImportService.ReadTasks(second, recipient.ActiveCustomFields));
        Assert.Equal(Values((2, "12.5")), received.CustomValues);

        var onlyClient = new List<CustomFieldDef> { recipient.CustomFieldDefs[0] };
        recipient.ImportCards(ImportService.ReadTasks(second, onlyClient)); // a file with only the Client column reads no Estimate at all
        Assert.Equal(Values((2, "12.5")), received.CustomValues);
    });

    [Fact]
    public void ABoardTakesAValueByTheFieldsName_WhateverSlotItIsInThere_AndIgnoresFieldsItDoesNotHave() => wpf.Run(() =>
    {
        var sender = BoardWithFields("sender.db");
        var card = AddTask(sender, "Quote", Values((1, "Dupont"), (2, "12.5")));
        var path = _temp.File("tasks.xlsx");
        ImportService.SaveSingleTaskFile(path, OutlookEmailHelper.BuildImportRow(card, sender), sender.ActiveCustomFields);

        var recipient = OpenBoard("recipient.db");
        recipient.SetCustomField(7, "client", CustomFieldType.Text, true);             // the same field, another slot and capitals
        recipient.SetCustomField(8, "Estimate", CustomFieldType.Number, false);         // switched off here: not filled in

        var received = Assert.Single(recipient.ImportCards(ImportService.ReadTasks(path, recipient.ActiveCustomFields)));

        Assert.Equal(Values((7, "Dupont")), received.CustomValues);
    });

    [Fact]
    public void TheShareFingerprint_IsTheSameAsBeforeForATaskWithNoValues_AndChangesWithOne()
    {
        var plain = new ImportedTaskRow { Title = "Quote", Priority = "High" };
        var emptyFields = new ImportedTaskRow { Title = "Quote", Priority = "High", CustomFields = new Dictionary<string, string> { ["Client"] = "" } };
        var withValue = new ImportedTaskRow { Title = "Quote", Priority = "High", CustomFields = new Dictionary<string, string> { ["Client"] = "Dupont" } };

        Assert.Equal(ShareSync.Fingerprint(plain), ShareSync.Fingerprint(emptyFields)); // copies exchanged before custom fields are not taken for changed ones
        Assert.NotEqual(ShareSync.Fingerprint(plain), ShareSync.Fingerprint(withValue));
        Assert.Contains(ShareSync.Differences(plain, withValue), d => d.Name == "Custom fields");
    }

    [Fact]
    public void ACustomFieldCannotTakeAnExcelHeading() =>
        Assert.All(new[] { "Title", "task", "Status", "Due", "Description", "Checklist", "Shared By", "URL" }, name => Assert.True(ImportService.IsBuiltInHeading(name), name));

    // ---------- copying

    [Fact]
    public void CopyingToAnotherFile_CarriesAValueToTheFieldWithTheSameName_AndLeavesTheRestBehind() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        var card = AddTask(board, "Quote", Values((1, "Dupont"), (2, "12.5"), (3, "2026-12-01")));

        var target = new DatabaseService(_temp.File("target.db"));
        var other = new MainViewModel(target);
        other.SetCustomField(5, "CLIENT", CustomFieldType.Text, true);       // same name, other capitals and slot
        other.SetCustomField(6, "Estimate", CustomFieldType.Text, true);     // same name, another type: read as the text it shows
        // no "Review date" here: that value stays behind

        TaskCopyService.Copy([(card, "To Do")], _temp.File("target.db"), board.CustomFieldDefs);

        var copied = Assert.Single(new DatabaseService(_temp.File("target.db")).GetCards());
        Assert.Equal(Values((5, "Dupont"), (6, "12.5")), copied.CustomValues);

        TaskCopyService.Copy([(card, "To Do")], _temp.File("target.db")); // no fields given: copies the task, none of the values
        Assert.Equal(2, new DatabaseService(_temp.File("target.db")).GetCards().Count);
    });

    [Fact]
    public void CopyAsText_ListsTheFieldsATaskHoldsAValueIn() => wpf.Run(() =>
    {
        var board = BoardWithFields();
        var card = AddTask(board, "Quote", Values((1, "Dupont"), (4, "Yes")));

        var text = CardTextFormatter.Format(card, "To Do", board.ActiveCustomFields);

        Assert.Contains("Client: Dupont\r\nBillable: Yes", text); // the last line has no line break after it
        Assert.DoesNotContain("Estimate", text);
        Assert.DoesNotContain("Client", CardTextFormatter.Format(card, "To Do"));
    });
}
