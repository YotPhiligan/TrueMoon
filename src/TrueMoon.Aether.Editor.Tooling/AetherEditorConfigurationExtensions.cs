using TrueMoon.Argentis.Styling;
using TrueMoon.Aether.Editor.Tooling.Diagnostics;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.Tooling.Hierarchy;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Diagnostics;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Aether.Importing;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Aether.Tooling.SourceTracking;
using TrueMoon.Argentis;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Aether.Editor.Tooling;

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
        => UseAetherEditor(context, configureEngine, configure, null);

    /// <summary>Registers Editor with explicit authoring limits while preserving the existing resource owners.</summary>
    /// <param name="context">App context.</param><param name="configureEngine">Engine composition.</param>
    /// <param name="configure">Initial Editor data.</param><param name="configureAuthoring">Authoring limits.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditor(this IAppConfigurationContext context,
        Action<AetherOptions> configureEngine, Action<AetherEditorOptions>? configure, Action<SceneAuthoringOptions>? configureAuthoring)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(configureEngine);
        var options = new AetherEditorOptions(); configure?.Invoke(options); var snapshot = options.Snapshot();
        var defaultViewport = true;
        context.Services(services =>
        {
            if (services.Exist<EditorSession>() || services.Exist<EditorProjects>() || services.Exist<AetherHost>())
                throw new InvalidOperationException("Aether editor requires one engine/editor registration per App.");
            services.Singleton<RuntimeViewportBinding>(_ => new(defaultViewport));
            if (!services.Exist<Func<ReadOnlyMemory<byte>, BaseColorTextureDefinition>>())
                services.Singleton<Func<ReadOnlyMemory<byte>, BaseColorTextureDefinition>>(_ => DecodeProjectTexture);
        });
        context.UseAether(engine =>
        {
            configureEngine(engine);
            defaultViewport = engine.ViewportTransport == null && engine.ViewportTransportFactory == null;
            if (defaultViewport)
                engine.ViewportTransportFactory = resolver => resolver.Resolve<RuntimeViewportBinding>();
        });
        context.UseAetherAuthoring(configureAuthoring);
        context.UseAetherEditorDocuments();
        context.Services(services =>
        {
            if (!services.Exist<IFactory<EditorSourceWatch, ResolvedProject>>())
            {
                var clock = services.Exist<TimeProvider>();
                services.Singleton<IFactory<EditorSourceWatch, ResolvedProject>>(resolver =>
                    new EditorSourceWatchFactory(snapshot.SourceTracking, clock ? resolver.Resolve<TimeProvider>() : TimeProvider.System));
            }
        });
        context.UseAetherEditorProjects();
        context.EnsureAetherEditorDiagnostics();
        context.Services(services =>
        {
            if (!services.Exist<EditorWorkspace>()) services.Singleton<EditorWorkspace>(_ => new(
                new SceneSnapshot(snapshot.ProjectPath == null ? "Проект не открыт" : "Сцена не открыта", 0, []), snapshot.ProjectPath != null));
            if (!services.Exist<EditorViewportState>()) services.Singleton<EditorViewportState>(r => new(r.Resolve<EditorWorkspace>(),r.Resolve<AetherHost>().Preview));
            if (!services.Exist<EditorPreview>()) services.Singleton<EditorPreview>(resolver =>
            {
                var engine = resolver.Resolve<AetherHost>(); var viewport = resolver.Resolve<RuntimeViewportBinding>();
                if (!defaultViewport)
                    return new EditorPreview((scene, token) => engine.Preview.ShowAsync(scene, 640, 360, token), engine.Preview.StopAsync);
                var state = resolver.Resolve<EditorViewportState>();
                return new(state.CaptureSource, state.SetDesiredSource, state.CanReuse, async (source, view, token) =>
                {
                    await viewport.Ready.WaitAsync(token).ConfigureAwait(false);
                    while(true)
                    {
                        await state.VisibleReady.WaitAsync(token).ConfigureAwait(false);
                        var visibility=state.VisibilityToken;
                        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,visibility);
                        try{return await engine.Preview.ShowCapturedViewAsync(source,view,640,360,linked.Token).ConfigureAwait(false);}
                        catch(OperationCanceledException)when(!token.IsCancellationRequested&&visibility.IsCancellationRequested) { }
                    }
                }, engine.Preview.StopAsync);
            });
            if (!services.Exist<EditorCommands>()) services.Singleton<EditorCommands>(resolver => new(
                resolver.Resolve<AetherHost>().Editor, resolver.Resolve<RuntimeViewportBinding>().Ready,
                resolver.Resolve<IAppLifetime>().AppCancellationToken));
            if (!services.Exist<EditorDocumentCommands>()) services.Singleton<EditorDocumentCommands>(resolver =>
            {
                var engine = resolver.Resolve<AetherHost>(); var commands = resolver.Resolve<EditorCommands>();
                var preview = resolver.Resolve<EditorPreview>();
                return new(resolver.Resolve<EditorProjects>(), () => commands.Pending.IsCompleted &&
                    (engine.Supervisor.Status.State == SessionState.Stopped || preview.Read().Active) &&
                    engine.Editor.Status.Operation == GameEditorOperation.Idle,
                    commands.StopAsync, resolver.Resolve<IAppLifetime>().AppCancellationToken, preview.StopAsync,
                    project =>
                    {
                        if (project == null) engine.Editor.ClearContext();
                        else engine.Editor.SetContext(new(project.CreateBuildRequest(0), new SceneSnapshot("Сцена не открыта", 0, []), ""));
                    }, () => commands.Pending.IsCompleted && engine.Supervisor.Status.State == SessionState.Playing &&
                             engine.Editor.Status.Operation == GameEditorOperation.Idle);
            });
            if (!services.Exist<EditorDocumentViewModel>()) services.Singleton<EditorDocumentViewModel>(r =>
                new(r.Resolve<EditorDocumentCommands>(), r.Resolve<EditorScenePlayback>(), r.Resolve<EditorInspectorViewModel>()));
            if (!services.Exist<EditorScenePlayback>()) services.Singleton<EditorScenePlayback>(r =>
            {
                var engine = r.Resolve<AetherHost>();
                return new(r.Resolve<EditorDocumentCommands>(), r.Resolve<EditorPreview>(), engine.Editor,
                    () => engine.Supervisor.Status, r.Resolve<RuntimeViewportBinding>().Ready, r.Resolve<IAppLifetime>().AppCancellationToken, snapshot);
            });
            if (!services.Exist<IViewStyle<EditorDocumentView>>()) services.Singleton<IViewStyle<EditorDocumentView>>(_ => new EditorDocumentViewStyle());
            if (!services.Exist<IFactory<EditorDocumentView>>()) services.Singleton<IFactory<EditorDocumentView>>(r =>
                new Factory<EditorDocumentView>(() => new(r.Resolve<EditorDocumentViewModel>(), r.Resolve<IViewStyle<EditorDocumentView>>(), compactHeader: true)));
            if (!services.Exist<IViewStyle<EditorView>>()) services.Singleton<IViewStyle<EditorView>>(_ => new EditorViewStyle());
            if (!services.Exist<EditorViewModel>()) services.Singleton<EditorViewModel>(r => new(r.Resolve<EditorWorkspace>(), r.Resolve<EditorCommands>(),
                r.Resolve<EditorDocumentViewModel>(), r.Resolve<EditorInspectorViewModel>(), r.Resolve<EditorHierarchyViewModel>(), r.Resolve<EditorScenePlayback>()));
            if (!services.Exist<IFactory<EditorView>>()) services.Singleton<IFactory<EditorView>>(r =>
                new Factory<EditorView>(() => new(r.Resolve<EditorViewModel>(), r.Resolve<IViewStyle<EditorView>>(),
                    r.Resolve<IFactory<EditorDocumentView>>(), null, true, r.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>(),
                    r.Resolve<IFactory<EditorHierarchyView>>(), r.Resolve<IFactory<EditorInspectorView>>(), r.Resolve<IFactory<EditorDiagnosticsView>>())));
        });
        context.UseAetherEditorEditing(previewInStatusBar: true);
        context.UseAetherEditorHierarchy(); context.UseAetherEditorHeader(); context.UseAetherEditorStatusBar();
        context.Services(services => services
            .Singleton<IFactory<TrueMoon.Aether.Editor.UI.IEditorViewportController,EditorViewportArguments>>(r => new Factory<TrueMoon.Aether.Editor.UI.IEditorViewportController,EditorViewportArguments>(args =>
                new EditorViewportController(args,r.Resolve<EditorViewportState>(),r.Resolve<EditorWorkspace>(),r.Resolve<EditorDocumentCommands>(),r.Resolve<EditorViewModel>(),
                    r.Resolve<EditorPreview>(),r.Resolve<AetherHost>().Preview)))
            .Singleton<EditorSession>(r => new(snapshot, r.Resolve<AetherHost>(), r.Resolve<EditorProjects>(),
                r.Resolve<EditorWorkspace>(), r.Resolve<EditorCommands>(), r.Resolve<EditorDocumentCommands>(), r.Resolve<EditorViewModel>(), r.Resolve<RuntimeViewportBinding>(), r.Resolve<EditorDiagnosticsViewModel>(),
                r.Resolve<IFactory<TrueMoon.Aether.Editor.UI.IEditorViewportController,EditorViewportArguments>>()))
            .Singleton<IStartable>(r => r.Resolve<EditorSession>()).Singleton<IStoppable>(r => r.Resolve<EditorSession>()));
        return context;
    }

    internal static BaseColorTextureDefinition DecodeProjectTexture(ReadOnlyMemory<byte> png)
    {
        SkiaNativeLibrary.Initialize();
        return BaseColorPng.Decode(png.Span);
    }
}
