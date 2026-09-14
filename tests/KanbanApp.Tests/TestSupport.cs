using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Tests;

// A throwaway folder per test, so nothing ever touches the real task file, config, or backups.
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanbanApp.Tests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        // Pooled SQLite connections keep the file open, which would block deleting the folder.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

// MainViewModel applies its theme through Application.Current, and WPF objects belong to the thread
// that made them. So one WPF Application lives on a dedicated STA thread for the whole run, and each
// board test's body is marshalled onto that thread with Run.
public sealed class WpfDispatcherFixture : IDisposable
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcherFixture()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        _dispatcher = dispatcher!;
    }

    public void Run(Action action) => _dispatcher.Invoke(action);

    public void Dispose() => _dispatcher.InvokeShutdown();
}

[CollectionDefinition(Name)]
public sealed class WpfCollection : ICollectionFixture<WpfDispatcherFixture>
{
    public const string Name = "WPF";
}
