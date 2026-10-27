using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Connects the reusable editor to the consumer's App, without creating an App or opening native resources.</summary>
public static class AetherEditorConfigurationExtensions
{
    /// <summary>Registers the engine, project/document coordination, presentation models, styles and parent-owned view factories.</summary>
    /// <param name="context">Consumer App composition.</param>
    /// <param name="configureEngine">Explicit engine identity, runtime factory and report paths. Defaults to the editor GPU viewport.</param>
    /// <param name="configure">Optional initial project selection, copied and validated during setup.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Attach once to a fresh engine/editor graph. Custom stores, styles, root model and root view factory registered
    /// beforehand are preserved. Registration and empty-workspace startup perform no SDK/compiler/window/game work.</remarks>
    public static IAppConfigurationContext UseAetherEditor(this IAppConfigurationContext context,
        Action<AetherOptions> configureEngine, Action<AetherEditorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(configureEngine);
        var options = new AetherEditorOptions(); configure?.Invoke(options); var snapshot = options.Snapshot();
        var defaultViewport = true;
        context.Services(services =>
        {
            if (services.Exist<EditorSession>() || services.Exist<EditorProjects>() || services.Exist<AetherHost>())
                throw new InvalidOperationException("Aether editor requires one engine/editor registration per App.");
            services.Singleton<EditorViewportBinding>(_ => new(defaultViewport));
        });
        context.UseAether(engine =>
        {
            configureEngine(engine);
            defaultViewport = engine.ViewportTransport == null && engine.ViewportTransportFactory == null;
            if (defaultViewport)
                engine.ViewportTransportFactory = resolver => resolver.Resolve<EditorViewportBinding>();
        });
        context.UseAetherAuthoring();
        context.UseAetherEditorDocuments();
        context.UseAetherEditorProjects();
        context.Services(services =>
        {
            if (!services.Exist<EditorWorkspace>()) services.Singleton<EditorWorkspace>(_ => new(
                new SceneSnapshot(snapshot.ProjectPath == null ? "Проект не открыт" : "Сцена не открыта", 0, []), snapshot.ProjectPath != null));
            if (!services.Exist<EditorPreview>()) services.Singleton<EditorPreview>(resolver =>
            {
                var engine = resolver.Resolve<AetherHost>(); var viewport = resolver.Resolve<EditorViewportBinding>();
                return new(async (scene, token) =>
                {
                    await viewport.Ready.WaitAsync(token).ConfigureAwait(false);
                    return await engine.Preview.ShowAsync(scene, 640, 360, token).ConfigureAwait(false);
                }, engine.Preview.StopAsync);
            });
            if (!services.Exist<EditorCommands>()) services.Singleton<EditorCommands>(resolver => new(
                resolver.Resolve<AetherHost>().Editor, resolver.Resolve<EditorViewportBinding>().Ready,
                resolver.Resolve<IAppLifetime>().AppCancellationToken));
            if (!services.Exist<EditorDocumentCommands>()) services.Singleton<EditorDocumentCommands>(resolver =>
            {
                var engine = resolver.Resolve<AetherHost>(); var commands = resolver.Resolve<EditorCommands>();
                var preview = resolver.Resolve<EditorPreview>();
                return new(resolver.Resolve<EditorProjects>(), () => commands.Pending.IsCompleted &&
                    (engine.Supervisor.Status.State == SessionState.Stopped || preview.Read().Active) &&
                    engine.Editor.Status.Operation == TrueMoon.Aether.Editing.GameEditorOperation.Idle,
                    commands.StopAsync, resolver.Resolve<IAppLifetime>().AppCancellationToken, preview.StopAsync);
            });
            if (!services.Exist<EditorDocumentViewModel>()) services.Singleton<EditorDocumentViewModel>(r => new(r.Resolve<EditorDocumentCommands>()));
            if (!services.Exist<IViewStyle<EditorDocumentView>>()) services.Singleton<IViewStyle<EditorDocumentView>>(_ => new EditorDocumentViewStyle());
            if (!services.Exist<IFactory<EditorDocumentView>>()) services.Singleton<IFactory<EditorDocumentView>>(r =>
                new Factory<EditorDocumentView>(() => new(r.Resolve<EditorDocumentViewModel>(), r.Resolve<IViewStyle<EditorDocumentView>>(), compactHeader: true)));
            if (!services.Exist<IViewStyle<EditorView>>()) services.Singleton<IViewStyle<EditorView>>(_ => new EditorViewStyle());
            if (!services.Exist<EditorViewModel>()) services.Singleton<EditorViewModel>(r => new(r.Resolve<EditorWorkspace>(), r.Resolve<EditorCommands>(),
                r.Resolve<EditorDocumentViewModel>(), r.Resolve<EditorInspectorViewModel>(), r.Resolve<EditorHierarchyViewModel>()));
            if (!services.Exist<IFactory<EditorView>>()) services.Singleton<IFactory<EditorView>>(r =>
                new Factory<EditorView>(() => new(r.Resolve<EditorViewModel>(), r.Resolve<IViewStyle<EditorView>>(),
                    r.Resolve<IFactory<EditorDocumentView>>(), null, true, r.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>(),
                    r.Resolve<IFactory<EditorHierarchyView>>(), r.Resolve<IFactory<EditorInspectorView>>())));
        });
        context.UseAetherEditorEditing(previewInStatusBar: true);
        context.UseAetherEditorHierarchy(); context.UseAetherEditorHeader(); context.UseAetherEditorStatusBar();
        context.Services(services => services.Singleton<EditorSession>(r => new(snapshot, r.Resolve<AetherHost>(), r.Resolve<EditorProjects>(),
                r.Resolve<EditorWorkspace>(), r.Resolve<EditorCommands>(), r.Resolve<EditorDocumentCommands>(), r.Resolve<EditorViewModel>(), r.Resolve<EditorViewportBinding>()))
            .Singleton<IStartable>(r => r.Resolve<EditorSession>()).Singleton<IStoppable>(r => r.Resolve<EditorSession>()));
        return context;
    }
}
