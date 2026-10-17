using System.Runtime.InteropServices;
using System.Text.Json;
using AlloyPackageConsumer;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Cobalt;
using TrueMoon.Extensions.DependencyInjection;

var backend = args.Length > 0 ? args[0] : "raster";
var reportPath = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
var provider = args.Length > 2 ? args[2] : "cobalt";
var frames = 0L;
var completed = false;
var generation = false;
var cleanup = false;
ConsumerModel? ownedModel = null;
HostedUiSessionFactory? ownedFactory = null;
Dictionary<string, string>? fontHashes = null;
string? failure = null;
try
{
    if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        throw new PlatformNotSupportedException("This package smoke requires Windows x64.");
    if (backend is not ("raster" or "opengl" or "vulkan"))
        throw new ArgumentException("Choose raster, opengl or vulkan.");
    if (provider is not ("cobalt" or "di")) throw new ArgumentException("Choose cobalt or di.");
#if COBALT_GENERATION
    var registration = typeof(RAlloyPackageConsumer.Cobalt.Generated.CobaltResolversRegistration);
    if (registration.Assembly != typeof(ConsumerView).Assembly
        || !ServiceResolvers.Shared.GetResolvers().ContainsKey(typeof(ConsumerModel))
        || !ServiceResolvers.Shared.GetResolvers().ContainsKey(typeof(ConsumerView)))
        throw new InvalidOperationException("Consumer generated graph was not published by its module initializer.");
    generation = true;
#else
    if (provider != "di") throw new InvalidOperationException("Cobalt requires consumer generation.");
#endif
    var nativePath = Path.Combine(AppContext.BaseDirectory, "truemoon-native", "win-x64", "libSkiaSharp.dll");
    if (!File.Exists(nativePath)) throw new FileNotFoundException("Transitive native Skia asset was not delivered.", nativePath);
    if (SkiaNativeLibrary.LoadedPath != null) throw new InvalidOperationException("Skia was initialized before App configuration.");
    fontHashes = ConsumerFonts.Fingerprints();
    // Loading explicit font snapshots validates them with native Skia before App configuration.
    var fonts = ConsumerFonts.Load();
    var builder = App.Builder(options => { if (provider == "di") options.UseDI(); });
    builder.Setup(context =>
    {
        if (backend == "raster")
            context.UseAlloy(options => options.UseSkiaRaster(fonts).UseExternalHost());
        else
            context.UsePresentation<ConsumerView>(options =>
            {
                if (backend == "vulkan") options.UseSkiaVulkan(fonts); else options.UseSkiaOpenGL(fonts);
                options.UseSilkWindow(window =>
                {
                    window.Title = $"TrueMoon NuGet — {provider}/{backend}";
                    window.Width = 480;
                    window.Height = 260;
                });
            });
        // Concrete registration gives the package analyzer the consumer's view constructor graph.
        context.Services(services => services.Singleton<ConsumerModel>().Transient<ConsumerView>());
    });
    await using (var app = builder.Build())
    {
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        var model = (ConsumerModel)app.Services.GetService(typeof(ConsumerModel))!;
        ownedFactory = factory;
        ownedModel = model;
        if (factory.ActiveSessionCount != 0 || model.SubscriptionCount != 0)
            throw new InvalidOperationException("App Build created UI sessions or bindings eagerly.");
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
                using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
                if (bitmap.Pixels.Distinct().Count() < 2) throw new InvalidOperationException("Raster UI did not draw content.");
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
    cleanup = ownedFactory.ActiveSessionCount == 0 && ownedModel.SubscriptionCount == 0 && ownedModel.DisposeCalls == 1;
    if (!cleanup) throw new InvalidOperationException("App disposal did not release its singleton model and UI bindings.");
    completed = true;
    Console.WriteLine($"Package consumer {provider}/{backend}: passed; frames={frames}; App startup/shutdown, binding, tree disposal, fonts and native delivery verified.");
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
            Completed = completed, Backend = backend, Provider = provider, Frames = frames, Failure = failure,
            ConsumerGeneration = generation, Cleanup = cleanup, Fonts = fontHashes,
            ModelDisposeCalls = ownedModel?.DisposeCalls, ActiveSessions = ownedFactory?.ActiveSessionCount,
            Subscriptions = ownedModel?.SubscriptionCount, ProcessId = Environment.ProcessId,
            NativeSkia = SkiaNativeLibrary.LoadedPath, BaseDirectory = AppContext.BaseDirectory,
            Framework = RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Assemblies = new[] { typeof(UiSession).Assembly, typeof(ConsumerView).Assembly, typeof(HostedUiWindow).Assembly }
                .Select(assembly => new { assembly.FullName, assembly.Location }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
