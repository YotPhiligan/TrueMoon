using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace TrueMoon.Aether.Editor;

/// <summary>App composition for status presentation and parent/overlay-owned view products.</summary>
public static class StatusBarConfigurationExtensions
{
    /// <summary>Registers borrowed models/styles and UI-owner factories after editor presentation/editing.</summary>
    /// <param name="context">App composition root.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorStatusBar(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<EditorStatusBarViewModel>() || services.Exist<IFactory<EditorStatusBarView, EditorStatusBarArguments>>() ||
                services.Exist<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>())
                throw new InvalidOperationException("Editor status bar is already registered.");
            if (!services.Exist<EditorViewModel>() || !services.Exist<EditorDocumentViewModel>() || !services.Exist<EditorInspectorViewModel>())
                throw new InvalidOperationException("Configure editor presentation and editing before the status bar.");
            services.Singleton<EditorStatusBarViewModel>(r => new(r.Resolve<EditorViewModel>()!, r.Resolve<EditorDocumentViewModel>()!, r.Resolve<EditorInspectorViewModel>()!));
            if (!services.Exist<IViewStyle<EditorStatusBarView>>()) services.Singleton<IViewStyle<EditorStatusBarView>>(_ => new EditorStatusBarViewStyle());
            if (!services.Exist<IViewStyle<EditorStatusPopupView>>()) services.Singleton<IViewStyle<EditorStatusPopupView>>(_ => new EditorStatusPopupViewStyle());
            services.Singleton<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>(r => new Factory<EditorStatusPopupView, EditorStatusPopupArguments>(args =>
                new(r.Resolve<EditorStatusBarViewModel>()!, r.Resolve<IViewStyle<EditorStatusPopupView>>()!, args)));
            services.Singleton<IFactory<EditorStatusBarView, EditorStatusBarArguments>>(r => new Factory<EditorStatusBarView, EditorStatusBarArguments>(args =>
                new(r.Resolve<EditorStatusBarViewModel>()!, r.Resolve<IViewStyle<EditorStatusBarView>>()!,
                    r.Resolve<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>()!, args)));
        });
        return context;
    }
}
