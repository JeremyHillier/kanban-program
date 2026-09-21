// Turns the demo recorder's frames (PNG files + manifest.txt of "file|milliseconds") into an MP4,
// using the H.264 encoder built into Windows. Nothing to install.
//
//   DemoEncoder <framesDir> <output.mp4> [--bitrate 8000000] [--height 720] [--allkey]
//   DemoEncoder --sizes <video.mp4> <firstFrame> <count>      per-frame sizes and key frames, without decoding
//
// --allkey makes every frame a key frame. The file is several times larger, but it sidesteps a
// playback fault found on the development PC (AMD Radeon, August 2026 driver): there, the frames
// BETWEEN key frames play back peppered with white dashes, in Windows' player and in browsers
// alike, even though the file is sound (those frames are 18-20 bytes: "repeat the last picture").
// Key frames always play correctly. See README.md for how that was established - in particular,
// do not judge a file by decoding it on the PC that shows the fault.
//
// The frames are decoded here and handed to the encoder as ready-made NV12 pictures through a
// MediaStreamSource, rather than through MediaComposition image clips.
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

const int TickMs = 50; // 20 frames a second

if (args.Length == 4 && args[0] == "--sizes") return FrameSizes.Run(args[1], int.Parse(args[2]), int.Parse(args[3]));
if (args.Length < 2 || args[0].StartsWith("--"))
{
    Console.WriteLine("usage: DemoEncoder <framesDir> <output.mp4> [--bitrate N] [--height N] [--allkey]\n       DemoEncoder --sizes <video.mp4> <firstFrame> <count>");
    return 2;
}

var framesDir = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var allKey = args.Contains("--allkey");
var bitrate = Option("--bitrate") ?? (allKey ? 8_000_000u : 2_500_000u);
var outHeight = Option("--height") ?? 0u; // 0 = the same size as the frames

uint? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? uint.Parse(args[i + 1]) : null;
}

// One entry per 1/20 s of video, naming the picture showing at that moment. A held picture simply
// repeats, which costs almost nothing once compressed (unless --allkey).
var schedule = new List<string>();
double elapsedMs = 0;
foreach (var line in File.ReadAllLines(Path.Combine(framesDir, "manifest.txt")))
{
    var parts = line.Split('|');
    elapsedMs += int.Parse(parts[1]);
    while (schedule.Count * TickMs < elapsedMs - TickMs / 2.0) schedule.Add(Path.Combine(framesDir, parts[0]));
}

var (width, height, _) = await Decode(schedule[0]);
Console.WriteLine($"{schedule.Count} video frames, {schedule.Count * TickMs / 1000.0:0.0} s, {width}x{height}{(allKey ? ", every frame a key frame" : "")}");

var raw = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Nv12, width, height);
raw.FrameRate.Numerator = 1000 / TickMs;
raw.FrameRate.Denominator = 1;
raw.PixelAspectRatio.Numerator = 1;
raw.PixelAspectRatio.Denominator = 1;
var source = new MediaStreamSource(new VideoStreamDescriptor(raw))
{
    Duration = TimeSpan.FromMilliseconds(schedule.Count * TickMs),
    CanSeek = false,
    BufferTime = TimeSpan.Zero
};

var next = 0;
string? cachedPath = null;
byte[]? cachedPixels = null;
source.Starting += (_, e) => e.Request.SetActualStartPosition(TimeSpan.Zero);
source.SampleRequested += (_, e) =>
{
    if (next >= schedule.Count) { e.Request.Sample = null; return; }

    var deferral = e.Request.GetDeferral();
    try
    {
        var path = schedule[next];
        if (path != cachedPath)
        {
            cachedPixels = Decode(path).GetAwaiter().GetResult().Pixels;
            cachedPath = path;
        }

        var sample = MediaStreamSample.CreateFromBuffer(cachedPixels!.AsBuffer(), TimeSpan.FromMilliseconds(next * TickMs));
        sample.Duration = TimeSpan.FromMilliseconds(TickMs);
        sample.KeyFrame = true;
        e.Request.Sample = sample;
        next++;
    }
    finally { deferral.Complete(); }
};

var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
profile.Video.Width = outHeight == 0 ? width : width * outHeight / height;
profile.Video.Height = outHeight == 0 ? height : outHeight;
profile.Video.FrameRate.Numerator = 1000 / TickMs;
profile.Video.FrameRate.Denominator = 1;
profile.Video.Bitrate = bitrate;
profile.Video.ProfileId = H264ProfileIds.Main;
profile.Audio = null; // silent
if (allKey)
{
    profile.Video.Properties[new Guid("C16EB52B-73A1-476F-8D62-839D6A020652")] = 1u; // MF_MT_MAX_KEYFRAME_SPACING (ignored by itself)
    profile.Video.Properties[new Guid("95F31B26-95A4-41AA-9303-246A7FC6EEF1")] = 1u; // CODECAPI_AVEncMPVGOPSize - the one that works
}

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(output)!);
var target = await folder.CreateFileAsync(Path.GetFileName(output), CreationCollisionOption.GenerateUniqueName);

var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = false };
TranscodeFailureReason result;
using (var stream = await target.OpenAsync(FileAccessMode.ReadWrite))
{
    var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, stream, profile);
    result = prepared.FailureReason;
    if (prepared.CanTranscode) await prepared.TranscodeAsync();
}

Console.WriteLine($"{result}: {target.Path} ({new FileInfo(target.Path).Length / 1024} KB)");
return result == TranscodeFailureReason.None ? 0 : 1;

static async Task<(uint Width, uint Height, byte[] Pixels)> Decode(string path)
{
    var file = await StorageFile.GetFileFromPathAsync(path);
    using var stream = await file.OpenAsync(FileAccessMode.Read);
    var decoder = await BitmapDecoder.CreateAsync(stream);
    var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, new BitmapTransform(),
        ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
    return (decoder.PixelWidth, decoder.PixelHeight, ToNv12(data.DetachPixelData(), (int)decoder.PixelWidth, (int)decoder.PixelHeight));
}

// BGRA to NV12 (the encoder's own format: a brightness plane, then colour at half resolution),
// BT.709 limited range as HD video expects.
static byte[] ToNv12(byte[] bgra, int width, int height)
{
    var nv12 = new byte[width * height * 3 / 2];
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 4;
            double b = bgra[i], g = bgra[i + 1], r = bgra[i + 2];
            nv12[y * width + x] = (byte)Math.Clamp(Math.Round(16 + 0.1826 * r + 0.6142 * g + 0.0620 * b), 16, 235);
        }
    }

    var chroma = width * height;
    for (var y = 0; y < height; y += 2)
    {
        for (var x = 0; x < width; x += 2)
        {
            double r = 0, g = 0, b = 0;
            for (var dy = 0; dy < 2; dy++)
                for (var dx = 0; dx < 2; dx++)
                {
                    var i = ((y + dy) * width + x + dx) * 4;
                    b += bgra[i]; g += bgra[i + 1]; r += bgra[i + 2];
                }
            r /= 4; g /= 4; b /= 4;
            var o = chroma + (y / 2) * width + x;
            nv12[o] = (byte)Math.Clamp(Math.Round(128 - 0.1006 * r - 0.3386 * g + 0.4392 * b), 16, 240);
            nv12[o + 1] = (byte)Math.Clamp(Math.Round(128 + 0.4392 * r - 0.3989 * g - 0.0403 * b), 16, 240);
        }
    }
    return nv12;
}
