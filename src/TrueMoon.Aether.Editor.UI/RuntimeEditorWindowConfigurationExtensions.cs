using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Optional standard Runtime Editor window. Embedded hosts own their View/UI and call RuntimeEditorSession.Present.</summary>
public static class RuntimeEditorWindowConfigurationExtensions
{
    /// <summary>Creates the window at App startup; keeps the context alive until runtime surfaces drain.</summary>
    /// <param name="context">App after UseAetherEditorUi.</param><param name="fonts">Borrowed immutable fonts; no hidden file lookup.</param>
    /// <param name="configure">Explicit native window options, copied by Hosting.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorUiWindow(this IAppConfigurationContext context, SkiaFontCatalog fonts, Action<SilkUiWindowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(fonts);
        context.Services(services =>
        {
            if (!services.Exist<RuntimeEditorSession>()) throw new InvalidOperationException("Call UseAetherEditorUi before its window adapter.");
            if (services.Exist<RuntimeEditorWindow>()) throw new InvalidOperationException("Runtime editor window is already registered.");
            services.Singleton<RuntimeEditorWindow>(resolver => new(resolver.Resolve<HostedUiWindow>(), resolver.Resolve<RuntimeEditorSession>()));
            services.Singleton<IStartable>(resolver => new WindowLifetime(resolver.Resolve<RuntimeEditorWindow>()));
            services.Singleton<IStoppable>(resolver => new WindowLifetime(resolver.Resolve<RuntimeEditorWindow>()));
        });
        context.UsePresentationFactory<EditorView>(presentation => presentation.UseSkiaOpenGL(fonts).UseSilkWindow(window =>
        {
            window.Width = 1280; window.Height = 800; window.Title = "TrueMoon Aether Runtime"; window.FrameMode = UiFrameMode.Continuous;
            configure?.Invoke(window);
        }));
        context.Services(services => services.Singleton<IStartable>(resolver => new ReadyLifetime(resolver.Resolve<RuntimeEditorWindow>())));
        return context;
    }
    /// <summary>Uses Editor:RuntimeWindow through the common window schema; fonts and native callbacks stay explicit.</summary>
    public static IAppConfigurationContext UseConfiguredAetherEditorUiWindow(this IAppConfigurationContext context, SkiaFontCatalog fonts, Action<WindowSettings>? configure = null)
        => ConfigurationModuleBinding.Configure(context, EditorUiConfigurationSchemas.Window, (app, settings) => app.UseAetherEditorUiWindow(fonts, settings.ApplyTo), configure);
    private sealed class WindowLifetime(RuntimeEditorWindow window) : IStartable, IStoppable
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => window.StartAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default) => window.StopAsync();
    }
    private sealed class ReadyLifetime(RuntimeEditorWindow window) : IStartable
    { public Task StartAsync(CancellationToken cancellationToken = default) => window.WaitReadyAsync(cancellationToken); }
}
