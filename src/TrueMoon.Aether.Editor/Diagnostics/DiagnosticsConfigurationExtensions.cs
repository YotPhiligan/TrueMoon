using TrueMoon.Argentis;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Registers the App-owned presentation and parent-owned console factory without creating a collector.</summary>
public static class DiagnosticsConfigurationExtensions
{
    public static IAppConfigurationContext UseAetherEditorDiagnostics(this IAppConfigurationContext context, EditorDiagnosticsOptions? options = null)
        => Register(context, options, false);

    internal static void EnsureAetherEditorDiagnostics(this IAppConfigurationContext context) => Register(context, null, true);

    private static IAppConfigurationContext Register(IAppConfigurationContext context, EditorDiagnosticsOptions? options, bool useExisting)
    {
        ArgumentNullException.ThrowIfNull(context); var snapshot = (options ?? new()).Snapshot();
        context.Services(services =>
        {
            if (services.Exist<EditorDiagnosticsViewModel>())
            {
                if (useExisting) return;
                throw new InvalidOperationException("Editor diagnostics are already registered.");
            }
            if (!services.Exist<EditorDiagnosticReportWriter>()) services.Singleton<EditorDiagnosticReportWriter>(_ => new());
            if (!services.Exist<IViewStyle<EditorDiagnosticsView>>()) services.Singleton<IViewStyle<EditorDiagnosticsView>>(_ => new EditorDiagnosticsViewStyle());
            services.Singleton<EditorDiagnosticsViewModel>(r => new(r.TryResolve<IDiagnosticRecordReader>(), snapshot, r.Resolve<EditorDiagnosticReportWriter>(),
                r.TryResolve<IDiagnosticHealthReader>(), r.TryResolve<TrueMoon.Aether.Diagnostics.IProcessDiagnosticsSource>()));
            if (!services.Exist<IFactory<EditorDiagnosticsView>>()) services.Singleton<IFactory<EditorDiagnosticsView>>(r =>
                new Factory<EditorDiagnosticsView>(() => new(r.Resolve<EditorDiagnosticsViewModel>(), r.Resolve<IViewStyle<EditorDiagnosticsView>>())));
            services.Singleton<IStoppable>(r => new DiagnosticsLifetime(r.Resolve<EditorDiagnosticsViewModel>()));
        });
        return context;
    }
    // This alias is not disposable: DI owns the model exactly once; each UI parent owns its factory product.
    private sealed class DiagnosticsLifetime(EditorDiagnosticsViewModel model) : IStoppable
    { public Task StopAsync(CancellationToken cancellationToken = default) => model.StopAsync(); }
}
