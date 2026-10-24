using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace TrueMoon.Aether.Editor;

/// <summary>App composition for the scene hierarchy over shared document and inspector owners.</summary>
public static class HierarchyConfigurationExtensions
{
    /// <summary>Registers App-owned presentation/style and a UI-owner factory with parent-owned products.</summary>
    /// <param name="context">Composition after workspace, document commands and Transform presentation.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorHierarchy(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<EditorHierarchyViewModel>() || services.Exist<IFactory<EditorHierarchyView>>())
                throw new InvalidOperationException("Editor hierarchy is already registered.");
            if (!services.Exist<EditorWorkspace>() || !services.Exist<EditorDocumentCommands>() || !services.Exist<EditorInspectorViewModel>())
                throw new InvalidOperationException("Configure workspace, document commands and editor editing before hierarchy.");
            services.Singleton<EditorHierarchyViewModel>(resolver => new(resolver.Resolve<EditorWorkspace>()!,
                resolver.Resolve<EditorDocumentCommands>()!, resolver.Resolve<EditorInspectorViewModel>()!));
            if (!services.Exist<IViewStyle<EditorHierarchyView>>()) services.Singleton<IViewStyle<EditorHierarchyView>>(_ => new EditorHierarchyViewStyle());
            services.Singleton<IFactory<EditorHierarchyView>>(resolver => new Factory<EditorHierarchyView>(() =>
                new(resolver.Resolve<EditorHierarchyViewModel>()!, resolver.Resolve<IViewStyle<EditorHierarchyView>>()!)));
        });
        return context;
    }
}
