using System.Runtime.InteropServices;
using System.Text.Json;
using RichTextEditorTest;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

var backend = args.Length > 0 ? args[0] : "opengl";
var reportPath = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
var smoke = reportPath != null;
var completed = false;
string? failure = null;
var frames = 0L;
try
{
    if (backend is not ("raster" or "opengl" or "vulkan")) throw new ArgumentException("Choose raster, opengl or vulkan.");
    var builder = App.Builder(_ => { }).Setup(app =>
    {
        if (backend == "raster") app.UseAlloy(options => options.UseSkiaRaster().UseExternalHost());
        else app.UsePresentation<EditorView>(options =>
        {
            if (backend == "vulkan") options.UseSkiaVulkan(); else options.UseSkiaOpenGL();
            options.UseSilkWindow(window => { window.Width = 900; window.Height = 560; window.Title = "TrueMoon RichTextEditor"; });
        });
        app.Services(services => services.Transient<EditorView>());
    });
    await using (var app = builder.Build())
    {
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(smoke ? 40 : 3600));
        if (backend == "raster")
        {
            await app.StartAsync(timeout.Token);
            var view = new EditorView();
            using (var ui = factory.Create(view, new RasterUiTarget(), new(900, 560)))
            {
                for (var frame = 0; frame < 14; frame++)
                {
                    ui.Update();
                    if (frame == 0 && reportPath != null)
                    {
                        var initialPath = Path.ChangeExtension(reportPath, ".initial.png");
                        Directory.CreateDirectory(Path.GetDirectoryName(initialPath)!);
                        using var initial = ui.SnapshotRasterImage();
                        using var encoded = initial.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                        using var initialOutput = File.Create(initialPath); encoded.SaveTo(initialOutput);
                    }
                    view.VerifyFrame(); frames++;
                }
                ui.Update();
                using var image = ui.SnapshotRasterImage();
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                var output = Path.ChangeExtension(reportPath ?? Path.Combine(AppContext.BaseDirectory, "richtext.json"), ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                using var file = File.Create(output); data.SaveTo(file);
                if (ui.Update()) throw new InvalidOperationException("Static rich editor redrew.");
            }
            view.VerifyClosed();
        }
        else
        {
            var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
            EditorView? view = null;
            window.FramePresented += ui =>
            {
                view = (EditorView)ui.Root;
                if (smoke) { view.VerifyFrame(); if (window.PresentedFrames >= 20) window.Close(); }
            };
            await app.StartAsync(timeout.Token);
            await window.Completion.WaitAsync(timeout.Token);
            frames = window.PresentedFrames;
            if (smoke) (view ?? throw new InvalidOperationException("View was not created.")).VerifyClosed();
        }
        await app.StopAsync(timeout.Token);
        if (factory.ActiveSessionCount != 0) throw new InvalidOperationException("Hosting retained sessions.");
    }
    completed = true;
    Console.WriteLine($"RichTextEditor {backend}: passed; frames={frames}.");
}
catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); Environment.ExitCode = 1; }
finally
{
    if (reportPath != null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            Completed = completed, Backend = backend, Frames = frames, Failure = failure,
            Runtime = RuntimeInformation.FrameworkDescription, Native = SkiaNativeLibrary.LoadedPath,
            RichTextKit = typeof(Topten.RichTextKit.TextBlock).Assembly.FullName
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
