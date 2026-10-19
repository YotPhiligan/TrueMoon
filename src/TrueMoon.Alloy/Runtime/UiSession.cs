using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

/// <summary>Owns one retained UI tree and render surface. Post, InvokeAsync and RequestFrame accept calls from any thread.</summary>
public sealed class UiSession : IDisposable, IInputContext
{
    private static long _nextId;
    private volatile UiDiagnostics? _diagnostics;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly UiDispatcher _posted;
    private readonly IUiClipboard? _clipboard;
    private Element? _focus, _capture, _hover;
    private Invalidation _dirty = Invalidation.Tree | Invalidation.Layout | Invalidation.Render;
    private volatile bool _disposed;
    private bool _updating, _disposing, _reportingFailure;
    private volatile UiRenderingException? _renderingFailure;
    /// <summary>Stable identity within this process; independent of diagnostics being enabled.</summary>
    public long Id { get; } = Interlocked.Increment(ref _nextId);
    /// <summary>Optional fixed-storage diagnostics. Null until explicitly enabled; retained after disposal.</summary>
    public UiDiagnostics? Diagnostics => _diagnostics;
    /// <summary>Enables observation from now on, including the current queue backlog. Repeated calls return the first collector.</summary>
    /// <param name="windowId">Optional positive host window identity; captured only on the first call.</param>
    /// <returns>The collector, whose snapshots remain readable on any thread after disposal.</returns>
    public UiDiagnostics EnableDiagnostics(long? windowId = null)
    {
        VerifyAccess();
        if (windowId is <= 0) throw new ArgumentOutOfRangeException(nameof(windowId));
        if (_diagnostics != null) return _diagnostics;
        var diagnostics = new UiDiagnostics(Id, windowId);
        SetDiagnosticState(diagnostics);
        _posted.EnableDiagnostics(diagnostics); _diagnostics = diagnostics;
        return diagnostics;
    }
    private void SetDiagnosticState(UiDiagnostics? diagnostics) => diagnostics?.State(
        _disposed ? UiSessionState.Disposed : _disposing ? UiSessionState.Disposing : IsFaulted ? UiSessionState.Faulted : Viewport.IsEmpty ? UiSessionState.Suspended : UiSessionState.Active,
        Viewport, _renderingFailure?.Kind);
    /// <summary>The tree owned and disposed by this session.</summary>
    public Element Root { get; }
    /// <summary>The surface owned by this session; backend extensions can acquire its output.</summary>
    public IUiRenderSurface Rendering { get; }
    /// <summary>Current framebuffer size and DPI scale.</summary>
    public UiViewport Viewport { get; private set; }
    /// <summary>Whether a frame/layout update is needed.</summary>
    public bool NeedsUpdate => _dirty != 0 || _posted.HasPending;
    /// <summary>Whether the tree/viewport has unapplied visual invalidation. Queued commands and requests
    /// for future frames leave the last completed output usable until they are applied by Update.</summary>
    public bool NeedsRender => _dirty != 0;
    /// <summary>Immutable dispatch limits selected when this session was created.</summary>
    public UiDispatchOptions DispatchOptions { get; }
    /// <summary>Whether the session has released its resources.</summary>
    public bool IsDisposed => _disposed;
    /// <summary>The first terminal rendering failure, retained after disposal.</summary>
    public UiRenderingException? RenderingFailure => _renderingFailure;
    /// <summary>Whether the host must explicitly recreate this session.</summary>
    public bool IsFaulted => _renderingFailure != null;
    /// <summary>Raised after disposal; hosting removes the session from its registry.</summary>
    public event Action<UiSession>? Disposed;
    /// <summary>Reports operational clipboard failures during routed input on the owner thread. Handler errors propagate.</summary>
    public event Action<UiClipboardException>? ClipboardFailed;
    /// <summary>Reports the first terminal graphics failure on the owner thread. Dispose after the failing operation unwinds.</summary>
    public event Action<UiRenderingException>? RenderingFailed;

    /// <summary>Attaches an unattached root. Ownership transfers only when construction succeeds.</summary>
    public UiSession(Element root, IUiRenderSurface rendering, UiViewport viewport, IUiClipboard? clipboard = null)
        : this(root, rendering, viewport, clipboard, new UiDispatchOptions()) { }

