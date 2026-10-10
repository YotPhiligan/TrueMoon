using AlloyTest;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

if (args.Contains("--collection-failure"))
{
    await CollectionFailureProbe.RunAsync();
    return;
}

if (args.Contains("--window-lifetime"))
{
    WindowLifetimeProbe.Run();
    return;
}

var clipboardSmoke = args.Contains("--smoke-clipboard");
var clipboardFailureSmoke = args.Contains("--smoke-clipboard-failure");
var traceIndex = Array.IndexOf(args, "--smoke-trace");
if (traceIndex >= 0 && (traceIndex + 1 == args.Length || args[traceIndex + 1].StartsWith("--", StringComparison.Ordinal)))
    throw new ArgumentException("--smoke-trace requires a JSON output path.");
var tracePath = traceIndex >= 0 ? args[traceIndex + 1] : null;
if (tracePath != null && !args.Contains("--smoke") && !clipboardSmoke && !clipboardFailureSmoke)
    throw new ArgumentException("--smoke-trace requires a smoke mode.");
if (clipboardSmoke && clipboardFailureSmoke) throw new ArgumentException("Choose one clipboard smoke mode.");
var clipboardFailures = clipboardFailureSmoke ? new ClipboardFailureSmoke() : null;
var useVulkan = args.Contains("--vulkan");
var validation = args.Contains("--validation");
var customFrame = args.Contains("--custom-frame");
var opacityIndex = Array.IndexOf(args, "--opacity");
if (opacityIndex >= 0 && opacityIndex + 1 == args.Length) throw new ArgumentException("--opacity requires a value from zero to one.");
if (opacityIndex >= 0 && args.Contains("--transparent")) throw new ArgumentException("Choose --opacity or --transparent.");
var appearance = new WindowAppearance
{
    Transparency = args.Contains("--transparent") ? WindowTransparencyMode.PerPixel
        : opacityIndex >= 0 ? WindowTransparencyMode.Opacity : WindowTransparencyMode.Opaque,
    Opacity = opacityIndex >= 0 ? float.Parse(args[opacityIndex + 1], System.Globalization.CultureInfo.InvariantCulture) : 1,
    Decorated = !args.Contains("--borderless") && !customFrame
};
appearance.Validate();
if (validation && !useVulkan) throw new ArgumentException("--validation requires --vulkan.");
var validationMessages = 0;
Action<IAppConfigurationContext> configure = context => context
        .UseDiagnostics(configuration => configuration
            .OnEvent(@event => Console.WriteLine($"{@event}"))
        )
        .UsePresentation<View1>(options =>
        {
            if (useVulkan) options.UseSkiaVulkan(); else options.UseSkiaOpenGL();
            options.UseSilkWindow(window =>
            {
                window.Width = 900; window.Height = 960; window.Title = "TrueMoon Alloy — настройки";
                window.Appearance = appearance;
                if (customFrame)
                {
                    window.Chrome = new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = true, MinimumSize = new Size(330, 300) };
                    window.TitleBarFactory = commands => new CustomTitleBar(commands);
                }
                if (validation) window.ValidationMessage = message =>
                { Interlocked.Increment(ref validationMessages); Console.Error.WriteLine(message); };
            });
        })
        .Services(services =>
        {
            services.Singleton<SettingsModel>();
            if (clipboardFailures != null)
                services.Singleton<IUiSessionFactory>(resolver => new ClipboardFailureSmoke.Sessions(resolver.Resolve<HostedUiSessionFactory>(), clipboardFailures));
        });

var builder = App.Builder(t => t.UseDI());
if (!args.Contains("--smoke") && !clipboardSmoke && !clipboardFailureSmoke)
{
    await builder.RunAsync(configure);
    return;
}

builder.Setup(configure);
await using var app = builder.Build();
var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
var smoke = new SettingsFormSmoke(clipboardSmoke, tracePath != null);
if (tracePath != null) window.PostWindow(host => host.Input += smoke.ObserveNativeInput);
var opacityChanged = false;
window.FramePresented += ui =>
{
    smoke.Frame(ui);
    clipboardFailures?.Frame(ui);
    if (appearance.Transparency == WindowTransparencyMode.Opacity && window.PresentedFrames == 20)
    {
        window.SetOpacity(.75f);
        window.PostWindow(host =>
        {
            if (Math.Abs(host.Opacity - .75f) > .005f) throw new InvalidOperationException("Queued opacity change did not reach the native window.");
            opacityChanged = true;
        });
    }
    if (window.PresentedFrames >= 40) window.Close();
};
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
Exception? smokeFailure = null;
try
{
    await app.StartAsync(timeout.Token);
    if (window.AppearanceCapabilities == null || window.SupportsPerPixelTransparency != (appearance.Transparency == WindowTransparencyMode.PerPixel))
        throw new InvalidOperationException("Hosted appearance capabilities mismatch.");
    await window.Completion.WaitAsync(timeout.Token);
    await app.StopAsync(timeout.Token);
    if (window.PresentedFrames < 40) throw new InvalidOperationException("Expected at least 40 presented frames.");
    if (appearance.Transparency == WindowTransparencyMode.Opacity && !opacityChanged) throw new InvalidOperationException("Expected a queued opacity update.");
    smoke.VerifyDisposed();
    clipboardFailures?.Verify((HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!);
    if (validationMessages != 0) throw new InvalidOperationException($"Standalone settings Vulkan validation reported {validationMessages} warning/error messages.");
    Console.WriteLine($"AlloyTest interactions {(useVulkan ? "Vulkan" : "OpenGL")}/Silk smoke passed: {window.PresentedFrames} frames; form/list/regions, themes/density, disabled/input, wheel/focus scrolling, image/meter, static redraw and disposal verified; OS clipboard: {(clipboardSmoke ? "verified" : "not requested")}.");
    if (validation) Console.WriteLine("Standalone settings Vulkan validation: 0 errors, 0 warnings, including teardown.");
    if (clipboardFailureSmoke) Console.WriteLine("Hosted window clipboard failure/recovery passed with controlled service: copy/cut/paste errors reported, editor/window retained, all retries recovered, registry zero; OS clipboard not accessed.");
}
catch (Exception error) { smokeFailure = error; throw; }
finally
{
    if (tracePath != null)
        try { smoke.WriteTrace(tracePath); }
        catch (Exception error) when (smokeFailure != null)
        { Console.Error.WriteLine($"Smoke trace could not be saved: {error}"); }
}
