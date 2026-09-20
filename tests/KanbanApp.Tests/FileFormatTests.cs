using System.IO;
using System.Security.Cryptography;
using System.Text;
using KanbanApp.Services;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Tests;

// The version stamp in the task file: what it records, that it never goes down, and a tripwire
// for changing what the file stores without raising the format number.
public sealed class FileFormatTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private DatabaseService Open() => new(_temp.File("board.db"));

    [Fact]
    public void ANewFile_IsStampedWithThisFormat_AndTheAppVersion()
    {
        var first = Open();
        Assert.Equal(0, first.FileFormatAtOpen); // nothing there yet when it was opened
        Assert.False(first.IsFromNewerApp);

        var second = Open();
        Assert.Equal(DatabaseService.CurrentFileFormat, second.FileFormatAtOpen);
        Assert.Equal(DatabaseService.CurrentFileFormat.ToString(), second.GetSetting("FileFormat"));
        Assert.Equal(DatabaseService.RunningAppVersion, second.GetSetting("FileFormatAppVersion"));
        Assert.Equal(DatabaseService.RunningAppVersion, second.GetSetting("LastOpenedByVersion"));
        Assert.False(second.IsFromNewerApp);
        Assert.Null(second.NewerAppVersion);
    }

    [Fact]
    public void AFileUsedByANewerApp_IsNoticed_EveryTime_AndItsStampIsLeftAlone()
    {
        var db = Open();
        db.SetSetting("FileFormat", "99");
        db.SetSetting("FileFormatAppVersion", "3.4.5");

        for (var i = 0; i < 2; i++)
        {
            var older = Open();
            Assert.True(older.IsFromNewerApp);
            Assert.Equal("3.4.5", older.NewerAppVersion);
            Assert.Equal("99", older.GetSetting("FileFormat"));
            Assert.Equal("3.4.5", older.GetSetting("FileFormatAppVersion"));
        }
    }

    [Fact]
    public void AStampThatIsNotANumber_CountsAsAnOldFile()
    {
        Open().SetSetting("FileFormat", "banana");

        var db = Open();

        Assert.Equal(0, db.FileFormatAtOpen);
        Assert.False(db.IsFromNewerApp);
        Assert.Equal(DatabaseService.CurrentFileFormat.ToString(), db.GetSetting("FileFormat"));
    }

    [Fact]
    public void TheWarning_NamesBothVersions_AndCopesWithoutTheNewerOne()
    {
        var message = App.NewerTaskFileMessage("0.120.0", "0.102.0");
        Assert.Contains("(0.120.0 or later)", message);
        Assert.Contains("version 0.102.0", message);

        Assert.DoesNotContain("()", App.NewerTaskFileMessage(null, "0.102.0"));
    }

    // If this fails you have changed what the task file stores (a table or a column). An older copy
    // of the app won't know about it, so: add one to DatabaseService.CurrentFileFormat, then put
    // the new format number and fingerprint (both are in the failure message) here.
    [Fact]
    public void ChangingTheTables_MeansRaisingTheFileFormat()
    {
        const int formatTheFingerprintBelongsTo = 2;
        const string fingerprint = "0D5C96D398DD6B52";

        Open();
        SqliteConnection.ClearAllPools();
        var columns = new List<string>();
        using (var connection = new SqliteConnection($"Data Source={_temp.File("board.db")}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT m.name || '.' || p.name || ':' || UPPER(p.type)
                FROM sqlite_master m JOIN pragma_table_info(m.name) p
                WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'
                ORDER BY 1;
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(0));
        }

        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", columns))))[..16];

        Assert.True(actual == fingerprint && DatabaseService.CurrentFileFormat == formatTheFingerprintBelongsTo,
            $"The task file's tables have changed. If you have not already, raise DatabaseService.CurrentFileFormat (now {DatabaseService.CurrentFileFormat}), " +
            $"then set formatTheFingerprintBelongsTo to it and fingerprint to \"{actual}\".");
    }
}
