# Demo video

Makes the short "basics" video (new task, Today filter, dragging between columns) without any
extra software and without recording the real screen or real tasks.

Nothing here is part of the app: `KanbanApp.csproj` excludes the whole `tools` folder.

## How it works

1. **`DemoRecorder.cs`** plays a scripted scene *inside the real app*, against a made-up board in
   a throwaway folder under `%TEMP%\kanban-demo`. The app's own screens are photographed; the
   mouse pointer, click rings, captions and the dragged "ghost" card are drawn on top. It writes
   PNG frames and a `manifest.txt` of `file|milliseconds`.
2. **`Encoder`** turns those frames into an MP4 with the H.264 encoder built into Windows.
3. **`Viewer`** serves a folder on `127.0.0.1:8765` so the result can be played in a browser and
   checked for the playback fault described below.

## Recording

The recorder needs the app's internals, so it is compiled into the app temporarily, the same way
as the other self-checks (see CLAUDE.md):

1. Copy `tools/DemoVideo/DemoRecorder.cs` into the project root.
2. In `App.xaml.cs`, straight after `base.OnStartup(e);`, add:
   `if (e.Args.Length > 0 && e.Args[0] == "--demo-record") { ShutdownMode = ShutdownMode.OnExplicitShutdown; DemoRecorder.Run(); return; }`
3. `dotnet build KanbanApp.csproj -c Debug`, then run `bin/Debug/net10.0-windows/KanbanApp.exe --demo-record`.
   It takes about a minute and shows the app's windows while it works. `%TEMP%\kanban-demo\result.txt`
   names the frames folder.
4. Put `App.xaml.cs` back as it was and move the copied `DemoRecorder.cs` out of the project root
   again. **Neither change is ever committed.** If the recorder was edited, copy it back here.

It never opens the real task file: it builds its own `demo.db`. It hides the TEST BUILD badge so
the picture matches the released app. To change what the video shows, edit the script in
`DemoRecorder.Run()` - captions, pacing (`Hold`, `MoveTo`), the made-up tasks, the scenes.

## Encoding

```
dotnet run -c Release --project tools/DemoVideo/Encoder -- <framesDir> <output.mp4> --allkey
```

- `--allkey` makes every frame a key frame (default 8 Mbps, about 32 MB for 38 s at 1080p).
  Without it the file is about 5 MB (2.5 Mbps). `--bitrate N` and `--height 720` adjust either.
- `--sizes <video.mp4> <first> <count>` prints per-frame sizes and the key frames, without decoding.

## The playback fault, and why `--allkey` exists

On the development PC (AMD Radeon RX 7900 XT plus integrated Radeon, driver dated 16 Aug 2026) the
ordinary file plays back covered in short white horizontal dashes, in Windows' own player and in
Chromium alike. What was established, in order:

- The recorder's PNG frames are clean.
- It is not the encoder, the picture format, the size or the H.264 profile: hardware and software
  encoding, PNG/BMP input, BGRA/NV12 samples, 1080p/720p and Main/Baseline all behave the same.
- **Key frames always play clean; only the frames between them show dashes.**
- In a stretch where nothing moves, those in-between frames are 18-20 bytes each ("repeat the
  last picture"), so they cannot contain the dashes. The file is sound.
- The same file played clean once and speckled every other time: a file cannot change between
  plays, so the dashes are made during playback, by the graphics hardware's video decoder.

Two traps that cost time, so they are written down:

- **Do not judge a file by decoding it on the PC that shows the fault.** Every convenient way of
  getting a frame back (`MediaComposition.GetThumbnailAsync`, a `MediaTranscoder` "software"
  transcode, a browser) went through the same hardware and showed dashes on sound files.
- **Check in-between moments, not whole seconds.** Key frames land on whole seconds at 20 fps, so
  sampling at 1.0 s, 2.0 s... always looks clean. The Viewer's check uses 0.15 s, 0.50 s, ...

`--allkey` works around it because key frames are the part that plays correctly. Whether other
people's PCs show the fault with the small file is **not known**; it has only been seen here.
Play the small file on a phone or another PC before choosing it for the website.

## Checking a finished video

```
dotnet run -c Release --project tools/DemoVideo/Viewer -- %TEMP%\kanban-demo
```

Open `http://127.0.0.1:8765/?f=<file.mp4>` and click **Check for specks**. It measures the title
card, which should be one flat colour, at moments between key frames. Clean means it plays
cleanly on this PC's hardware, nothing more. Stop the viewer with Ctrl+C.
