using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis.Windows;

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
    private IUiControlFactories? _controlFactories;
    private int _attachmentVersion;
    private bool _disposing;
    internal bool ChangingTree { get; set; }
    internal Element? PresentationRootForAttach { private get; set; }
    private Style? _style;
    private Style? _viewStyle;
    private HashSet<object>? _viewStyleRoles;
    private Thickness _margin;
    private float _minWidth, _minHeight, _maxWidth = float.PositiveInfinity, _maxHeight = float.PositiveInfinity;
    private LayoutAlignment _horizontal = LayoutAlignment.Stretch, _vertical = LayoutAlignment.Stretch;
    private bool _disposed;
    private bool _hovered, _focused, _inputBlocked;
    /// <summary>Parent ownership is managed exclusively by containers.</summary>
    public Element? Parent { get; private set; }
    IElement? IElement.Parent => Parent;
    /// <summary>Child elements in paint order.</summary>
    public virtual IReadOnlyList<Element> Children => Array.Empty<Element>();
    /// <summary>The root-coordinate viewport clipping this element's children for drawing and hit testing.</summary>
    public virtual Rect ChildClipBounds => Bounds;
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
    /// <summary>Custom title-bar role. Focusable controls stay client input unless explicitly Maximize; Client subtrees always exclude native roles.</summary>
    public WindowRegionRole WindowRegion { get => Get(UiProperties.WindowRegion); set => Set(UiProperties.WindowRegion, value); }
    /// <summary>Whether the pointer is over this element.</summary>
    public bool IsHovered { get => _hovered; set { if (_hovered != value) { VerifyAccess(); _hovered = value; Invalidate(Invalidation.Render); } } }
    /// <summary>Whether this element has keyboard focus.</summary>
    public bool IsFocused { get => _focused; set { if (_focused != value) { VerifyAccess(); _focused = value; Invalidate(Invalidation.Render); FocusChanged?.Invoke(this); } } }
    /// <summary>Raised on the UI owner after keyboard focus changes. Detaching a focused element also raises it.</summary>
    public event Action<Element>? FocusChanged;
    /// <summary>The effective enabled state, including ancestors.</summary>
    public bool IsEffectivelyEnabled => IsEnabled && (Parent?.IsEffectivelyEnabled ?? true);
    /// <summary>Temporarily excludes this subtree from input routing and focus navigation, independently of styled enablement.</summary>
    public bool IsInputBlocked
    {
        get => _inputBlocked;
        set
        {
            VerifyAccess();
            if (_inputBlocked == value) return;
            _inputBlocked = value;
            Invalidate(Invalidation.Render);
        }
    }
    /// <summary>Explicit width; NaN selects automatic sizing.</summary>
    public float Width { get => Get(UiProperties.Width); set => Set(UiProperties.Width, value); }
    /// <summary>Explicit height; NaN selects automatic sizing.</summary>
    public float Height { get => Get(UiProperties.Height); set => Set(UiProperties.Height, value); }
    /// <summary>Minimum width.</summary>
    [FluentSetter]
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
    public Thickness Padding { get => Get(UiProperties.Padding); set => Set(UiProperties.Padding, value); }
    /// <summary>The control's theme padding when no local or style value is present.</summary>
    protected virtual Thickness ThemePadding => default;
    /// <summary>Background when no local/style value is supplied.</summary>
    protected virtual Color ThemeBackground => Color.Transparent;
    /// <summary>Optional gradient replacing the default background fill on a window surface.</summary>
    protected virtual WindowGradient? ThemeBackgroundGradient => null;
    /// <summary>Optional background material when no local or styled value is supplied.</summary>
    protected virtual BackdropMaterial? ThemeBackdrop => null;
    /// <summary>Whether to apply the theme's glass treatment to the default background.</summary>
    protected virtual bool UsesThemedControlBackdrop => false;
    /// <summary>Opacity of the themed glass tint. Semantic action fills keep strong contrast with their labels by default.</summary>
    protected virtual byte ThemedControlTintAlpha => IsEffectivelyEnabled && Tone != ControlTone.Neutral
        ? Math.Max(Theme.ControlTintAlpha, (byte)250) : Theme.ControlTintAlpha;
    private BackdropMaterial? _themedControlBackdrop;
    /// <summary>Outer shadow when no local or styled value is supplied.</summary>
    protected virtual BoxShadow? ThemeShadow => null;
    /// <summary>Foreground when no local/style value is supplied.</summary>
    protected virtual Color ThemeForeground => Theme.Foreground;
    /// <summary>Corner radius when no local or styled value is supplied.</summary>
    protected virtual float ThemeCornerRadius => SupportsValidationIcon ? Theme.EditorCornerRadius : Theme.CornerRadius;
    internal virtual bool SquareDescendantButtons => Parent?.SquareDescendantButtons ?? false;
    /// <summary>Semantic editor role inherited by composite parts unless explicitly overridden.</summary>
    protected virtual EditorColorRole ThemeColorRole => Parent?.ColorRole ?? EditorColorRole.Neutral;
    /// <summary>Inside outline width when no local/style value is supplied.</summary>
    protected virtual float ThemeBorderWidth => 0;
    /// <summary>Outline color when no local, element style or view style value is supplied.</summary>
    protected virtual Color ThemeBorderColor => !IsEffectivelyEnabled ? Theme.Border :
        HasError ? Theme.Error : IsFocused ? Theme.Focus : IsModified ? Theme.Modified : IsHovered ? Theme.BorderHover :
        ColorRole == EditorColorRole.Neutral ? Theme.Border : Theme.Mix(Theme.Border, Theme.EditorColor(ColorRole), .4f);
    /// <summary>Pending edit state; does not change layout or commit data.</summary>
    public bool IsModified { get => Get(UiProperties.IsModified); set => Set(UiProperties.IsModified, value); }
    /// <summary>Semantic editor accent independent of validation and focus.</summary>
    public EditorColorRole ColorRole { get => Get(UiProperties.ColorRole); set => Set(UiProperties.ColorRole, value); }
    /// <summary>Semantic action tone.</summary>
    public ControlTone Tone { get => Get(UiProperties.Tone); set => Set(UiProperties.Tone, value); }
    /// <summary>Visual error state; text editors use a right-edge icon, other controls a structural marker.</summary>
    public bool HasError { get => Get(UiProperties.HasError); set => Set(UiProperties.HasError, value); }
    /// <summary>Validator explanation for the error icon's tooltip; does not mutate the value.</summary>
    public string ErrorMessage { get => Get(UiProperties.ErrorMessage); set => Set(UiProperties.ErrorMessage, value); }
    /// <summary>Whether this control reserves a right-edge validation icon.</summary>
    protected virtual bool SupportsValidationIcon => false;
    /// <summary>Whether direct editor children borrow this control's validation state when they have no override.</summary>
    protected virtual bool SharesValidationWithEditors => false;
    /// <summary>Root-coordinate validation badge bounds. The square spans the field height and is clipped for narrower fields.</summary>
    public Rect ValidationIconBounds
    {
        get
        {
            if (!SupportsValidationIcon || !HasError) return default;
            var width = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height));
            if (width == 0 || Bounds.Height <= 0) return default;
            return new(Bounds.X + Bounds.Width - width, Bounds.Y, width, Bounds.Height);
        }
    }
    /// <summary>Text viewport excluding padding and the validation icon; caret and selection share this geometry.</summary>
    protected Rect EditorContentBounds
    {
        get
        {
            var area = Bounds.Deflate(Padding); var icon = ValidationIconBounds;
            return icon.Width <= 0 ? area : area with { Width = Math.Max(0, Math.Min(area.Width, icon.X - 4 - area.X)) };
        }
    }
    /// <summary>Semantic name for icon-only controls and future accessibility adapters.</summary>
    [FluentSetter]
    public string AccessibleName { get => Get(UiProperties.AccessibleName); set => Set(UiProperties.AccessibleName, value); }
    /// <summary>Explicit stable target identity for semantic snapshots in the owning UI session.</summary>
    public string? SemanticId { get => Get(UiProperties.SemanticId); set => Set(UiProperties.SemanticId, value); }
    /// <summary>Effective corner radius.</summary>
    public float CornerRadius { get => Get(UiProperties.CornerRadius); set => Set(UiProperties.CornerRadius, value); }
    /// <summary>Effective inside outline width.</summary>
    public float BorderWidth { get => Get(UiProperties.BorderWidth); set => Set(UiProperties.BorderWidth, value); }
    /// <summary>Effective normal outline color.</summary>
    public Color BorderColor { get => Get(UiProperties.BorderColor); set => Set(UiProperties.BorderColor, value); }
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
    /// <summary>Opacity applied once to this element and all its children; does not change input admission.</summary>
    public float Opacity { get => Get(UiProperties.Opacity); set => Set(UiProperties.Opacity, value); }
    /// <summary>Optional backdrop material, painted before the ordinary background fill.</summary>
    public BackdropMaterial? Backdrop { get => Get(UiProperties.Backdrop); set => Set(UiProperties.Backdrop, value); }
    /// <summary>Optional outer shadow; it changes painting only, within the ancestor clip.</summary>
    public BoxShadow? Shadow { get => Get(UiProperties.Shadow); set => Set(UiProperties.Shadow, value); }
    /// <summary>Font size.</summary>
    public float FontSize { get => Get(UiProperties.FontSize); set => Set(UiProperties.FontSize, value); }
    /// <summary>Font family.</summary>
    public string FontFamily { get => Get(UiProperties.FontFamily); set => Set(UiProperties.FontFamily, value); }
    /// <summary>Font weight100..900;400 is regular and600 semibold.</summary>
    public int FontWeight { get => Get(UiProperties.FontWeight); set => Set(UiProperties.FontWeight, value); }
    /// <summary>Whether to select an italic face.</summary>
    public bool FontItalic { get => Get(UiProperties.FontItalic); set => Set(UiProperties.FontItalic, value); }
    /// <summary>Raised for a local typed property change.</summary>
    public event Action<Element, object>? PropertyChanged;
    /// <summary>Raised when this element or a descendant needs work.</summary>
    public event Action<Invalidation>? Invalidated;
    /// <summary>Raised after the entire subtree attachment changes. Queue follow-up tree mutations through the host.</summary>
    public event Action? AttachmentChanged;

    /// <summary>Gets a local, element-styled, view-styled, themed, or default value in that order.</summary>
    public T Get<T>(UiProperty<T> property)
    {
        if (_values.TryGetValue(property, out var value)) return (T)value!;
        if (_style != null && _style.TryGet(property, out T styled)) return styled;
        if (_viewStyle != null && _viewStyle.TryGet(property, out T viewStyled)) return viewStyled;
        if (SupportsValidationIcon && Parent?.SharesValidationWithEditors == true &&
            (ReferenceEquals(property, UiProperties.HasError) || ReferenceEquals(property, UiProperties.ErrorMessage)))
            return Parent.Get(property);
        if (ReferenceEquals(property, UiProperties.Background)) return (T)(object)ThemeBackground;
        if (ReferenceEquals(property, UiProperties.Backdrop)) return (T)(object?)ThemeBackdrop!;
        if (ReferenceEquals(property, UiProperties.Shadow)) return (T)(object?)ThemeShadow!;
        if (ReferenceEquals(property, UiProperties.Foreground)) return (T)(object)ThemeForeground;
        if (ReferenceEquals(property, UiProperties.CornerRadius)) return (T)(object)ThemeCornerRadius;
        if (ReferenceEquals(property, UiProperties.BorderWidth)) return (T)(object)ThemeBorderWidth;
        if (ReferenceEquals(property, UiProperties.BorderColor)) return (T)(object)ThemeBorderColor;
        if (ReferenceEquals(property, UiProperties.FontSize)) return (T)(object)Theme.FontSize;
        if (ReferenceEquals(property, UiProperties.FontFamily)) return (T)(object)Theme.FontFamily;
        if (ReferenceEquals(property, UiProperties.FontWeight)) return (T)(object)Theme.FontWeight;
        if (ReferenceEquals(property, UiProperties.FontItalic)) return (T)(object)Theme.FontItalic;
        if (ReferenceEquals(property, UiProperties.Padding)) return (T)(object)ThemePadding;
        if (ReferenceEquals(property, UiProperties.Spacing)) return (T)(object)Theme.StackSpacing;
        if (ReferenceEquals(property, UiProperties.ColorRole)) return (T)(object)ThemeColorRole;
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

    internal virtual ElementScope? OwnedScope => null;
    internal ViewDependencyOwnership? DependencyOwnership { get; set; }
    internal string? PartName { get; private set; }

    internal void SetPartName(string name)
    {
        VerifyTreeAccess();
        if (Parent != null || IsAttached)
            throw new InvalidOperationException("Name a part before adding it to a parent or attaching it.");
        OwnedScope?.ValidateRootName(name);
        PartName = name;
    }

    internal void ValidateChild(Element child)
    {
        VerifyTreeAccess();
        ArgumentNullException.ThrowIfNull(child);
        ObjectDisposedException.ThrowIf(child.IsDisposed, child);
        if (child.Parent != null || child.IsAttached) throw new InvalidOperationException("An element can have only one owner.");
        if (child.DependencyOwnership?.DraftOwner is { } owner && !ReferenceEquals(owner, this))
            throw new InvalidOperationException("A view-owned draft must be transferred by its owner.");
        for (Element? node = this; node != null; node = node.Parent)
            if (ReferenceEquals(node, child)) throw new InvalidOperationException("An element cannot contain itself or an ancestor.");
        child.ValidateUnattachedSubtree();
        if (IsAttached) child.PrepareAttachmentSubtree(_controlFactories, this);
        // Validate theme-derived rules before a container commits ownership changes.
        if (child.OwnedScope == null) FindScope()?.ValidateTheme(Theme);
        child.ValidateScopeThemes(Theme);
    }

    private void ValidateScopeThemes(Theme theme)
    {
        OwnedScope?.ValidateTheme(theme);
        var children = Children;
        for (var i = 0; i < children.Count; i++) children[i].ValidateScopeThemes(theme);
    }

    internal void ValidateChildChange(IReadOnlyList<Element> added, IReadOnlyList<Element> removed)
    {
        foreach (var child in added) ValidateChild(child);
        FindScope()?.ValidatePartChanges(added, removed);
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
        child.DependencyOwnership?.Release(child);
        FindScope()?.CommitPartChange(child, added: true);
        child.ApplyThemeCore(Theme, change);
        if (_verifyAccess != null) child.AttachCore(_verifyAccess, _post!, _detaching, _controlFactories, change);
        change.Schedule(() => Invalidate(Invalidation.Tree | Invalidation.Layout));
    }
    internal void Orphan(Element child, TreeChange change)
    {
        child.Parent = null;
        FindScope()?.CommitPartChange(child, added: false);
        child.RefreshViewStyles();
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
    /// <param name="controlFactories">Borrowed factory registry for declared view content and later insertions.</param>
    public void Attach(Action verifyAccess, Action<Action> post, Action<Element>? detaching, IUiControlFactories? controlFactories = null)
    {
        ArgumentNullException.ThrowIfNull(verifyAccess);
        ArgumentNullException.ThrowIfNull(post);
        verifyAccess();
        ValidateUnattachedSubtree();
        if (Parent != null) throw new InvalidOperationException("Only an unowned root can be attached directly.");
        if (DependencyOwnership?.DraftOwner != null)
            throw new InvalidOperationException("A view-owned draft must be transferred by its owner.");
        PrepareAttachmentSubtree(controlFactories);
        if (PresentationRootForAttach is { } presentationRoot)
            Window.ValidatePresentationTree(this, presentationRoot);
        using var change = new TreeChange(this);
        AttachCore(verifyAccess, post, detaching, controlFactories, change);
        DependencyOwnership?.Release(this);
        change.Complete();
    }

    private protected virtual void PrepareForAttach(IUiControlFactories? factories, Element? attachedParent) { }

    private void PrepareAttachmentSubtree(IUiControlFactories? factories, Element? attachedParent = null)
    {
        PrepareForAttach(factories, attachedParent);
        foreach (var child in Children) child.PrepareAttachmentSubtree(factories, attachedParent);
    }

    private void AttachCore(Action verifyAccess, Action<Action> post, Action<Element>? detaching,
        IUiControlFactories? controlFactories, TreeChange change)
    {
        var nodes = Subtree().ToArray();
        foreach (var node in nodes)
        {
            node._verifyAccess = verifyAccess;
            node._post = post;
            node._detaching = detaching;
            node._controlFactories = controlFactories;
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
            node._controlFactories = null;
            Interlocked.Increment(ref node._attachmentVersion);
            var focused = node._focused;
            node._hovered = node._focused = false;
            if (focused) change.Schedule(() => node.FocusChanged?.Invoke(node));
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
        List<(Element Node, Style? Value)> pending = [];
        ResolveSubtreeStyles(Parent?.FindScope(), theme, pending);
        foreach (var (node, value) in pending) { node.Theme = theme; node._viewStyle = value; }
        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var node = pending[i].Node;
            change.Schedule(() => node.Invalidate(Invalidation.Layout));
        }
    }
    internal ElementScope? FindScope()
    {
        for (Element? node = this; node != null; node = node.Parent)
            if (node.OwnedScope is { } scope) return scope;
        return null;
    }
    internal void SetViewStyleValue(Style? style) => _viewStyle = style;
    internal bool HasViewStyleRole(object role) => _viewStyleRoles?.Contains(role) == true;
    internal void AddViewStyleRole(object role)
    {
        VerifyAccess();
        if (!(_viewStyleRoles ??= []).Add(role)) return;
        _viewStyle = FindScope()?.ResolveStyle(this, Theme);
        Invalidate(Invalidation.Layout | Invalidation.Render);
    }
    private void RefreshViewStyles()
    {
        List<(Element Node, Style? Value)> pending = [];
        ResolveSubtreeStyles(null, null, pending);
        foreach (var (node, value) in pending) node._viewStyle = value;
    }

    private void ResolveSubtreeStyles(ElementScope? inherited, Theme? theme, List<(Element Node, Style? Value)> pending)
    {
        var scope = OwnedScope ?? inherited;
        pending.Add((this, scope?.ResolveStyle(this, theme ?? Theme)));
        var children = Children;
        for (var i = 0; i < children.Count; i++) children[i].ResolveSubtreeStyles(scope, theme, pending);
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

    internal bool HasEditorOutline => SupportsValidationIcon;
    internal bool HasSharedValidation => SharesValidationWithEditors;
    internal WindowGradient? EffectiveBackgroundGradient =>
        OverridesAppearance(UiProperties.Background) ? null : ThemeBackgroundGradient;
    internal bool HasThemedGlass => UsesThemedControlBackdrop &&
        !OverridesAppearance(UiProperties.Backdrop) && !OverridesAppearance(UiProperties.Background);
    internal Color GlassTint(Color color) => color with { A = ThemedControlTintAlpha };

    internal BackdropMaterial ControlBackdrop(Color tint, Color color)
    {
        var cached = _themedControlBackdrop;
        if (cached == null || cached.Tint != tint || cached.BlurSigma != Theme.ControlBlurSigma)
            _themedControlBackdrop = cached = new(Theme.ControlBlurSigma, tint, color);
        return cached;
    }

    private bool OverridesAppearance<T>(UiProperty<T> property) =>
        _values.ContainsKey(property) || _style?.TryGet(property, out _) == true || _viewStyle?.TryGet(property, out _) == true;
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
            Invalidated = null; PropertyChanged = null; AttachmentChanged = null; FocusChanged = null;
            _disposed = true;
            DependencyOwnership?.Release(this);
            _disposing = false;
        }
        change.Complete();
    }
}
