namespace TrueMoon.Argentis;

/// <summary>A retained UI element with typed properties, layout, and deterministic attachment lifetime.</summary>
public abstract class Element : PropertiesBase, IElement, IDisposable
{
    private readonly Dictionary<object, object?> _values = [];
    private readonly List<IDisposable> _subscriptions = [];
    private readonly List<IDisposable> _attachmentSubscriptions = [];
    private readonly HashSet<object> _boundProperties = [];
    private Action? _verifyAccess;
    private Action<Action>? _post;
    private Action<Element>? _detaching;
    private int _attachmentVersion;
    private bool _disposing;
    internal bool ChangingTree { get; set; }
    private Style? _style;
    private Thickness _margin, _padding;
    private float _minWidth, _minHeight, _maxWidth = float.PositiveInfinity, _maxHeight = float.PositiveInfinity;
    private LayoutAlignment _horizontal = LayoutAlignment.Stretch, _vertical = LayoutAlignment.Stretch;
    private bool _disposed;
    private bool _hovered, _focused;
    /// <summary>Parent ownership is managed exclusively by containers.</summary>
    public Element? Parent { get; private set; }
    IElement? IElement.Parent => Parent;
    /// <summary>Child elements in paint order.</summary>
    public virtual IReadOnlyList<Element> Children => Array.Empty<Element>();
    /// <summary>The arranged rectangle in root coordinates.</summary>
    public Rect Bounds { get; private set; }
    /// <summary>The requested size, including margins.</summary>
    public Size DesiredSize { get; private set; }
    /// <summary>The current host theme.</summary>
    public Theme Theme { get; private set; } = Theme.Dark;
    /// <summary>Whether a UI session currently owns this element.</summary>
    public bool IsAttached => _verifyAccess != null;
    /// <summary>Whether resources and subscriptions have been released.</summary>
    public bool IsDisposed => _disposed;
    /// <summary>Whether this element accepts keyboard focus.</summary>
    public virtual bool Focusable => false;
    /// <summary>Whether the pointer is over this element.</summary>
    public bool IsHovered { get => _hovered; set { if (_hovered != value) { VerifyAccess(); _hovered = value; Invalidate(Invalidation.Render); } } }
    /// <summary>Whether this element has keyboard focus.</summary>
    public bool IsFocused { get => _focused; set { if (_focused != value) { VerifyAccess(); _focused = value; Invalidate(Invalidation.Render); } } }
    /// <summary>The effective enabled state, including ancestors.</summary>
    public bool IsEffectivelyEnabled => IsEnabled && (Parent?.IsEffectivelyEnabled ?? true);
    /// <summary>Explicit width; NaN selects automatic sizing.</summary>
    public float Width { get => Get(UiProperties.Width); set => Set(UiProperties.Width, value); }
    /// <summary>Explicit height; NaN selects automatic sizing.</summary>
    public float Height { get => Get(UiProperties.Height); set => Set(UiProperties.Height, value); }
    /// <summary>Minimum width.</summary>
    public float MinWidth { get => _minWidth; set { VerifySize(value); if (value > MaxWidth) throw new ArgumentOutOfRangeException(nameof(value)); _minWidth = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Minimum height.</summary>
    public float MinHeight { get => _minHeight; set { VerifySize(value); if (value > MaxHeight) throw new ArgumentOutOfRangeException(nameof(value)); _minHeight = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Maximum width.</summary>
    public float MaxWidth { get => _maxWidth; set { VerifySize(value, true); if (value < MinWidth) throw new ArgumentOutOfRangeException(nameof(value)); _maxWidth = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Maximum height.</summary>
    public float MaxHeight { get => _maxHeight; set { VerifySize(value, true); if (value < MinHeight) throw new ArgumentOutOfRangeException(nameof(value)); _maxHeight = value; Invalidate(Invalidation.Layout); } }
    /// <summary>External spacing.</summary>
    public Thickness Margin { get => _margin; set { VerifyThickness(value); _margin = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Internal spacing.</summary>
    public Thickness Padding { get => _padding; set { VerifyThickness(value); _padding = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Horizontal placement within the parent slot.</summary>
    public LayoutAlignment HorizontalAlignment { get => _horizontal; set { VerifyAccess(); _horizontal = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Vertical placement within the parent slot.</summary>
    public LayoutAlignment VerticalAlignment { get => _vertical; set { VerifyAccess(); _vertical = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Local styling overrides theme defaults.</summary>
    public Style? Style { get => _style; set { VerifyAccess(); _style = value; Invalidate(Invalidation.Layout | Invalidation.Tree); } }
    /// <summary>Whether the element participates in layout.</summary>
    public bool IsVisible { get => Get(UiProperties.Visible); set => Set(UiProperties.Visible, value); }
    /// <summary>Whether the element accepts input.</summary>
    public bool IsEnabled { get => Get(UiProperties.Enabled); set => Set(UiProperties.Enabled, value); }
    /// <summary>The text color.</summary>
    public Color Foreground { get => Get(UiProperties.Foreground); set => Set(UiProperties.Foreground, value); }
    /// <summary>The background fill.</summary>
    public Color Background { get => Get(UiProperties.Background); set => Set(UiProperties.Background, value); }
    /// <summary>Font size.</summary>
    public float FontSize { get => Get(UiProperties.FontSize); set => Set(UiProperties.FontSize, value); }
    /// <summary>Font family.</summary>
    public string FontFamily { get => Get(UiProperties.FontFamily); set => Set(UiProperties.FontFamily, value); }
    /// <summary>Raised for a local typed property change.</summary>
    public event Action<Element, object>? PropertyChanged;
    /// <summary>Raised when this element or a descendant needs work.</summary>
    public event Action<Invalidation>? Invalidated;
    /// <summary>Raised after the entire subtree attachment changes. Queue follow-up tree mutations through the host.</summary>
    public event Action? AttachmentChanged;

    /// <summary>Gets a local, styled, themed, or default value in that order.</summary>
    public T Get<T>(UiProperty<T> property)
    {
        if (_values.TryGetValue(property, out var value)) return (T)value!;
        if (_style != null && _style.TryGet(property, out T styled)) return styled;
        if (ReferenceEquals(property, UiProperties.Foreground)) return (T)(object)Theme.Foreground;
        if (ReferenceEquals(property, UiProperties.FontSize)) return (T)(object)Theme.FontSize;
        if (ReferenceEquals(property, UiProperties.FontFamily)) return (T)(object)Theme.FontFamily;
        return property.DefaultValue;
    }
    /// <summary>Sets a typed local value and invalidates its dependent work.</summary>
    public void Set<T>(UiProperty<T> property, T value)
    {
        VerifyAccess();
        property.Validate(value);
        var previous = Get(property);
        _values[property] = value;
        if (EqualityComparer<T>.Default.Equals(previous, value)) return;
        NotifyPropertyChanged(property, property.Affects);
    }
    /// <summary>Removes a local override so style and theme values become visible.</summary>
    public void Clear<T>(UiProperty<T> property)
    {
        VerifyAccess();
        if (!_values.Remove(property)) return;
        NotifyPropertyChanged(property, property.Affects);
    }
    /// <summary>Restores control invariants after a typed property commits and before observers run.</summary>
    /// <param name="property">The changed typed property identifier.</param>
    protected virtual void OnPropertyChanged(object property) { }

    private void NotifyPropertyChanged(object property, Invalidation reason)
    {
        List<Exception>? errors = null;
        try { OnPropertyChanged(property); }
        catch (Exception error) { (errors ??= []).Add(error); }
        try { Invalidate(reason); }
        catch (Exception error) { (errors ??= []).Add(error); }
        var handlers = PropertyChanged;
        if (handlers != null)
            foreach (Action<Element, object> handler in handlers.GetInvocationList())
            {
                try { handler(this, property); }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
        TreeChange.ThrowErrors(errors);
    }
    /// <summary>Requests rendering or layout at the owning session.</summary>
    public void Invalidate(Invalidation reason = Invalidation.Render)
    {
        List<Exception>? errors = null;
        var handlers = Invalidated;
        if (handlers != null)
            foreach (Action<Invalidation> handler in handlers.GetInvocationList())
            {
                try { handler(reason); }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
        try { Parent?.Invalidate(reason); }
        catch (Exception error) { (errors ??= []).Add(error); }
        TreeChange.ThrowErrors(errors);
    }
    /// <summary>Checks that mutations are performed on the session's owner thread.</summary>
    public void VerifyAccess() { ObjectDisposedException.ThrowIf(_disposed, this); _verifyAccess?.Invoke(); }
    /// <summary>Schedules a mutation on the host thread. Queued work is skipped after this attachment ends.</summary>
    /// <param name="action">The mutation; runs immediately when unattached and is ignored after disposal.</param>
    public void Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_disposed) return;
        var version = Volatile.Read(ref _attachmentVersion);
        var post = _post;
        if (post == null) action();
        else post(() => { if (!_disposed && IsAttached && version == Volatile.Read(ref _attachmentVersion)) action(); });
    }
    internal Action<Action> CaptureAttachmentDispatcher()
    {
        VerifyAccess();
        var post = _post ?? throw new InvalidOperationException("An attachment dispatcher requires an attached element.");
        var version = Volatile.Read(ref _attachmentVersion);
        return action => post(() => { if (!_disposed && IsAttached && version == Volatile.Read(ref _attachmentVersion)) action(); });
    }
    /// <summary>Tracks a resource until disposal. Removal and reparenting retain it.</summary>
    /// <param name="subscription">The resource whose ownership transfers to this element.</param>
    public void Own(IDisposable subscription)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(subscription);
        if (_disposing) throw new InvalidOperationException("Cannot own resources during disposal.");
        _subscriptions.Add(subscription);
    }
    /// <summary>Tracks a resource until the current attachment ends. Reattachment requires a new subscription.</summary>
    /// <param name="subscription">The resource whose ownership transfers only when this call succeeds.</param>
    public void OwnAttachment(IDisposable subscription)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(subscription);
        if (!IsAttached || _disposing) throw new InvalidOperationException("An attachment subscription requires an attached, live element.");
        _attachmentSubscriptions.Add(subscription);
    }

    internal void OwnBinding(object property, Func<IDisposable> create)
    {
        VerifyAccess();
        if (_disposing) throw new InvalidOperationException("Cannot create bindings during disposal.");
        if (!_boundProperties.Add(property)) throw new InvalidOperationException("The UI property already has a binding.");
        IDisposable? binding = null;
        try { binding = create(); Own(binding); }
        catch
        {
            _boundProperties.Remove(property);
            binding?.Dispose();
            throw;
        }
    }

    /// <summary>Checks owner-thread access and rejects structural changes during tree notifications or disposal.</summary>
    public void VerifyTreeAccess()
    {
        VerifyAccess();
        for (Element? node = this; node != null; node = node.Parent)
            if (node.ChangingTree || node._disposing) throw new InvalidOperationException("UI tree changes cannot be reentrant.");
    }

    internal void ValidateChild(Element child)
    {
        VerifyTreeAccess();
        ArgumentNullException.ThrowIfNull(child);
        ObjectDisposedException.ThrowIf(child.IsDisposed, child);
        if (child.Parent != null || child.IsAttached) throw new InvalidOperationException("An element can have only one owner.");
        for (Element? node = this; node != null; node = node.Parent)
            if (ReferenceEquals(node, child)) throw new InvalidOperationException("An element cannot contain itself or an ancestor.");
        child.ValidateUnattachedSubtree();
    }
    private void ValidateUnattachedSubtree()
    {
        VerifyTreeAccess();
        if (IsAttached) throw new InvalidOperationException("The subtree already belongs to a UI session.");
        foreach (var child in Children) child.ValidateUnattachedSubtree();
    }
    internal void Adopt(Element child, TreeChange change)
    {
        child.Parent = this;
        child.ApplyThemeCore(Theme, change);
        if (_verifyAccess != null) child.AttachCore(_verifyAccess, _post!, _detaching, change);
        change.Schedule(() => Invalidate(Invalidation.Tree | Invalidation.Layout));
    }
    internal void Orphan(Element child, TreeChange change)
    {
        child.Parent = null;
        child.DetachCore(change);
        change.Schedule(() => Invalidate(Invalidation.Tree | Invalidation.Layout));
    }
    /// <summary>Attaches this subtree to a host; normally called by UiSession.</summary>
    /// <param name="verifyAccess">Checks access to the host's owner thread.</param>
    /// <param name="post">Queues work on the host's owner thread.</param>
    public void Attach(Action verifyAccess, Action<Action> post) => Attach(verifyAccess, post, null);
    /// <summary>Attaches this subtree and notifies the host before detach notifications run.</summary>
    /// <param name="verifyAccess">Checks access to the host's owner thread.</param>
    /// <param name="post">Queues work on the host's owner thread.</param>
    /// <param name="detaching">Clears host references after attachment ends and before user detach notifications.</param>
    public void Attach(Action verifyAccess, Action<Action> post, Action<Element>? detaching)
    {
        ArgumentNullException.ThrowIfNull(verifyAccess);
        ArgumentNullException.ThrowIfNull(post);
        verifyAccess();
        ValidateUnattachedSubtree();
        if (Parent != null) throw new InvalidOperationException("Only an unowned root can be attached directly.");
        using var change = new TreeChange(this);
        AttachCore(verifyAccess, post, detaching, change);
        change.Complete();
    }
    private void AttachCore(Action verifyAccess, Action<Action> post, Action<Element>? detaching, TreeChange change)
    {
        var nodes = Subtree().ToArray();
        foreach (var node in nodes)
        {
            node._verifyAccess = verifyAccess;
            node._post = post;
            node._detaching = detaching;
            Interlocked.Increment(ref node._attachmentVersion);
        }
        foreach (var node in nodes.Reverse()) change.Notify(node.AttachmentChanged);
    }
    private IEnumerable<Element> Subtree()
    {
        yield return this;
        foreach (var child in Children) foreach (var node in child.Subtree()) yield return node;
    }
    /// <summary>Disconnects an unowned root without disposing it. Owned children must be removed by their container.</summary>
    public void Detach()
    {
        VerifyTreeAccess();
        if (Parent != null) throw new InvalidOperationException("Remove an owned child before detaching it.");
        if (!IsAttached) return;
        using var change = new TreeChange(this);
        DetachCore(change);
        change.Complete();
    }
    private void DetachCore(TreeChange change)
    {
        var nodes = Subtree().Where(node => node.IsAttached).ToArray();
        foreach (var node in nodes)
        {
            var detaching = node._detaching;
            change.Schedule(() => detaching?.Invoke(node));
            node._verifyAccess = null;
            node._post = null;
            node._detaching = null;
            Interlocked.Increment(ref node._attachmentVersion);
            node._hovered = node._focused = false;
        }
        foreach (var node in nodes.Reverse())
        {
            change.Schedule(node.OnInputCancelled);
            var subscriptions = node._attachmentSubscriptions.ToArray();
            node._attachmentSubscriptions.Clear();
            foreach (var subscription in subscriptions) change.Schedule(subscription.Dispose);
            change.Notify(node.AttachmentChanged);
        }
    }
    /// <summary>Applies the session's theme to this subtree.</summary>
    public void ApplyTheme(Theme theme)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(theme);
        using var change = new TreeChange(this);
        ApplyThemeCore(theme, change);
        change.Complete();
    }
    private void ApplyThemeCore(Theme theme, TreeChange change)
    {
        var nodes = Subtree().ToArray();
        foreach (var node in nodes) node.Theme = theme;
        foreach (var node in nodes.Reverse()) change.Schedule(() => node.Invalidate(Invalidation.Layout));
    }
    /// <summary>Measures the subtree within a logical size constraint.</summary>
    public void Measure(Size available, ITextLayoutService text)
    {
        if (!IsVisible) { DesiredSize = default; return; }
        var inner = new Size(Math.Max(0, Math.Min(available.Width - Margin.Horizontal, float.IsNaN(Width) ? MaxWidth : Width) - Padding.Horizontal),
            Math.Max(0, Math.Min(available.Height - Margin.Vertical, float.IsNaN(Height) ? MaxHeight : Height) - Padding.Vertical));
        var desired = MeasureCore(inner, text);
        DesiredSize = new Size(Math.Clamp(float.IsNaN(Width) ? desired.Width + Padding.Horizontal : Width, MinWidth, MaxWidth) + Margin.Horizontal,
            Math.Clamp(float.IsNaN(Height) ? desired.Height + Padding.Vertical : Height, MinHeight, MaxHeight) + Margin.Vertical);
    }
    /// <summary>Arranges this subtree in a logical parent slot.</summary>
    public void Arrange(Rect slot)
    {
        if (!IsVisible) { Bounds = default; return; }
        var area = slot.Deflate(Margin);
        var width = Math.Clamp(float.IsNaN(Width) ? (HorizontalAlignment == LayoutAlignment.Stretch ? area.Width : DesiredSize.Width - Margin.Horizontal) : Width, MinWidth, MaxWidth);
        var height = Math.Clamp(float.IsNaN(Height) ? (VerticalAlignment == LayoutAlignment.Stretch ? area.Height : DesiredSize.Height - Margin.Vertical) : Height, MinHeight, MaxHeight);
        width = Math.Min(width, area.Width); height = Math.Min(height, area.Height);
        Bounds = new Rect(area.X + Align(area.Width - width, HorizontalAlignment), area.Y + Align(area.Height - height, VerticalAlignment), width, height);
        ArrangeCore(Bounds.Deflate(Padding));
    }
    private static float Align(float extra, LayoutAlignment alignment) => alignment switch { LayoutAlignment.Center => extra / 2, LayoutAlignment.End => extra, _ => 0 };
    /// <summary>Measures custom content excluding margins and padding.</summary>
    protected virtual Size MeasureCore(Size available, ITextLayoutService text) => default;
    /// <summary>Arranges custom content inside the padded bounds.</summary>
    protected virtual void ArrangeCore(Rect contentBounds) { }
    /// <summary>Draws this subtree with balanced clipping state.</summary>
    public void Draw(IDrawingContext context)
    {
        if (!IsVisible || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        context.Save();
        try
        {
            context.Clip(Bounds);
            if (Background.A != 0) context.Fill(Bounds, Background);
            DrawCore(context);
            foreach (var child in Children) child.Draw(context);
            if (IsFocused) context.Stroke(Bounds.Deflate(new Thickness(1)), Theme.Accent, 2, 3);
        }
        finally { context.Restore(); }
    }
    /// <summary>Draws custom content before children.</summary>
    protected virtual void DrawCore(IDrawingContext context) { }
    /// <summary>Handles a routed input event, returning true when consumed.</summary>
    public virtual bool HandleInput(UiInput input, IInputContext context) => false;
    /// <summary>Resets transient interaction state after focus or capture is lost.</summary>
    public virtual void OnInputCancelled() { }
    private void VerifySize(float value, bool infinite = false)
    {
        VerifyAccess();
        if (value < 0 || float.IsNaN(value) || (!infinite && !float.IsFinite(value))) throw new ArgumentOutOfRangeException(nameof(value));
    }
    private void VerifyThickness(Thickness t) { VerifySize(t.Left); VerifySize(t.Top); VerifySize(t.Right); VerifySize(t.Bottom); }
    /// <summary>Disposes an unowned subtree and its resources exactly once, continuing cleanup after callback errors.</summary>
    /// <remarks>Remove an owned child before disposing it. Containers dispose their remaining children automatically.</remarks>
    public virtual void Dispose()
    {
        if (_disposed || _disposing) return;
        VerifyAccess();
        if (Parent != null && !Parent._disposing) throw new InvalidOperationException("Remove an owned child before disposing it.");
        // Parent disposal already holds the tree lock, while retaining virtual child disposal.
        using var change = Parent?._disposing == true ? new TreeChange() : new TreeChange(this);
        _disposing = true;
        try
        {
            DetachCore(change);
            change.Flush();
            var subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
            foreach (var subscription in subscriptions) change.Run(subscription.Dispose);
            foreach (var child in Children) change.Run(child.Dispose);
        }
        finally
        {
            Invalidated = null; PropertyChanged = null; AttachmentChanged = null;
            _disposed = true;
            _disposing = false;
        }
        change.Complete();
    }
}
