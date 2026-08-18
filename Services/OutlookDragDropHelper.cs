using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using ComIStream = System.Runtime.InteropServices.ComTypes.IStream;

namespace KanbanApp.Services;

// Outlook (and similar apps) hand over dragged items as "virtual files" - CFSTR_FILEDESCRIPTOR
// + CFSTR_FILECONTENTS OLE formats - rather than a real path, since no .msg exists on disk yet.
public static class OutlookDragDropHelper
{
    public static bool HasDroppableFiles(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent("FileGroupDescriptorW");

    // Real files (FileDrop) are linked in place, matching "+ File...". Virtual files (e.g. an
    // Outlook email) have no real path, so their bytes are saved into attachmentsDir first.
    public static List<(string FilePath, string DisplayName, bool WasSaved)> ExtractDroppedFiles(IDataObject data, string attachmentsDir)
    {
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            return paths.Select(p => (p, Path.GetFileName(p), false)).ToList();
        }

        if (data.GetDataPresent("FileGroupDescriptorW"))
        {
            return ExtractVirtualFiles((ComDataObject)data, attachmentsDir);
        }

        return [];
    }

    private static List<(string, string, bool)> ExtractVirtualFiles(ComDataObject comData, string attachmentsDir)
    {
        var fileNames = ReadFileGroupDescriptor(comData);
        if (fileNames.Count == 0) return [];

        Directory.CreateDirectory(attachmentsDir);

        var results = new List<(string, string, bool)>();
        for (var i = 0; i < fileNames.Count; i++)
        {
            var bytes = ReadFileContents(comData, i);
            if (bytes is null) continue;

            var safeName = SanitizeFileName(fileNames[i]);
            var destPath = UniquePath(Path.Combine(attachmentsDir, safeName));
            File.WriteAllBytes(destPath, bytes);
            results.Add((destPath, Path.GetFileName(destPath), true));
        }

        return results;
    }

    private static List<string> ReadFileGroupDescriptor(ComDataObject comData)
    {
        var formatEtc = new System.Runtime.InteropServices.ComTypes.FORMATETC
        {
            cfFormat = (short)DataFormats.GetDataFormat("FileGroupDescriptorW").Id,
            ptd = IntPtr.Zero,
            dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
        };

        comData.GetData(ref formatEtc, out var medium);
        try
        {
            var ptr = NativeMethods.GlobalLock(medium.unionmember);
            if (ptr == IntPtr.Zero) return [];
            try
            {
                var count = Marshal.ReadInt32(ptr);
                var names = new List<string>();
                var descriptorSize = Marshal.SizeOf<FILEDESCRIPTOR>();
                for (var i = 0; i < count; i++)
                {
                    var descPtr = IntPtr.Add(ptr, 4 + i * descriptorSize);
                    var desc = Marshal.PtrToStructure<FILEDESCRIPTOR>(descPtr);
                    names.Add(desc.cFileName);
                }
                return names;
            }
            finally
            {
                NativeMethods.GlobalUnlock(medium.unionmember);
            }
        }
        finally
        {
            NativeMethods.ReleaseStgMedium(ref medium);
        }
    }

    private static byte[]? ReadFileContents(ComDataObject comData, int index)
    {
        var formatEtc = new System.Runtime.InteropServices.ComTypes.FORMATETC
        {
            cfFormat = (short)DataFormats.GetDataFormat("FileContents").Id,
            ptd = IntPtr.Zero,
            dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
            lindex = index,
            tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM | System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
        };

        comData.GetData(ref formatEtc, out var medium);
        try
        {
            return medium.tymed switch
            {
                System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM =>
                    ReadIStream((ComIStream)Marshal.GetObjectForIUnknown(medium.unionmember)),
                System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL =>
                    ReadHGlobal(medium.unionmember),
                _ => null
            };
        }
        finally
        {
            NativeMethods.ReleaseStgMedium(ref medium);
        }
    }

    private static byte[] ReadIStream(ComIStream stream)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        var bytesReadPtr = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            while (true)
            {
                stream.Read(buffer, buffer.Length, bytesReadPtr);
                var bytesRead = Marshal.ReadInt32(bytesReadPtr);
                if (bytesRead <= 0) break;
                output.Write(buffer, 0, bytesRead);
                if (bytesRead < buffer.Length) break;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(bytesReadPtr);
        }
        return output.ToArray();
    }

    private static byte[] ReadHGlobal(IntPtr hGlobal)
    {
        var ptr = NativeMethods.GlobalLock(hGlobal);
        try
        {
            var size = (int)NativeMethods.GlobalSize(hGlobal);
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            NativeMethods.GlobalUnlock(hGlobal);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? $"Attachment_{DateTime.Now:yyyyMMdd_HHmmss}.dat" : sanitized;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));
        return candidate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FILEDESCRIPTOR
    {
        public uint dwFlags;
        public Guid clsid;
        public int cx;
        public int cy;
        public int x;
        public int y;
        public uint dwFileAttributes;
        public long ftCreationTime;
        public long ftLastAccessTime;
        public long ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        public static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("ole32.dll")]
        public static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);
    }
}
