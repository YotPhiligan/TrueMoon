using System.ComponentModel;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Standalone Window root owning one reusable EditorView; models, style and factories are borrowed.</summary>
/// <remarks>Construct on the UI owner through the registered factory. This constructor performs no native/GPU work.
/// Document exit policy remains with EditorWindow/EditorSession, rather than a second Closing handler here.</remarks>
public sealed class EditorMainWindow : Window<EditorViewModel>
{
    private readonly EditorDocumentViewModel _documents;
    private readonly string _baseTitle;
    private bool _captionPending;
    /// <summary>The single owned editor content instance, also passed to the standard header and presentation adapter.</summary>
    public EditorView EditorView { get; }
    /// <summary>Creates the caller-owned standalone root from borrowed App services and copied initial settings.</summary>
    /// <param name="model">App-owned main presentation model.</param><param name="documents">App-owned caption/document presentation.</param>
    /// <param name="views">Creates a fresh EditorView belonging to the same model.</param>
    /// <param name="style">Borrowed scoped wrapper appearance; nested EditorView retains its own style.</param>
    /// <param name="arguments">Initial caption/client area copied after host overrides.</param>
    public EditorMainWindow(EditorViewModel model, EditorDocumentViewModel documents, IFactory<EditorView> views,
        IViewStyle<EditorMainWindow> style, EditorMainWindowArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(views); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(arguments);
        _documents = documents; _baseTitle = arguments.Title;
        EditorView? accepted = null;
        try
        {
            Title = arguments.Title; ClientSize = arguments.ClientSize;
            var view = views.Create() ?? throw new InvalidOperationException("Editor view factory returned null.");
            if (view.IsDisposed || view.IsAttached || view.Parent != null)
                throw new InvalidOperationException("Editor view factory must return a fresh, detached view.");
            accepted = view;
            if (!ReferenceEquals(view.DataContext, model))
                throw new ArgumentException("Editor view belongs to another presentation model.", nameof(views));
            EditorView = view;
            Content(model, _ => view);
            this.WithViewStyle(style);
            _documents.PropertyChanged += DocumentChanged;
            Own(new Subscription(() => _documents.PropertyChanged -= DocumentChanged));
            AttachmentChanged += Attachment;
            Own(new Subscription(() => AttachmentChanged -= Attachment));
            UpdateCaption();
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            try { Dispose(); } catch (Exception cleanup) { errors.Add(cleanup); }
            if (accepted is { Parent: null, IsAttached: false, IsDisposed: false })
                try { accepted.Dispose(); } catch (Exception cleanup) { errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException(errors);
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
    private void DocumentChanged(object? sender, PropertyChangedEventArgs args) => QueueCaption();
    private void QueueCaption()
    {
        if (_captionPending) return;
        _captionPending = true;
        try { Dispatch(() => { _captionPending = false; UpdateCaption(); }); }
        catch { _captionPending = false; throw; }
    }
    private void Attachment()
    {
        _captionPending = false;
        if (IsAttached) QueueCaption();
    }
    private void UpdateCaption()
    {
        Title = _documents.GetWindowCaption(_baseTitle);
        AccessibleName = _documents.GetWindowCaptionDescription(_baseTitle);
    }
    private sealed class Subscription(Action release) : IDisposable { public void Dispose() => release(); }
}
