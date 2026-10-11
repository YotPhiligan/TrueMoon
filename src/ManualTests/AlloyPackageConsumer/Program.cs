using System.Runtime.InteropServices;
using System.Text.Json;
using AlloyPackageConsumer;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Extensions.DependencyInjection;

var backend = args.Length > 0 ? args[0] : "raster";
var reportPath = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
var frames = 0L;
var completed = false;
string? failure = null;
try
{
    if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        throw new PlatformNotSupportedException("This package smoke requires Windows x64.");
    if (backend is not ("raster" or "opengl" or "vulkan"))
        throw new ArgumentException("Choose raster, opengl or vulkan.");
    var nativePath = Path.Combine(AppContext.BaseDirectory, "truemoon-native", "win-x64", "libSkiaSharp.dll");
    if (!File.Exists(nativePath)) throw new FileNotFoundException("Transitive native Skia asset was not delivered.", nativePath);
    if (SkiaNativeLibrary.LoadedPath != null) throw new InvalidOperationException("Skia was initialized before App configuration.");
    var builder = App.Builder(options => options.UseDI());
    builder.Setup(context =>
    {
        if (backend == "raster")
            context.UseAlloy(options => options.UseSkiaRaster().UseExternalHost());
        else
            context.UsePresentation<ConsumerView>(options =>
            {
                if (backend == "vulkan") options.UseSkiaVulkan(); else options.UseSkiaOpenGL();
                options.UseSilkWindow(window =>
                {
                    window.Title = $"TrueMoon NuGet — {backend}";
                    window.Width = 480;
                    window.Height = 260;
                });
            });
        context.Services(services => services.Singleton<ConsumerModel>());
    });
    await using (var app = builder.Build())
    {
        if (SkiaNativeLibrary.LoadedPath != null) throw new InvalidOperationException("App Build allocated native Skia eagerly.");
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        var model = (ConsumerModel)app.Services.GetService(typeof(ConsumerModel))!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if (backend == "raster")
        {
            await app.StartAsync(timeout.Token);
            var view = new ConsumerView(model);
            // All session calls stay on this thread, with no await inside the ownership scope.
            using (var ui = factory.Create(view, new RasterUiTarget(), new UiViewport(480, 260, 1)))
            {
                for (var i = 0; i < 6; i++) { ui.Update(); view.VerifyFrame(ui); frames++; }
                ui.Update();
                using var image = ui.SnapshotRasterImage();
                if (image.Width != 480 || image.Height != 260) throw new InvalidOperationException("Raster output dimensions are incorrect.");
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                if (data == null || data.Size == 0) throw new InvalidOperationException("Raster PNG export failed.");
                var imagePath = reportPath == null ? Path.Combine(AppContext.BaseDirectory, "raster.png") : Path.ChangeExtension(reportPath, ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
                using var output = File.Create(imagePath);
                data.SaveTo(output);
                if (ui.Update()) throw new InvalidOperationException("Static UI unexpectedly redrew.");
            }
            view.VerifyClosed();
        }
        else
        {
            var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
            ConsumerView? view = null;
            window.FramePresented += ui =>
            {
                view = (ConsumerView)ui.Root;
                view.VerifyFrame(ui);
                if (window.PresentedFrames >= 20) window.Close();
            };
            await app.StartAsync(timeout.Token);
            await window.Completion.WaitAsync(timeout.Token);
            frames = window.PresentedFrames;
            if (frames < 20 || view == null) throw new InvalidOperationException("Standalone window did not complete presentation.");
            view.VerifyClosed();
        }
        await app.StopAsync(timeout.Token);
        if (factory.ActiveSessionCount != 0 || model.SubscriptionCount != 0)
            throw new InvalidOperationException("App shutdown retained UI sessions or bindings.");
        if (!string.Equals(SkiaNativeLibrary.LoadedPath, nativePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Renderer did not load the native Skia shipped next to the consumer.");
        if (backend == "vulkan") SkiaNativeLibrary.VerifyVulkanInterop();
    }
    completed = true;
    Console.WriteLine($"Package consumer {backend}: passed; frames={frames}; App startup/shutdown, binding, tree disposal and native delivery verified.");
}
catch (Exception error)
{
    failure = error.ToString();
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
finally
{
    if (reportPath != null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            Completed = completed, Backend = backend, Frames = frames, Failure = failure,
            NativeSkia = SkiaNativeLibrary.LoadedPath, BaseDirectory = AppContext.BaseDirectory,
            Framework = RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Assemblies = new[] { typeof(UiSession).Assembly, typeof(ConsumerView).Assembly, typeof(HostedUiWindow).Assembly }
                .Select(assembly => new { assembly.FullName, assembly.Location }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
