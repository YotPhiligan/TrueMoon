namespace TrueMoon.Argentis;

/// <summary>A root view describing one standalone window without allocating platform or graphics resources.</summary>
/// <remarks>The host creates this view on its UI owner thread and owns the returned tree. Data, styles and
/// the controller are borrowed. ClientSize configures the native client area; inherited Width/Height only affect layout.</remarks>
public class Window : View
{
    /// <summary>The window caption, including the native taskbar caption when hosted.</summary>
    public static readonly UiProperty<string> TitleProperty = new("Title", "TrueMoon UI", validate: value => value != null);
    /// <summary>Requested client area in whole logical pixels, independently of content layout.</summary>
    public static readonly UiProperty<Size> ClientSizeProperty = new("ClientSize", new(960, 540), Invalidation.Layout,
        value => Dimension(value.Width) && Dimension(value.Height));
    private WindowHostConnection? _connection;
    private static bool Dimension(float value) => float.IsFinite(value) && value >= 1 && value <= 32768 && value == MathF.Truncate(value);

    /// <summary>Gets or sets the effective caption; bindings and styles use the ordinary Argentis priority.</summary>
    public string Title { get => Get(TitleProperty); set => Set(TitleProperty, value); }
    /// <summary>Gets or requests the client area. Native resize observations do not overwrite this request.</summary>
    public Size ClientSize { get => Get(ClientSizeProperty); set => Set(ClientSizeProperty, value); }
    /// <summary>Whether one host currently borrows this component's window actions.</summary>
    public bool IsHosted => _connection != null;
    /// <summary>Last observed native presentation state; Normal before hosting.</summary>
    public WindowPresentationState State { get; private set; }
    /// <summary>Last observed native focus state; false before hosting and after disconnect.</summary>
    public bool IsActive { get; private set; }
    /// <summary>Last observed client area, including zero while minimized; zero before hosting.</summary>
    public Size ActualClientSize { get; private set; }
    /// <summary>Raised on the UI owner when observed state changes or the host disconnects.</summary>
    public event Action<Window>? HostStateChanged;
    /// <summary>Owner-thread close callbacks. Return false to veto a user close; Shutdown drains all handlers and ignores vetoes.</summary>
    /// <remarks>The host snapshots handlers per attempt. After await, marshal UI work through the session.
    /// Never await RequestCloseAsync or the host's termination from a Closing callback.</remarks>
    public event Func<WindowCloseReason, Task<bool>>? Closing;

    /// <summary>Requests minimize through the connected host.</summary>
    public void Minimize() => Controller.Minimize();
    /// <summary>Requests maximize through the connected host; unsupported requests remain host errors.</summary>
    public void Maximize() => Controller.Maximize();
    /// <summary>Requests restore through the connected host.</summary>
    public void Restore() => Controller.Restore();
    /// <summary>Requests cancellable close and awaits actual host cleanup.</summary>
    /// <param name="cancellationToken">Cancels only this wait; an accepted close attempt continues.</param>
    /// <returns>False when vetoed, true after successful cleanup; failures fault the task.</returns>
    public Task<bool> RequestCloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Controller.RequestCloseAsync(cancellationToken);
    }
    private IWindowController Controller
    {
        get { VerifyAccess(); return _connection?.Controller ?? throw new InvalidOperationException("The Window has no connected host."); }
    }

    /// <summary>Connects a fresh root to a borrowed host without taking ownership of the controller.</summary>
    /// <param name="controller">Owner-thread window actions. Dispose the connection before disposing its UI tree.</param>
    /// <returns>The host's exclusive connection for observed state and close-handler snapshots.</returns>
    /// <remarks>A host connects before attaching/wrapping the root. It must reject Window components embedded in another view.</remarks>
    public WindowHostConnection AttachHost(IWindowController controller)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(controller);
        if (_connection != null || Parent != null || IsAttached)
            throw new InvalidOperationException("A Window must be a fresh, detached root with no other host.");
        return _connection = new WindowHostConnection(this, controller);
    }
    internal void VerifyConnection(WindowHostConnection connection)
    {
        VerifyAccess();
        if (!ReferenceEquals(_connection, connection)) throw new ObjectDisposedException(nameof(WindowHostConnection));
    }
    internal void Synchronize(WindowHostConnection connection, WindowPresentationState state, bool active, Size clientSize)
    {
        VerifyConnection(connection);
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (!float.IsFinite(clientSize.Width) || !float.IsFinite(clientSize.Height) || clientSize.Width < 0 || clientSize.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(clientSize));
        if (State == state && IsActive == active && ActualClientSize == clientSize) return;
        State = state; IsActive = active; ActualClientSize = clientSize;
        HostStateChanged?.Invoke(this);
    }
    internal Func<WindowCloseReason, Task<bool>>[] SnapshotClosingHandlers(WindowHostConnection connection)
    {
        VerifyConnection(connection);
        return Closing?.GetInvocationList().Cast<Func<WindowCloseReason, Task<bool>>>().ToArray() ?? [];
    }
    internal void Disconnect(WindowHostConnection connection)
    {
        if (!ReferenceEquals(_connection, connection)) return;
        _connection = null; IsActive = false;
        if (!IsDisposed) HostStateChanged?.Invoke(this);
    }
    /// <summary>Releases the owned content and subscriptions; borrowed data and controller are never disposed.</summary>
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally
        {
            if (IsDisposed)
            {
                Closing = null; HostStateChanged = null;
                _connection?.Dispose();
            }
        }
    }
}

/// <summary>A standalone root with typed borrowed data and the same content/style conventions as View&lt;TData&gt;.</summary>
/// <typeparam name="TData">Plain data or view-model type; the Window does not dispose it.</typeparam>
public class Window<TData> : Window, IView<TData>
{
    /// <inheritdoc />
    public TData? DataContext { get; set; }
    /// <summary>Assigns typed data and composes content once. Later updates should use bindings or targeted subtree changes.</summary>
    /// <param name="data">Borrowed screen data.</param><param name="factory">Creates the fresh content.</param>
    public void Content(TData? data, Func<TData?, object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        DataContext = data; Content(() => factory(DataContext));
    }
}
