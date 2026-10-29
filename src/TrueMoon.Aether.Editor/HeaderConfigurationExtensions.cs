using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace TrueMoon.Aether.Editor;

/// <summary>App composition for compact window-owned editor headers.</summary>
public static class HeaderConfigurationExtensions
{
    /// <summary>Registers header style and a typed factory. Each view product belongs to its window, not the container.</summary>
    /// <param name="context">App composition after editor presentation and editing registration.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorHeader(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<IFactory<EditorHeaderView, EditorHeaderArguments>>())
                throw new InvalidOperationException("Editor header is already registered.");
            if (!services.Exist<EditorViewModel>() || !services.Exist<EditorDocumentViewModel>() || !services.Exist<EditorInspectorViewModel>())
                throw new InvalidOperationException("Configure editor presentation and editing before the header.");
            if (!services.Exist<IViewStyle<EditorHeaderView>>()) services.Singleton<IViewStyle<EditorHeaderView>>(_ => new EditorHeaderViewStyle());
            services.Singleton<IFactory<EditorHeaderView, EditorHeaderArguments>>(r => new Factory<EditorHeaderView, EditorHeaderArguments>(args =>
                new(r.Resolve<EditorViewModel>()!, r.Resolve<EditorDocumentViewModel>()!, r.Resolve<EditorInspectorViewModel>()!,
                    r.Resolve<IViewStyle<EditorHeaderView>>()!, args, r.TryResolve<EditorDiagnosticsViewModel>())));
        });
        return context;
    }
}
