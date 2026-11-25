using System.Text;

namespace KanbanApp.Services;

// What goes into crash.log for each unexpected error. One fault can repeat hundreds of times a second (a layout error is
// raised again on every frame), which buried everything else in the file and pushed older entries out of the problem
// report's size limit. So the first time an error is seen it is written in full, with the app version; the same error
// again within a short while is only counted, and the count is written when something else happens (and every 100).
public sealed class CrashLog
{
    public static readonly TimeSpan Burst = TimeSpan.FromSeconds(30);

    private string? _lastSignature;
    private DateTime _lastAt;
    private int _repeats;

    // Two errors are "the same" when they have the same type and message.
    public static string Signature(Exception ex) => ex.GetType().FullName + "|" + ex.Message;

    // A layout error: an element measured or arranged to an impossible size. WPF raises it again on every frame.
    public static bool IsLayoutError(Exception ex) =>
        ex is InvalidOperationException && ex.Message.StartsWith("Layout ", StringComparison.Ordinal)
        && (ex.Message.Contains("measurement override", StringComparison.Ordinal) || ex.Message.Contains("arrange override", StringComparison.Ordinal));

    // The text to append to the log (null when nothing needs writing) and how many times in a row this error has now come.
    public (string? Text, int InARow) Record(Exception ex, DateTime now, string version)
    {
        var signature = Signature(ex);
        if (signature == _lastSignature && now - _lastAt <= Burst)
        {
            _repeats++;
            _lastAt = now;
            var text = _repeats % 100 == 0 ? $"{now:yyyy-MM-dd HH:mm:ss}  (the same error {_repeats} more times so far)\n\n" : null;
            return (text, _repeats + 1);
        }

        var entry = new StringBuilder();
        if (_repeats > 0) entry.Append($"{_lastAt:yyyy-MM-dd HH:mm:ss}  (the error above came {_repeats} more times, the last at this time)\n\n");
        entry.Append($"{now:yyyy-MM-dd HH:mm:ss}  version {version}\n{ex}\n\n");
        _lastSignature = signature;
        _lastAt = now;
        _repeats = 0;
        return (entry.ToString(), 1);
    }
}
