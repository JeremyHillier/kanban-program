using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using ComIStream = System.Runtime.InteropServices.ComTypes.IStream;
using ComFormatEtc = System.Runtime.InteropServices.ComTypes.FORMATETC;
using ComTymed = System.Runtime.InteropServices.ComTypes.TYMED;
using ComDvAspect = System.Runtime.InteropServices.ComTypes.DVASPECT;

namespace KanbanApp.Services;

// Outlook (and similar apps) hand over dragged items as "virtual files" - CFSTR_FILEDESCRIPTOR
// + CFSTR_FILECONTENTS OLE formats - rather than a real path, since no .msg exists on disk yet.
public static class OutlookDragDropHelper
{
    public static bool HasDroppableFiles(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent("FileGroupDescriptorW");

    // Real files (FileDrop) are copied into attachmentsDir, same as virtual files (e.g. an
    // Outlook email, which has no real path and must be saved there anyway). Copying rather
    // than linking in place (unlike "+ File...") means a dropped file's lifecycle is fully
    // owned by the app - it moves with the card into Done/Archived/Deleted and is cleaned up
    // like any other attachment the app created.
    public static List<(string FilePath, string DisplayName, bool WasSaved)> ExtractDroppedFiles(IDataObject data, string attachmentsDir)
    {
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            Directory.CreateDirectory(attachmentsDir);
            return paths.Select(p =>
            {
                var destPath = UniquePath(Path.Combine(attachmentsDir, Path.GetFileName(p)));
                File.Copy(p, destPath);
                return (destPath, Path.GetFileName(destPath), true);
            }).ToList();
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

        var contentsFormatId = (short)DataFormats.GetDataFormat("FileContents").Id;
        var advertisedFormats = EnumerateAdvertisedFormats(comData).Where(f => f.cfFormat == contentsFormatId).ToList();

        var results = new List<(string, string, bool)>();
        for (var i = 0; i < fileNames.Count; i++)
        {
            var safeName = SanitizeFileName(fileNames[i]);
            var destPath = UniquePath(Path.Combine(attachmentsDir, safeName));

            if (!ExtractOneFileContents(comData, i, fileNames.Count, advertisedFormats, destPath)) continue;

            results.Add((destPath, Path.GetFileName(destPath), true));
        }

        return results;
    }

    // The most reliable FORMATETC for a virtual-file source is the exact one it advertised via
    // its own IEnumFORMATETC, rather than one we construct by hand and guess at (lindex/tymed
    // conventions vary enough between apps - see ReadFileContents below - that guessing is fragile).
    private static List<ComFormatEtc> EnumerateAdvertisedFormats(ComDataObject comData)
    {
        var results = new List<ComFormatEtc>();

        System.Runtime.InteropServices.ComTypes.IEnumFORMATETC? enumerator;
        try
        {
            enumerator = comData.EnumFormatEtc(System.Runtime.InteropServices.ComTypes.DATADIR.DATADIR_GET);
        }
        catch (COMException)
        {
            return results;
        }
        if (enumerator is null) return results;

        var buffer = new ComFormatEtc[1];
        var fetched = new int[1];
        while (enumerator.Next(1, buffer, fetched) == 0 && fetched[0] == 1)
        {
            results.Add(buffer[0]);
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

        System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;
        try
        {
            comData.GetData(ref formatEtc, out medium);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException($"reading the file list (FileGroupDescriptorW): {ex.Message}", ex);
        }

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

    // Prefer the exact FORMATETC the source advertised via IEnumFORMATETC, but corrected to this
    // item's lindex - some sources (confirmed: classic Outlook) advertise a summary entry with
    // lindex=-1 that GetData then rejects with DV_E_LINDEX, only accepting the real per-item index.
    // Beyond that, fall back to every (tymed) guess in turn: some IDataObjects check tymed for an
    // exact match rather than a bitmask (so ISTREAM/HGLOBAL/ISTORAGE must be requested separately,
    // never OR'd), and for a single dragged item some sources only respond to lindex=-1 after all.
    private static bool ExtractOneFileContents(ComDataObject comData, int index, int itemCount, List<ComFormatEtc> advertisedFormats, string destPath)
    {
        var contentsFormatId = (short)DataFormats.GetDataFormat("FileContents").Id;

        var candidates = new List<(string Label, ComFormatEtc FormatEtc)>();
        if (index < advertisedFormats.Count)
        {
            var advertised = advertisedFormats[index];
            if (advertised.lindex != index)
            {
                candidates.Add(("advertised tymed, corrected lindex", advertised with { lindex = index }));
            }
            candidates.Add(("advertised", advertised));
        }

        foreach (var tymed in new[] { ComTymed.TYMED_ISTREAM, ComTymed.TYMED_ISTORAGE, ComTymed.TYMED_HGLOBAL })
        {
            candidates.Add(("guess", BuildFormatEtc(contentsFormatId, index, tymed)));
            if (itemCount == 1)
            {
                candidates.Add(("guess", BuildFormatEtc(contentsFormatId, -1, tymed)));
            }
        }

        var errors = new List<string>();
        foreach (var (label, formatEtc) in candidates)
        {
            try
            {
                return ReadFileContents(comData, formatEtc, destPath);
            }
            catch (COMException ex)
            {
                errors.Add($"{label}(lindex={formatEtc.lindex},tymed={formatEtc.tymed}): {ex.Message}");
            }
        }

        throw new InvalidOperationException($"reading file contents for item {index} - advertised {advertisedFormats.Count} FileContents format(s) ({string.Join("; ", errors)})");
    }

    private static ComFormatEtc BuildFormatEtc(short cfFormat, int lindex, ComTymed tymed) => new()
    {
        cfFormat = cfFormat,
        ptd = IntPtr.Zero,
        dwAspect = ComDvAspect.DVASPECT_CONTENT,
        lindex = lindex,
        tymed = tymed
    };

    private static bool ReadFileContents(ComDataObject comData, ComFormatEtc formatEtc, string destPath)
    {
        comData.GetData(ref formatEtc, out var medium);
        try
        {
            switch (medium.tymed)
            {
                case ComTymed.TYMED_ISTREAM:
                    File.WriteAllBytes(destPath, ReadIStream((ComIStream)Marshal.GetObjectForIUnknown(medium.unionmember)));
                    return true;
                case ComTymed.TYMED_HGLOBAL:
                    File.WriteAllBytes(destPath, ReadHGlobal(medium.unionmember));
                    return true;
                case ComTymed.TYMED_ISTORAGE:
                    WriteIStorage((IStorage)Marshal.GetObjectForIUnknown(medium.unionmember), destPath);
                    return true;
                default:
                    return false;
            }
        }
        finally
        {
            NativeMethods.ReleaseStgMedium(ref medium);
        }
    }

    // .msg files are themselves OLE structured-storage documents, so the cleanest way to
    // materialize an IStorage medium is to create a real compound file on disk and let COM
    // copy directly into it - no separate byte-array staging step needed.
    private static void WriteIStorage(IStorage sourceStorage, string destPath)
    {
        const uint stgmCreate = 0x00001000, stgmReadWrite = 0x00000002, stgmShareExclusive = 0x00000010;
        NativeMethods.StgCreateDocfile(destPath, stgmCreate | stgmReadWrite | stgmShareExclusive, 0, out var destStorage);
        try
        {
            sourceStorage.CopyTo(0, null, null, destStorage);
            destStorage.Commit(0);
        }
        finally
        {
            Marshal.ReleaseComObject(destStorage);
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

    // Not present in System.Runtime.InteropServices.ComTypes on .NET Core/.NET - declared by hand,
    // matching the native vtable order exactly (objidl.idl). Only CopyTo/Commit are actually
    // called; the rest must still be declared in order so the vtable slots line up correctly.
    [ComImport]
    [Guid("0000000B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStorage
    {
        void CreateStream(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out ComIStream ppstm);
        void OpenStream(string pwcsName, IntPtr reserved1, uint grfMode, uint reserved2, out ComIStream ppstm);
        void CreateStorage(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStorage ppstg);
        void OpenStorage(string pwcsName, IStorage pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstg);
        void CopyTo(uint ciidExclude, Guid[]? rgiidExclude, string[]? snbExclude, IStorage pstgDest);
        void MoveElementTo(string pwcsName, IStorage pstgDest, string pwcsNewName, uint grfFlags);
        void Commit(uint grfCommitFlags);
        void Revert();
        void EnumElements(uint reserved1, IntPtr reserved2, uint reserved3, out IntPtr ppEnum);
        void DestroyElement(string pwcsName);
        void RenameElement(string pwcsOldName, string pwcsNewName);
        void SetElementTimes(string pwcsName, System.Runtime.InteropServices.ComTypes.FILETIME pctime,
            System.Runtime.InteropServices.ComTypes.FILETIME patime, System.Runtime.InteropServices.ComTypes.FILETIME pmtime);
        void SetClass(ref Guid clsid);
        void SetStateBits(uint grfStateBits, uint grfMask);
        void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, uint grfStatFlag);
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

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void StgCreateDocfile(string pwcsName, uint grfMode, uint reserved, out IStorage ppstgOpen);
    }
}