    /// <summary>Attaches an unattached root with explicit dispatch limits. Ownership transfers only on success.</summary>
    public UiSession(Element root, IUiRenderSurface rendering, UiViewport viewport, IUiClipboard? clipboard, UiDispatchOptions dispatchOptions)
    {
        ArgumentNullException.ThrowIfNull(root); ArgumentNullException.ThrowIfNull(rendering); viewport.Validate();
        ArgumentNullException.ThrowIfNull(dispatchOptions); dispatchOptions.Validate();
        DispatchOptions = dispatchOptions; _posted = new UiDispatcher(dispatchOptions);
        if (root.Parent != null || root.IsAttached || root.IsDisposed) throw new ArgumentException("Root is already owned or disposed.", nameof(root));
        Root = root; Rendering = rendering; Viewport = viewport; _clipboard = clipboard;
        try { Root.Attach(VerifyAccess, Post, ElementDetaching); }
        catch (Exception attachError)
        {
            try { Root.Detach(); }
            catch (Exception detachError) { throw new AggregateException("UI attachment and rollback failed.", attachError, detachError); }
            throw;
        }
        Root.Invalidated += Invalidated;
    }
    /// <summary>Creates a session directly without App/DI. The session owns its root and created surface.</summary>
    public static UiSession Create(Element root, IRenderBackend backend, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
        => Create(root, backend, target, viewport, clipboard, new UiDispatchOptions());

    /// <summary>Creates a directly owned session with explicit dispatch limits, without App/DI.</summary>
    public static UiSession Create(Element root, IRenderBackend backend, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard, UiDispatchOptions dispatchOptions)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(dispatchOptions); dispatchOptions.Validate();
        var surface = backend.CreateSurface(target, viewport);
        try { return new UiSession(root, surface, viewport, clipboard, dispatchOptions); }
        catch (Exception error) { UiCleanup.Complete(error, surface.Dispose); throw; }
    }
    /// <summary>Queues synchronous work in FIFO order. Capacity/lifetime errors throw immediately;
    /// callback errors propagate from Update. Remaining commands wait for a later Update when its budget is exhausted.</summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action); _posted.Post(action);
    }
    /// <summary>Coalesces a render request without consuming command capacity. Safe on any thread;
    /// the next Update redraws dynamic content even when the tree has not changed. A suspended
    /// session retains invalidation until it has a nonempty viewport. Closed/faulted sessions reject requests.</summary>
    public void RequestFrame() => _posted.RequestFrame();
    internal void SetUpdateSignal(Action? signal) => _posted.SetUpdateSignal(signal);
    /// <summary>Runs a synchronous callback immediately on the owner, or queues it from another thread.
    /// The returned task observes completion, rejection and callback errors. Cancellation removes pending
    /// work but cannot interrupt a started callback. Owner execution bypasses the queue and its budget.
    /// Await from workers; blocking the owner on work previously queued by a worker is unsupported.
    /// Async callbacks and marshaling of task continuations are not provided.</summary>
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { return _posted.InvokeAsync(action, cancellationToken, Environment.CurrentManagedThreadId == _thread); }
        catch (UiRenderingException error)
        {
            try { ReportRenderingFailure(error); }
            catch (Exception reportingError) { return Task.FromException(reportingError); }
            return Task.FromException(error);
        }
    }
    /// <summary>Checks thread affinity and session lifetime, including during fault cleanup.</summary>
    public void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("UI session access requires its owner thread.");
    }
    /// <summary>Checks thread/lifetime and terminal failure before rendering, export or input.</summary>
    public void VerifyRendering() { VerifyAccess(); ThrowIfFaulted(); }
    private void ThrowIfFaulted()
    {
        if (_renderingFailure is { } failure)
            throw new InvalidOperationException("UI rendering has failed. Dispose this session and explicitly create a new tree/session from the model.", failure);
    }
    /// <summary>Reports a confirmed terminal host/backend failure outside Update, such as presentation. Does not dispose borrowed frames.</summary>
    /// <param name="failure">The terminal failure; the first error remains authoritative.</param>
    /// <returns>True for the first report. Notification/cancellation errors aggregate with that failure.</returns>
    public bool ReportRenderingFailure(UiRenderingException failure)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(failure);
        if (_renderingFailure != null) return false;
        _renderingFailure = failure; _reportingFailure = true;
        SetDiagnosticState(_diagnostics);
        try
        {
            _posted.Close(failure);
            var errors = new List<Exception>();
            try { CancelInput(); } catch (Exception error) { errors.Add(error); }
            finally { _focus = null; _capture = null; _hover = null; }
            var handlers = RenderingFailed;
            if (handlers != null) foreach (Action<UiRenderingException> handler in handlers.GetInvocationList())
                try { handler(failure); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Rendering failure reporting failed.", new Exception[] { failure }.Concat(errors));
            return true;
        }
        finally { _reportingFailure = false; }
    }
    private void Invalidated(Invalidation reason) { VerifyAccess(); _dirty |= reason | Invalidation.Render; _posted.SignalUpdate(); }
    /// <summary>Changes viewport; rejects resize while the host has borrowed the current frame.</summary>
    public void Resize(UiViewport viewport)
    {
        VerifyRendering(); viewport.Validate(); if (viewport == Viewport) return;
        try { Rendering.VerifyAvailable(); Rendering.Resize(viewport); }
        catch (UiRenderingException error) { ReportRenderingFailure(error); throw; }
        Viewport = viewport; Invalidated(Invalidation.Layout);
        SetDiagnosticState(_diagnostics);
    }
    /// <summary>Applies a theme to the tree.</summary>
    public void SetTheme(Theme theme) { VerifyRendering(); ArgumentNullException.ThrowIfNull(theme); Root.ApplyTheme(theme); }
    /// <summary>Runs posted work, layout and drawing only when invalidated. Returns whether a frame was drawn.</summary>
    public bool Update()
    {
        VerifyRendering(); if (_updating) throw new InvalidOperationException("UI Update cannot be reentrant.");
        _updating = true;
        var diagnostics = _diagnostics;
        var started = diagnostics?.Begin(UiPhase.Update) ?? 0;
        var completed = false;
        try
        {
            Rendering.VerifyAvailable();
            _posted.Drain();
            if (_posted.TakeFrameRequest()) _dirty |= Invalidation.Render;
            VerifyRendering();
            ClearDetachedInput();
            if (Viewport.IsEmpty || _dirty == 0) { completed = true; return false; }
            var work = _dirty; _dirty = 0;
            try
            {
                if ((work & (Invalidation.Layout | Invalidation.Tree)) != 0)
                {
                    var logical = new Size(Viewport.Width / Viewport.Scale, Viewport.Height / Viewport.Scale);
                    Layout(logical, diagnostics);
                }
                VerifyRendering(); Draw(diagnostics); completed = true; return true;
            }
            catch { _dirty |= work; throw; }
        }
        catch (UiRenderingException error) { ReportRenderingFailure(error); throw; }
        finally { _updating = false; diagnostics?.End(UiPhase.Update, started, completed); }
    }
    private void Layout(Size logical, UiDiagnostics? diagnostics)
    {
        var started = diagnostics?.Begin(UiPhase.Measure) ?? 0;
        try { Root.Measure(logical, Rendering.TextLayout); }
        catch { diagnostics?.End(UiPhase.Measure, started, false); throw; }
        diagnostics?.End(UiPhase.Measure, started, true);
        started = diagnostics?.Begin(UiPhase.Arrange) ?? 0;
        try { Root.Arrange(new Rect(0, 0, logical.Width, logical.Height)); }
        catch { diagnostics?.End(UiPhase.Arrange, started, false); throw; }
        diagnostics?.End(UiPhase.Arrange, started, true);
    }
    private void Draw(UiDiagnostics? diagnostics)
    {
        var started = diagnostics?.Begin(UiPhase.Render) ?? 0;
        try { Rendering.Render(Root, Viewport); }
        catch { diagnostics?.End(UiPhase.Render, started, false); throw; }
        diagnostics?.End(UiPhase.Render, started, true);
    }
    /// <summary>Routes input from the host in logical coordinates and reports consumption/capture.</summary>
    public UiInputResult HandleInput(UiInput input)
    {
        VerifyRendering(); ClearDetachedInput();
        if (input.Kind == InputKind.FocusLost) { CancelInput(); return new(false, false, false); }
        if (Viewport.IsEmpty) return new(false, false, _focus != null);
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Tab)
        {
            if (_focus != null && _focus.HandleInput(input, this))
                return new(true, _capture != null, true);
            var items = Descendants(Root).Where(e => e.Focusable && Eligible(e)).ToArray();
            if (items.Length == 0) return new(false, _capture != null, false);
            var index = Array.IndexOf(items, _focus); if (index < 0 && input.Shift) index = 0;
            Focus(items[(index + (input.Shift ? items.Length - 1 : 1) + items.Length) % items.Length]);
            return new(true, _capture != null, true);
        }
        var pointer = input.Kind is InputKind.PointerMove or InputKind.PointerDown or InputKind.PointerUp or InputKind.Wheel;
        if (pointer)
        {
            var hover = Hit(Root, input.X, input.Y);
            if (_hover != hover) { if (_hover != null) _hover.IsHovered = false; _hover = hover; if (_hover != null) _hover.IsHovered = true; }
        }
        var target = pointer ? _capture ?? Hit(Root, input.X, input.Y) : _focus;
        var handled = false;
        try
        {
            for (var node = target; node != null && Eligible(node); node = node.Parent)
                if (node.HandleInput(input, this)) { handled = true; break; }
        }
        catch (UiClipboardException error) { handled = true; ClipboardFailed?.Invoke(error); }
        if (input.Kind == InputKind.PointerDown && input.Button == 0 && !handled) SetFocus(null);
        return new(handled, _capture != null, _focus != null);
    }
    private static IEnumerable<Element> Descendants(Element root)
    { yield return root; foreach (var child in root.Children) foreach (var item in Descendants(child)) yield return item; }
    private bool Eligible(Element element)
    {
        if (element.IsDisposed || !element.IsAttached || !element.IsEffectivelyEnabled) return false;
        for (Element? node = element; node != null; node = node.Parent) { if (!node.IsVisible) return false; if (node == Root) return true; }
        return false;
    }
    private Element? Hit(Element element, float x, float y)
    {
        if (!Eligible(element) || !element.Bounds.Contains(x, y)) return null;
        if (element.ChildClipBounds.Contains(x, y))
            for (var i = element.Children.Count - 1; i >= 0; i--) { var hit = Hit(element.Children[i], x, y); if (hit != null) return hit; }
        return element;
    }
    private void ClearDetachedInput()
    {
        if (_focus != null && !Eligible(_focus)) SetFocus(null);
        if (_capture != null && !Eligible(_capture)) { if (!_capture.IsDisposed) _capture.OnInputCancelled(); _capture = null; }
        if (_hover != null && !Eligible(_hover)) { if (!_hover.IsDisposed) _hover.IsHovered = false; _hover = null; }
    }
    private void ElementDetaching(Element element)
    {
        if (_focus == element) _focus = null; if (_capture == element) _capture = null; if (_hover == element) _hover = null;
    }
    private void SetFocus(Element? element)
    {
        if (_focus == element) return;
        if (_focus != null && !_focus.IsDisposed) { _focus.IsFocused = false; _focus.OnInputCancelled(); }
        _focus = element; if (_focus != null) _focus.IsFocused = true;
    }
    /// <inheritdoc />
    public void Focus(Element element)
    {
        VerifyRendering(); if (!Eligible(element) || !element.Focusable) throw new ArgumentException("Element cannot receive focus.");
        SetFocus(element);
        var bounds = element.Bounds;
        for (var ancestor = element.Parent; ancestor != null; ancestor = ancestor.Parent)
            if (ancestor is ScrollViewer scroll)
            {
                var previous = scroll.Offset; scroll.BringIntoView(bounds);
                bounds = bounds with { Y = bounds.Y - (scroll.Offset - previous) };
            }
    }
    /// <inheritdoc />
    public void Capture(Element element)
    { VerifyRendering(); if (!Eligible(element)) throw new ArgumentException("Element is not in this session."); if (_capture != element) _capture?.OnInputCancelled(); _capture = element; }
    /// <inheritdoc />
    public void ReleaseCapture(Element element) { VerifyRendering(); if (_capture == element) _capture = null; }
    /// <inheritdoc />
    public string? GetClipboardText() { VerifyRendering(); return _clipboard?.GetText(); }
    /// <inheritdoc />
    public void SetClipboardText(string text) { VerifyRendering(); _clipboard?.SetText(text); }
    private void CancelInput()
    {
        var focus = _focus; var capture = _capture; var hover = _hover;
        _focus = null; _capture = null; _hover = null;
        UiCleanup.Complete(null,
            () => { if (focus is { IsDisposed: false }) focus.IsFocused = false; },
            () => { if (focus is { IsDisposed: false }) focus.OnInputCancelled(); },
            () => { if (capture is { IsDisposed: false } && capture != focus) capture.OnInputCancelled(); },
            () => { if (hover is { IsDisposed: false }) hover.IsHovered = false; });
    }
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        VerifyAccess(); Rendering.VerifyAvailable();
        if (_updating) throw new InvalidOperationException("UI session cannot be disposed during Update.");
        if (_disposing || _reportingFailure) throw new InvalidOperationException("UI session disposal cannot be reentrant or occur during failure notification.");
        if (!Root.IsDisposed) Root.VerifyTreeAccess();
        _disposing = true;
        SetDiagnosticState(_diagnostics);
        _posted.Close();
        var errors = new List<Exception>();
        void Release(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
        Release(CancelInput); Root.Invalidated -= Invalidated;
        Release(Root.Dispose); Release(Rendering.Dispose);
        _disposed = true; _disposing = false;
        SetDiagnosticState(_diagnostics);
        var handlers = Disposed; Disposed = null; ClipboardFailed = null; RenderingFailed = null;
        if (handlers != null) foreach (Action<UiSession> handler in handlers.GetInvocationList()) Release(() => handler(this));
        if (errors.Count != 0) throw new AggregateException("UI session cleanup failed.", errors);
    }
}
