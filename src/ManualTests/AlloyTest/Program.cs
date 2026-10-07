using AlloyTest;
using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

if (args.Contains("--window-lifetime"))
{
    WindowLifetimeProbe.Run();
    return;
}

Action<IAppConfigurationContext> configure = context => context
        .UseDiagnostics(configuration => configuration
            .OnEvent(@event => Console.WriteLine($"{@event}"))
        )
        .UsePresentation<View1>(options => options.UseSkiaOpenGL().UseSilkWindow())
        .Services(services => services.Singleton<SettingsModel>());

var builder = App.Builder(t => t.UseDI());
if (!args.Contains("--smoke"))
{
    await builder.RunAsync(configure);
    return;
}

builder.Setup(configure);
await using var app = builder.Build();
var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
var smoke = new SettingsFormSmoke();
window.FramePresented += ui =>
{
    smoke.Frame(ui);
    if (window.PresentedFrames >= 30) window.Close();
};
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await app.StartAsync(timeout.Token);
await window.Completion.WaitAsync(timeout.Token);
await app.StopAsync(timeout.Token);
if (window.PresentedFrames < 30) throw new InvalidOperationException("Expected at least 30 presented frames.");
smoke.VerifyDisposed();
Console.WriteLine($"AlloyTest settings form OpenGL/Silk smoke passed: {window.PresentedFrames} frames; editing, model updates, reset, retained focus/selection and disposal verified.");
