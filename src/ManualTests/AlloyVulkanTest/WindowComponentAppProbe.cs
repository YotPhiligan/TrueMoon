using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

internal static class WindowComponentAppProbe
{
    internal static async Task<object[]> RunAsync(CancellationToken token)
    {
        var reports = new List<object>();
        foreach (var provider in new[] { "cobalt", "di" })
        {
            var created = 0; AppWindow? view = null; Model? model = null;
            var builder = App.Builder(options => { if (provider == "di") options.UseDI(); });
            builder.Setup(context =>
            {
                context.Services(services => services.Singleton<Model>(_ => new Model())
                    .Singleton<IViewStyle<AppWindow>>(_ => new AppWindowStyle())
                    .Singleton<IFactory<AppWindow>>(resolver => new Factory<AppWindow>(() =>
                    {
                        Interlocked.Increment(ref created);
                        return new AppWindow(resolver.Resolve<Model>(), resolver.Resolve<IViewStyle<AppWindow>>());
                    })));
                context.UsePresentationFactory<AppWindow>(options => options.UseSkiaOpenGL().UseSilkWindow(window =>
                {
                    window.Appearance = new WindowAppearance { Decorated = false };
                    window.Chrome = new WindowChromeOptions { NativeDrag = true };
                    window.FrameMode = UiFrameMode.Continuous;
                }));
            });
            await using (var app = builder.Build())
            {
                var host = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
                var sessions = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
                model = (Model)app.Services.GetService(typeof(Model))!;
                Require(created == 0 && sessions.ActiveSessionCount == 0, "App Build created a Window eagerly.");
                Task<bool>? close = null;
                host.FramePresented += ui =>
                {
                    view = (AppWindow)((WindowFrame)ui.Root).Content;
                    Require(ReferenceEquals(view.DataContext, model) && ReferenceEquals(view.Child, view.Input), "Factory/data/content identity changed.");
                    Require(Environment.CurrentManagedThreadId == view.Owner && view.IsHosted, "Factory did not create on the UI owner.");
                    Require(view.Background == new Color(22, 44, 66), "Injected scoped Window style was not applied.");
                    if (host.PresentedFrames >= 3 && close == null) close = view.RequestCloseAsync(token);
                };
                await app.StartAsync(token);
                await host.Completion.WaitAsync(token);
                Require(close != null && await close, "Factory Window close did not await cleanup.");
                Require(created == 1 && sessions.ActiveSessionCount == 0 && view is { IsDisposed: true, IsHosted: false }, "Factory product was not uniquely owned/cleaned.");
                Require(model.Disposals == 0, "Window disposed its borrowed model.");
                await app.StopAsync(token);
            }
            Require(model!.Disposals == 1, "App did not dispose its model exactly once.");
            reports.Add(new { Provider = provider, Passed = true, FactoryCreates = created, ModelDisposals = model.Disposals });
        }
        return reports.ToArray();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Model : IDisposable
    {
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
    private sealed class AppWindow : Window<Model>
    {
        internal int Owner { get; } = Environment.CurrentManagedThreadId;
        internal TextBox Input { get; } = new() { Value = "factory-owned Window" };
        internal AppWindow(Model data, IViewStyle<AppWindow> style)
        {
            this.WithTitle("App Window factory").WithClientSize(420, 260);
            Content(data, _ => Input); this.WithViewStyle(style);
        }
    }
    private sealed class AppWindowStyle : IViewStyle<AppWindow>
    {
        public void Configure(IViewStyleBuilder<AppWindow> style) => style.Root().Background(new Color(22, 44, 66));
    }
}
