using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ComIStream = System.Runtime.InteropServices.ComTypes.IStream;

namespace KanbanApp.Services;

// Reading the attachments out of an Outlook .msg file. A .msg is an OLE compound file (the same
// structured storage the drag-and-drop code writes them with): each attachment is a storage named
// "__attach_version1.0_#0000000N", holding streams for its file name (MAPI property 3707, or 3704
// for the short name) and its bytes (property 3701). That is enough to pull an Excel file out of
// an email dropped whole onto the Import screen, without Outlook's help.
public static partial class OutlookDragDropHelper
{
    private const uint StgmRead = 0x0, StgmReadWrite = 0x2, StgmShareExclusive = 0x10, StgmShareDenyWrite = 0x20, StgmCreate = 0x1000;

    public static bool IsOutlookMessageFile(string path) => Path.GetExtension(path).Equals(".msg", StringComparison.OrdinalIgnoreCase);

    // The attachments whose file names pass keep, written into destDir. Returns their paths. A file
    // that isn't a .msg at all (or has no attachments) gives an empty list rather than an error.
    public static List<string> ExtractMsgAttachments(string msgPath, string destDir, Func<string, bool> keep)
    {
        var results = new List<string>();
        IStorage message;
        try
        {
            NativeMethods.StgOpenStorage(msgPath, null, StgmRead | StgmShareDenyWrite, IntPtr.Zero, 0, out message);
        }
        catch (COMException)
        {
            return results;
        }

        try
        {
            Directory.CreateDirectory(destDir);
            for (var i = 0; i < 1024; i++)
            {
                IStorage attachment;
                try
                {
                    message.OpenStorage($"__attach_version1.0_#{i:X8}", null!, StgmRead | StgmShareExclusive, IntPtr.Zero, 0, out attachment);
                }
                catch (COMException)
                {
                    break; // attachments are numbered without gaps
                }

                try
                {
                    var name = ReadStringProperty(attachment, "3707") ?? ReadStringProperty(attachment, "3704");
                    if (name is null || !keep(name)) continue;

                    var bytes = ReadBinaryProperty(attachment, "3701");
                    if (bytes is null) continue;

                    var destPath = UniquePath(Path.Combine(destDir, SanitizeFileName(name)));
                    File.WriteAllBytes(destPath, bytes);
                    results.Add(destPath);
                }
                finally
                {
                    Marshal.ReleaseComObject(attachment);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(message);
        }

        return results;
    }

    // Unicode (001F) messages are the norm; an ANSI (001E) one is read in the local code page.
    private static string? ReadStringProperty(IStorage storage, string propertyId)
    {
        var unicode = ReadStream(storage, $"__substg1.0_{propertyId}001F");
        if (unicode is not null) return Encoding.Unicode.GetString(unicode).TrimEnd('\0');

        var ansi = ReadStream(storage, $"__substg1.0_{propertyId}001E");
        return ansi is null ? null : Encoding.Default.GetString(ansi).TrimEnd('\0');
    }

    private static byte[]? ReadBinaryProperty(IStorage storage, string propertyId) => ReadStream(storage, $"__substg1.0_{propertyId}0102");

    private static byte[]? ReadStream(IStorage storage, string name)
    {
        ComIStream stream;
        try
        {
            storage.OpenStream(name, IntPtr.Zero, StgmRead | StgmShareExclusive, 0, out stream);
        }
        catch (COMException)
        {
            return null;
        }

        try
        {
            return ReadIStream(stream);
        }
        finally
        {
            Marshal.ReleaseComObject(stream);
        }
    }

    // Writes a minimal .msg with the given attachments - the same layout the reader above expects.
    // For the tests, which can't drag anything out of Outlook.
    internal static void WriteMsgWithAttachments(string path, IEnumerable<(string Name, byte[] Bytes)> attachments)
    {
        NativeMethods.StgCreateDocfile(path, StgmCreate | StgmReadWrite | StgmShareExclusive, 0, out var message);
        try
        {
            var i = 0;
            foreach (var (name, bytes) in attachments)
            {
                message.CreateStorage($"__attach_version1.0_#{i++:X8}", StgmCreate | StgmReadWrite | StgmShareExclusive, 0, 0, out var attachment);
                try
                {
                    WriteStream(attachment, "__substg1.0_3707001F", Encoding.Unicode.GetBytes(name));
                    WriteStream(attachment, "__substg1.0_37010102", bytes);
                    attachment.Commit(0);
                }
                finally
                {
                    Marshal.ReleaseComObject(attachment);
                }
            }
            message.Commit(0);
        }
        finally
        {
            Marshal.ReleaseComObject(message);
        }
    }

    private static void WriteStream(IStorage storage, string name, byte[] bytes)
    {
        storage.CreateStream(name, StgmCreate | StgmReadWrite | StgmShareExclusive, 0, 0, out var stream);
        try
        {
            stream.Write(bytes, bytes.Length, IntPtr.Zero);
            stream.Commit(0);
        }
        finally
        {
            Marshal.ReleaseComObject(stream);
        }
    }

    private static partial class NativeMethods
    {
        [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void StgOpenStorage(string pwcsName, IStorage? pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstgOpen);
    }
}
