using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Schedule in Outlook: an appointment for a task, the way Email This Task makes an email. Classic
// Outlook is driven through late-bound COM (no Office reference needed to build) and gets the task's
// details and attachments; the new Outlook for Windows and PCs without Outlook have no automation,
// so they get the same appointment as an .ics file, opened in whatever app handles calendar files.
// Either way the appointment opens for the user to check and save - nothing is ever saved for them.
public static class OutlookCalendarHelper
{
    private static readonly string CalendarFilesRoot = Path.Combine(Path.GetTempPath(), "Kanban Task Board Calendar");

    public static void ScheduleCard(Window owner, CardViewModel card, MainViewModel viewModel) =>
        ScheduleCard(owner, card, viewModel, card.Title, card.DueDate, card.DueDateTime);

    // The task screen passes the title and due date and time as they are on screen, possibly not yet
    // saved, since those decide the appointment; the rest of the details are the task as last saved.
    public static void ScheduleCard(Window owner, CardViewModel card, MainViewModel viewModel, string title, DateTime? dueDate, DateTime? dueDateTime)
    {
        var appointment = CalendarFile.For(dueDate, dueDateTime, DateTime.Now);
        var body = BuildBody(card);

        if (TryOpenInClassicOutlook(owner, title, card, appointment, body))
        {
            viewModel.RecordCardScheduled(card, CalendarFile.Describe(appointment), "in Outlook");
            return;
        }

        OpenCalendarFile(owner, title, card, appointment, body, viewModel);
    }

    internal static string BuildBody(CardViewModel card)
    {
        var sb = new StringBuilder();
        OutlookEmailHelper.AppendPlainTextDetails(sb, card);
        sb.Append("\r\nFrom ").Append(AppInfo.ProductName).Append('.');
        return sb.ToString().Trim();
    }

    // False only when Outlook couldn't make an appointment at all, so the caller can fall back. Once
    // it is on screen any later trouble is reported instead - falling back then would open two.
    private static bool TryOpenInClassicOutlook(Window owner, string title, CardViewModel card, CalendarFile.Appointment appointment, string body)
    {
        dynamic item;
        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null) return false;

            // Attaches to a running Outlook rather than starting a second one.
            dynamic app = Activator.CreateInstance(outlookType)!;
            item = app.CreateItem(1); // olAppointmentItem
            item.Subject = title;
            item.AllDayEvent = appointment.AllDay;
            item.Start = appointment.Start;
            item.End = appointment.End;
            if (appointment.AllDay) item.BusyStatus = 0; // olFree: a due date shouldn't block the day
            item.Body = body;
        }
        catch
        {
            return false;
        }

        // A file that can't be attached (moved, locked) shouldn't cost the user the appointment.
        string? attachProblem = null;
        foreach (var attachment in card.Attachments.Where(a => File.Exists(a.FilePath)))
        {
            try
            {
                item.Attachments.Add(attachment.FilePath);
            }
            catch (Exception ex)
            {
                attachProblem ??= ex.Message;
            }
        }

        try
        {
            item.Display(false);
        }
        catch
        {
            return false;
        }

        if (attachProblem is not null)
        {
            Dialogs.Tell(owner, "Schedule in Outlook",
                "The appointment opened, but not every attachment could be added.\n\nCheck it before saving, and add anything that is missing.",
                DialogTone.Warning, attachProblem);
        }

        return true;
    }

    private static void OpenCalendarFile(Window owner, string title, CardViewModel card, CalendarFile.Appointment appointment, string body, MainViewModel viewModel)
    {
        string path;
        try
        {
            Directory.CreateDirectory(CalendarFilesRoot);
            path = Path.Combine(CalendarFilesRoot, OutlookEmailHelper.SanitizeFileName(title) + ".ics");
            var uid = $"{Guid.NewGuid():N}@kanban-task-board";
            File.WriteAllText(path, CalendarFile.BuildIcs(title, body, appointment, DateTime.UtcNow, uid), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Dialogs.Tell(owner, "Schedule in Outlook", "The appointment could not be made.", DialogTone.Error, ex.Message);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            viewModel.RecordCardScheduled(card, CalendarFile.Describe(appointment), "in your calendar app");
        }
        catch
        {
            Dialogs.Tell(owner, "Schedule in Outlook",
                "No calendar app is set up to open calendar files on this PC.\n\n" +
                "The appointment has been saved as a calendar file. Open your calendar and import it from the folder below.",
                DialogTone.Warning, path);
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch
            {
                // The message already names the file.
            }
        }
    }
}
