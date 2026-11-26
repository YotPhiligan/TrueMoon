using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Windows;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Standalone Window root owning one reusable EditorView; models, style and factories are borrowed.</summary>
/// <remarks>Construct on the UI owner through the registered factory. This constructor performs no native/GPU work.
/// Document exit policy remains with EditorWindow/EditorSession, rather than a second Closing handler here.</remarks>
public sealed class EditorMainWindow : Window<IEditorViewModel>
{
    private readonly IEditorDocumentViewModel _documents;
    private readonly string _baseTitle;
    /// <summary>The window-owned editor. The title-bar callback can borrow it before the window attaches.</summary>
    public EditorView EditorView { get; }
    /// <summary>Creates the caller-owned standalone root from borrowed App services and copied initial settings.</summary>
    /// <param name="model">App-owned main presentation model.</param><param name="documents">App-owned caption/document presentation.</param>
    /// <param name="views">Creates the early editor after the window settings are validated.</param>
    /// <param name="style">Borrowed scoped wrapper appearance; nested EditorView retains its own style.</param>
    /// <param name="arguments">Initial caption/client area copied after host overrides.</param>
    public EditorMainWindow(IEditorViewModel model, IEditorDocumentViewModel documents, IFactory<EditorView> views,
        IViewStyle<EditorMainWindow> style, EditorMainWindowArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(views); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(arguments);
        _documents = documents; _baseTitle = arguments.Title;
        try
        {
            Title = arguments.Title; ClientSize = arguments.ClientSize;
            var view = views.Create() ?? throw new InvalidOperationException("Editor view factory returned null.");
            if (view.IsDisposed || view.IsAttached || view.Parent != null)
                throw new InvalidOperationException("Editor view factory must return a fresh, detached view.");
            EditorView = OwnDraft(view);
            if (!ReferenceEquals(view.DataContext, model))
                throw new ArgumentException("Editor view belongs to another presentation model.", nameof(views));
            Content(model, ui => ui.Adopt(EditorView));
            this.WithViewStyle(style);
            UpdateCaption();
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    /// <summary>Reads the explicit standalone root with or without its one host-owned WindowFrame.</summary>
    /// <param name="root">The actual UI session root on its owner thread.</param>
    /// <returns>The existing Window component. No recursive view search or ownership transfer is performed.</returns>
    public static EditorMainWindow FromRoot(Element root)
    {
        ArgumentNullException.ThrowIfNull(root); root.VerifyAccess();
        return root switch
        {
            EditorMainWindow window => window,
            WindowFrame { Content: EditorMainWindow window } => window,
            _ => throw new ArgumentException("Expected an EditorMainWindow presentation root.", nameof(root))
        };
    }
    /// <inheritdoc />
    protected override void OnContentPrepared() =>
        this.ObserveState(UpdateCaption, UiSynchronization.AfterTreeChange, _documents);
    private void UpdateCaption()
    {
        Title = _documents.GetWindowCaption(_baseTitle);
        AccessibleName = _documents.GetWindowCaptionDescription(_baseTitle);
    }
}
