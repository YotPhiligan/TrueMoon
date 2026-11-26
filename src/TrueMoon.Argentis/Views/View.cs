using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Layout;
using System.Diagnostics.CodeAnalysis;

namespace TrueMoon.Argentis.Views;

/// <summary>A reusable view authored in a C# class.</summary>
public abstract class View : ContentControl, IView
{
    internal sealed override ElementScope OwnedScope { get; }
    private ViewDependencyOwnership? _ownedDrafts;
    private Func<IUiControlFactories, Element>? _deferredContent;
    private Action? _deferredStyle;
    private bool _contentPreparationStarted;

    /// <summary>Starts a name and style scope independent of any enclosing view.</summary>
    protected View() => OwnedScope = new(this);

    /// <summary>Initializes behavior after factory-built content and its named parts are installed.</summary>
    protected virtual void OnContentPrepared() { }

    /// <summary>Owns a precreated control that a host needs before this view's content is prepared.</summary>
    /// <param name="control">A fresh, unattached control with no other owner.</param>
    /// <returns>The same control, now owned by this view.</returns>
    /// <remarks>The view disposes untransferred drafts. Transfer a draft with its own <c>ui.Adopt(control)</c>,
    /// or by installing it directly as this view's content.</remarks>
    protected TControl OwnDraft<TControl>(TControl control) where TControl : Element
    {
        VerifyTreeAccess();
        ArgumentNullException.ThrowIfNull(control);
        control.VerifyAccess();
        if (_contentPreparationStarted || IsAttached || ReferenceEquals(this, control) ||
            control is not { Parent: null, IsAttached: false, DependencyOwnership: null })
            throw new InvalidOperationException("OwnDraft requires an unprepared view and an unowned control.");
        for (var owner = DependencyOwnership?.DraftOwner; owner != null; owner = owner.DependencyOwnership?.DraftOwner)
            if (ReferenceEquals(owner, control)) throw new InvalidOperationException("View drafts cannot form an ownership cycle.");

        if (_ownedDrafts == null)
        {
            _ownedDrafts = new ViewDependencyOwnership(2, this);
            Own(_ownedDrafts);
        }
        _ownedDrafts.Add(control);
        return control;
    }

    internal bool HasDeferredContent => _deferredContent != null;

    /// <inheritdoc />
    public void Content(Func<IUiBuildContext<object>, Element> factory) => DeclareContent<object>(null, factory);

    /// <summary>Declares a layout with a borrowed, replaceable context.</summary>
    protected void DeclareContent<TData>(IDataContext<TData>? context, Func<IUiBuildContext<TData>, Element> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        VerifyTreeAccess();
        if (IsAttached || Child != null || _contentPreparationStarted || HasDeferredContent)
            throw new InvalidOperationException("Deferred content must be declared once on an unprepared view.");
        _deferredContent = factories => new UiBuildContext<TData>(this, factories, context).Build(factory);
    }

    internal void DeferStyle(Action applyStyle) => _deferredStyle = applyStyle;

    private protected sealed override void PrepareForAttach(IUiControlFactories? factories, Element? attachedParent)
    {
        if (_deferredContent is not { } factory) return;
        if (factories == null) throw new InvalidOperationException("Attaching a declared view requires a UI control factory registry.");
        // Preparing a detached candidate must not allow callbacks to modify the tree receiving it.
        using var guard = attachedParent == null ? null : new TreeChange(attachedParent);
        VerifyAccess();
        if (_contentPreparationStarted) throw new InvalidOperationException("View content preparation has already started.");

        _contentPreparationStarted = true;
        Element? draft = null;
        try
        {
            if (Child == null)
            {
                using (new TreeChange(this)) draft = factory(factories);
                SetContent(draft);
                OnContentPrepared();
            }
            _deferredStyle?.Invoke();
            _deferredContent = null;
            _deferredStyle = null;
        }
        catch (Exception original)
        {
            if (draft != null) CleanFailedContent(draft, original);
            throw;
        }
    }

    private void CleanFailedContent(Element draft, Exception original)
    {
        List<Exception>? errors = null;
        if (ReferenceEquals(Child, draft))
        {
            try { SetContent(null); }
            catch (Exception cleanup) { (errors ??= [original]).Add(cleanup); }
        }
        if (draft.Parent == null && !draft.IsAttached && !draft.IsDisposed)
        {
            try { draft.Dispose(); }
            catch (Exception cleanup) { (errors ??= [original]).Add(cleanup); }
        }
        TreeChange.ThrowErrors(errors);
    }

    /// <summary>Gets the live named part of the expected type in this view's scope.</summary>
    /// <exception cref="KeyNotFoundException">The part is missing.</exception>
    /// <exception cref="InvalidOperationException">The part has an incompatible type.</exception>
    public T GetRequiredPart<T>(string name) where T : Element
    {
        var part = OwnedScope.FindPart(name) ?? throw new KeyNotFoundException($"View '{GetType().Name}' has no part named '{name}'.");
        return part as T ?? throw new InvalidOperationException(
            $"Part '{name}' in view '{GetType().Name}' is '{part.GetType().Name}', expected '{typeof(T).Name}'.");
    }

    /// <summary>Gets an optional named part. Returns false for a missing part or a type mismatch.</summary>
    public bool TryGetPart<T>(string name, [NotNullWhen(true)] out T? part) where T : Element
    {
        part = OwnedScope.FindPart(name) as T;
        return part != null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally
        {
            if (IsDisposed)
            {
                _ownedDrafts = null;
                _deferredContent = null;
                _deferredStyle = null;
            }
        }
    }
}
/// <summary>A view with explicitly bound typed data. Data may be a plain model or an observable view model.</summary>
public abstract class View<TData> : View, IView<TData>
{
    private readonly DataContextState<TData> _data = new();

    /// <inheritdoc />
    public TData? DataContext { get => _data.Value; set => _data.Set(this, value); }
    /// <inheritdoc />
    public event Action? DataContextChanged { add => _data.Changed += value; remove => _data.Changed -= value; }

    /// <inheritdoc />
    public void Content(TData? data, Func<IUiBuildContext<TData>, Element> factory)
    {
        DeclareContent(this, factory);
        DataContext = data;
    }
}
