using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.Diagnostics.CodeAnalysis;

namespace TrueMoon.Argentis.Views;

/// <summary>A reusable view authored in a C# class.</summary>
/// <remarks>The view is its own name and style scope: it indexes its root and descendants up to the next view
/// boundary, where a child view root is indexed by its own name while its descendants stay private to that view.
/// Content is declared once — through <see cref="Content"/>, <see cref="DeclareContent{TData}"/> or a direct
/// <see cref="SetContent"/> inside <see cref="OnContentPrepared"/> — so a view is not a content host whose
/// content is meant to be replaced from outside.</remarks>
public abstract class View : Element, IView
{
    private Element[] _children = [];
    private Dictionary<string, Element>? _parts;
    private ViewStyleScope? _viewStyleScope;
    private ViewDependencyOwnership? _ownedDrafts;
    private Func<IUiControlFactories, Element>? _deferredContent;
    private Action? _deferredStyle;
    private bool _contentPreparationStarted;

    // The view root is the scope that indexes its parts and resolves its styles; there is no separate scope object.
    internal sealed override View OwnedScope => this;

    /// <summary>The current child.</summary>
    public Element? Child => _children.Length == 0 ? null : _children[0];
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => _children;
    /// <summary>Replaces content. The caller owns the detached old subtree and must reuse or dispose it.</summary>
    /// <param name="content">An unowned live subtree, or null to clear content.</param>
    /// <remarks>Validation failures preserve the tree. Notification failures are reported after the replacement commits.</remarks>
    public void SetContent(Element? content)
    {
        VerifyTreeAccess();
        var previous = Child;
        if (ReferenceEquals(content, previous)) return;
        if (content != null) ValidateChildChange(content, previous);
        using var change = TreeChange.Acquire(this, previous, content);
        _children = content == null ? [] : [content];
        if (previous != null) Orphan(previous, change);
        if (content != null) Adopt(content, change);
        change.ScheduleInvalidation(this, Invalidation.Tree | Invalidation.Layout);
        change.Complete();
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) { Child?.Measure(available, text); return Child?.DesiredSize ?? default; }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area) => Child?.Arrange(area);

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
        using var guard = attachedParent == null ? null : TreeChange.Acquire(attachedParent);
        VerifyAccess();
        if (_contentPreparationStarted) throw new InvalidOperationException("View content preparation has already started.");

        _contentPreparationStarted = true;
        Element? draft = null;
        try
        {
            if (Child == null)
            {
                using (TreeChange.Acquire(this)) draft = factory(factories);
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
        var part = FindPart(name) ?? throw new KeyNotFoundException($"View '{GetType().Name}' has no part named '{name}'.");
        return part as T ?? throw new InvalidOperationException(
            $"Part '{name}' in view '{GetType().Name}' is '{part.GetType().Name}', expected '{typeof(T).Name}'.");
    }

    /// <summary>Gets an optional named part. Returns false for a missing part or a type mismatch.</summary>
    public bool TryGetPart<T>(string name, [NotNullWhen(true)] out T? part) where T : Element
    {
        part = FindPart(name) as T;
        return part != null;
    }

    internal Element? FindPart(string name)
    {
        VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return string.Equals(PartName, name, StringComparison.Ordinal) ? this : _parts?.GetValueOrDefault(name);
    }

    internal void ValidateRootName(string name)
    {
        if (_parts?.ContainsKey(name) == true) throw DuplicatePart(name);
    }

    internal void ValidatePartChanges(IReadOnlyList<Element> added, IReadOnlyList<Element> removed)
    {
        HashSet<string>? names = null;
        for (var i = 0; i < added.Count; i++) Validate(added[i]);

        void Validate(Element node)
        {
            if (node.PartName is { } name)
            {
                // A child view root may share this view's root name; the view root always wins lookup,
                // so the shadowed entry simply stays out of the index.
                if (node.OwnedScope != null && string.Equals(PartName, name, StringComparison.Ordinal)) return;
                if (!(names ??= new(StringComparer.Ordinal)).Add(name) || string.Equals(PartName, name, StringComparison.Ordinal))
                    throw DuplicatePart(name);
                if (_parts?.GetValueOrDefault(name) is { } existing && !WillBeRemoved(existing, removed))
                    throw DuplicatePart(name);
            }
            if (node.OwnedScope != null) return;
            var children = node.Children;
            for (var i = 0; i < children.Count; i++) Validate(children[i]);
        }
    }

    private InvalidOperationException DuplicatePart(string name) =>
        new($"View '{GetType().Name}' already contains a part named '{name}'.");

    private static bool WillBeRemoved(Element part, IReadOnlyList<Element> removed)
    {
        if (removed.Count == 0) return false;
        for (Element? node = part; node != null; node = node.Parent)
            for (var i = 0; i < removed.Count; i++)
                if (ReferenceEquals(node, removed[i])) return true;
        return false;
    }

    internal void CommitPartChange(Element node, bool added)
    {
        if (!added && _parts == null) return;
        if (node.PartName is { } name && (node.OwnedScope == null || !string.Equals(PartName, name, StringComparison.Ordinal)))
        {
            if (added) (_parts ??= new(StringComparer.Ordinal)).Add(name, node);
            else _parts!.Remove(name);
        }
        if (node.OwnedScope != null) return;
        var children = node.Children;
        for (var i = 0; i < children.Count; i++) CommitPartChange(children[i], added);
    }

    internal void ValidateTheme(Theme theme) => _viewStyleScope?.PrepareTheme(theme);
    internal Style? ResolveStyle(Element element, Theme theme) => _viewStyleScope?.Resolve(element, theme);

    internal void ReplaceStyle(ViewStyleScope? style)
    {
        VerifyAccess();
        var pending = new List<(Element Node, Style? Value)>();
        Collect(this);
        _viewStyleScope = style;
        foreach (var (node, value) in pending) node.SetViewStyleValue(value);
        Invalidate(Invalidation.Layout | Invalidation.Render);

        void Collect(Element node)
        {
            pending.Add((node, style?.Resolve(node, node.Theme)));
            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
                if (children[i].OwnedScope == null) Collect(children[i]);
        }
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
