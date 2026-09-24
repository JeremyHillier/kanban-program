using KanbanApp.Services;

namespace KanbanApp.Tests;

// On/off and number settings read the way the app always read them, including what happens when
// the stored value is missing or odd.
public sealed class SettingFlagTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(null, true, true)]      // never set: its default
    [InlineData(null, false, false)]
    [InlineData("True", true, true)]
    [InlineData("True", false, true)]
    [InlineData("False", true, false)]
    [InlineData("False", false, false)]
    [InlineData("yes", true, true)]     // odd values: a starts-on setting stays on...
    [InlineData("yes", false, false)]   // ...and a starts-off one stays off
    [InlineData("true", true, true)]
    [InlineData("true", false, false)]  // only the exact word "True" switches a starts-off setting on
    [InlineData("", true, true)]
    public void AFlag_ReadsAsItAlwaysDid(string? stored, bool defaultValue, bool expected)
    {
        var db = new DatabaseService(_temp.File("board.db"));
        if (stored is not null) db.SetSetting("Something", stored);

        Assert.Equal(expected, db.GetFlag("Something", defaultValue));
        Assert.Equal(defaultValue ? stored != "False" : stored == "True", db.GetFlag("Something", defaultValue)); // the old rule, spelled out
    }

    [Fact]
    public void AFlag_IsSavedAsTrueOrFalse()
    {
        var db = new DatabaseService(_temp.File("board.db"));

        db.SetFlag("A", true);
        db.SetFlag("B", false);

        Assert.Equal("True", db.GetSetting("A"));
        Assert.Equal("False", db.GetSetting("B"));
    }

    [Theory]
    [InlineData(null, 1800)]
    [InlineData("2500", 2500)]
    [InlineData("-3", -3)]
    [InlineData("soon", 1800)]
    [InlineData("", 1800)]
    public void ANumber_FallsBackToItsDefault(string? stored, int expected)
    {
        var db = new DatabaseService(_temp.File("board.db"));
        if (stored is not null) db.SetSetting("Delay", stored);

        Assert.Equal(expected, db.GetInt("Delay", 1800));
    }
}
