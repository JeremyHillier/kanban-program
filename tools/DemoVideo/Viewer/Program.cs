// Serves one folder on this PC only (127.0.0.1:8765), so a finished demo video can be played and
// checked in a browser:   dotnet run --project tools/DemoVideo/Viewer -- <folder>
// then open http://127.0.0.1:8765/?f=<file.mp4>. "Check for specks" measures the title card, which
// should be one flat colour, at moments BETWEEN key frames - where the playback fault described in
// the README shows up. A clean result only says the video plays cleanly on THIS PC's hardware.
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:8765");
var app = builder.Build();
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root), ServeUnknownFileTypes = true, ContentTypeProvider = new FileExtensionContentTypeProvider() });
app.MapGet("/", () => Results.Content("""
    <html><body style="margin:0;font-family:Segoe UI,sans-serif;background:#111;color:#eee">
    <video id="v" style="width:100vw;max-height:85vh" controls muted></video>
    <p style="margin:8px 16px"><button id="go">Check for specks</button> <span id="out"></span></p>
    <script>
      const p = new URLSearchParams(location.search);
      const v = document.getElementById('v');
      v.src = '/' + (p.get('f') || 'video.mp4');
      document.getElementById('go').onclick = async () => {
        const out = document.getElementById('out'); out.textContent = 'checking...';
        const c = document.createElement('canvas'); c.width = v.videoWidth; c.height = v.videoHeight;
        const g = c.getContext('2d', { willReadFrequently: true });
        let specks = 0, moments = 0;
        for (let t = 0.15; t < 2.4; t += 0.35) {
          v.currentTime = t; await new Promise(r => v.addEventListener('seeked', r, { once: true }));
          g.drawImage(v, 0, 0); moments++;
          const d = g.getImageData(0, 0, c.width, Math.round(c.height * 0.37)).data;
          for (let i = 0; i < d.length; i += 4) if (d[i] > 200 && d[i + 1] > 200 && d[i + 2] > 200) specks++;
        }
        out.textContent = `${moments} moments of the title card: ${specks} stray bright pixels ` + (specks === 0 ? '(clean)' : '(SPECKS)');
      };
    </script></body></html>
    """, "text/html"));
app.Run();
