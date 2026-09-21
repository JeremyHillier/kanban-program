// Reads the per-frame sizes (the MP4's 'stsz' table) and key-frame list ('stss') without decoding.
internal static class FrameSizes
{
    public static int Run(string video, int first, int count)
    {
        var bytes = File.ReadAllBytes(Path.GetFullPath(video));
        var stsz = Find(bytes, "stsz");
        var sampleCount = Be(bytes, stsz + 12);
        var sizes = Enumerable.Range(0, sampleCount).Select(i => Be(bytes, stsz + 16 + i * 4)).ToList();
        var keys = new HashSet<int>();
        var stss = Find(bytes, "stss");
        if (stss > 0) for (var i = 0; i < Be(bytes, stss + 8); i++) keys.Add(Be(bytes, stss + 12 + i * 4) - 1);
        Console.WriteLine($"{sampleCount} frames; key frames: {keys.Count} ({string.Join(",", keys.OrderBy(k => k).Take(12))}...)");
        if (keys.Count == 0) Console.WriteLine($"no key-frame table: every frame is a key frame; average {sizes.Average():0} bytes");
        else Console.WriteLine($"key frame average: {keys.Average(k => sizes[k]):0} bytes; other frames average: {sizes.Where((_, i) => !keys.Contains(i)).DefaultIfEmpty(0).Average():0} bytes");
        Console.WriteLine(string.Join(" ", Enumerable.Range(first, count).Select(i => (keys.Contains(i) ? "K" : "") + sizes[i])));
        return 0;
    }

    private static int Be(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    private static int Find(byte[] data, string fourcc)
    {
        for (var i = 4; i < data.Length - 4; i++)
            if (data[i] == fourcc[0] && data[i + 1] == fourcc[1] && data[i + 2] == fourcc[2] && data[i + 3] == fourcc[3]) return i;
        return -1;
    }
}
