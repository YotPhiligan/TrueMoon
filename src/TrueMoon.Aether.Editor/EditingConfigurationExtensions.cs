using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace TrueMoon.Aether.Editor;

/// <summary>App composition for schema-driven drafts and parent-owned inspector/form products.</summary>
public static class EditingConfigurationExtensions
{
    /// <summary>Registers shared presentation/style and a factory whose view products belong to their UI parent.</summary>
    /// <param name="context">App composition; EditorDocumentCommands and EditorPreview must already be configured.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorEditing(this IAppConfigurationContext context)
        => UseAetherEditorEditing(context, false);

    /// <summary>Registers the form with preview controls hosted separately in the status bar when requested.</summary>
    public static IAppConfigurationContext UseAetherEditorEditing(this IAppConfigurationContext context, bool previewInStatusBar)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<EditorInspectorViewModel>() || services.Exist<IFactory<EditorInspectorView>>() || services.Exist<IFactory<EditorTransformView>>())
                throw new InvalidOperationException("Editor editing is already registered.");
            if (!services.Exist<EditorDocumentCommands>() || !services.Exist<EditorPreview>())
                throw new InvalidOperationException("Configure document commands and preview before editor editing.");
            services.Singleton<EditorInspectorViewModel>(r => new(r.Resolve<EditorDocumentCommands>()!, r.Resolve<EditorPreview>()!));
            if (!services.Exist<IViewStyle<EditorInspectorView>>()) services.Singleton<IViewStyle<EditorInspectorView>>(_ => new EditorInspectorViewStyle());
            services.Singleton<IFactory<EditorInspectorView>>(r => new Factory<EditorInspectorView>(() =>
                new(r.Resolve<EditorInspectorViewModel>()!, r.Resolve<IViewStyle<EditorInspectorView>>()!)));
            if (!services.Exist<IViewStyle<EditorTransformView>>()) services.Singleton<IViewStyle<EditorTransformView>>(_ => new EditorTransformViewStyle());
            services.Singleton<IFactory<EditorTransformView>>(r => new Factory<EditorTransformView>(() =>
                new(r.Resolve<EditorInspectorViewModel>()!, r.Resolve<IViewStyle<EditorTransformView>>()!, !previewInStatusBar)));
        });
        return context;
    }
}
