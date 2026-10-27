using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Optional standard Windows/OpenGL editor window integration.</summary>
public static class EditorWindowConfigurationExtensions
{
    /// <summary>Registers an App-owned window and connects presentation and Save/Discard/Cancel close policy.</summary>
    /// <param name="context">Consumer composition after UseAetherEditor.</param>
    /// <param name="fonts">Host-selected immutable font catalog. The editor package performs no font file lookup.</param>
    /// <param name="configure">Optional overrides for the standard window settings.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Window creation is deferred to App startup. Custom/embedded hosts instead use IFactory&lt;EditorView&gt;
    /// and EditorSession.Present/RequestCloseAsync on their UI owner. They own the view and UI session.</remarks>
    public static IAppConfigurationContext UseAetherEditorWindow(this IAppConfigurationContext context,
        SkiaFontCatalog fonts, Action<SilkUiWindowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(fonts);
        context.Services(services =>
        {
            if (!services.Exist<EditorSession>()) throw new InvalidOperationException("Call UseAetherEditor before UseAetherEditorWindow.");
            if (services.Exist<EditorWindow>()) throw new InvalidOperationException("Editor window is already registered.");
            services.Singleton<EditorWindow>(r => new(r.Resolve<HostedUiWindow>(), r.Resolve<EditorSession>(),
                    r.Resolve<EditorWorkspace>(), r.Resolve<EditorViewportBinding>(), r.Resolve<AetherHost>()))
                .Singleton<WindowLifetime>(r => new(r.Resolve<EditorWindow>()))
                .Singleton<IStartable>(r => r.Resolve<WindowLifetime>())
                .Singleton<IStoppable>(r => r.Resolve<WindowLifetime>());
        });
        IFactory<EditorHeaderView, EditorHeaderArguments>? headers = null;
        context.UsePresentation(resolver =>
        {
            headers = resolver.Resolve<IFactory<EditorHeaderView, EditorHeaderArguments>>();
            return resolver.Resolve<IFactory<EditorView>>().Create()
                ?? throw new InvalidOperationException("Editor view factory returned null.");
        }, presentation => presentation.UseSkiaOpenGL(fonts).UseSilkWindow(window =>
        {
            window.Width = 1280; window.Height = 800; window.Title = "TrueMoon Aether Editor";
            window.Appearance = new WindowAppearance { Decorated = false };
            window.TitleBarOverContent = true; window.UseSystemAccentBorder = true;
            window.Chrome = new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = true, MinimumSize = new Size(1100, 700) };
            window.FrameMode = UiFrameMode.Continuous;
            window.ConfigureDefaultTitleBar = (title, content, requestClose) =>
            {
                var view = (EditorView)content;
                var header = headers!.Create(new(view.Overlays, title.Title, requestClose))
                    ?? throw new InvalidOperationException("Editor header factory returned null.");
                try { title.SetHeader(header); view.Header = header; }
                catch { if (header.Parent == null && !header.IsDisposed) header.Dispose(); throw; }
            };
            configure?.Invoke(window);
        }));
        // Wait only after HostedUiWindow starts. App rollback also runs WindowLifetime
        // when readiness fails after native window creation.
        context.Services(services => services.Singleton<IStartable>(r => new ReadyLifetime(r.Resolve<EditorWindow>())));
        return context;
    }

    // Neither alias is disposable: EditorWindow alone owns the subscriptions.
    private sealed class WindowLifetime(EditorWindow window) : IStartable, IStoppable
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => window.StartAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default) => window.StopAsync();
    }

    private sealed class ReadyLifetime(EditorWindow window) : IStartable
    { public Task StartAsync(CancellationToken cancellationToken = default) => window.WaitReadyAsync(cancellationToken); }
}
