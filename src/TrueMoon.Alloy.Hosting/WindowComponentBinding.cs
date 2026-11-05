using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting;

// Owns only the exclusive component connection. Native resources, controller and UI tree are borrowed.
internal sealed class WindowComponentBinding : IDisposable
{
    private readonly Window _component;
    private readonly WindowHostConnection _connection;
    private readonly Action<string> _title;
    private readonly Action<Size> _resize;
    private string _lastTitle;
    private Size _lastSize;
    internal WindowComponentBinding(Window component, IWindowController controller, Action<string> title, Action<Size> resize)
    {
        ArgumentNullException.ThrowIfNull(component); ArgumentNullException.ThrowIfNull(title); ArgumentNullException.ThrowIfNull(resize);
        _component = component; _title = title; _resize = resize;
        _lastTitle = component.Title; _lastSize = component.ClientSize;
        _connection = component.AttachHost(controller);
    }
    internal static void ValidateRoot(Element root)
    {
        if (root.IsDisposed || root.IsAttached || root.Parent != null || root is Window { IsHosted: true })
            throw new InvalidOperationException("The root factory must return a fresh, detached root.");
        Visit(root, true);
        static void Visit(Element element, bool root)
        {
            if (!root && element is Window) throw new InvalidOperationException("A Window must be the presentation root, not an embedded child.");
            foreach (var child in element.Children) Visit(child, false);
        }
    }
    internal static void Configure(Window component, SilkUiWindowOptions options)
    {
        options.Title = component.Title; options.Width = (int)component.ClientSize.Width; options.Height = (int)component.ClientSize.Height;
        options.ValidateChrome();
    }
    // Poll effective values as well as local changes: styles, Clear and theme changes must reach the native caption.
    internal void Synchronize(WindowPresentationState state, bool active, Size size)
    {
        // Fence lifetime/thread and validate observations before any native side effect.
        _connection.VerifyAccess();
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (!float.IsFinite(size.Width) || !float.IsFinite(size.Height) || size.Width < 0 || size.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        var title = _component.Title;
        if (_lastTitle != title) { _title(title); _lastTitle = title; }
        _connection.VerifyAccess();
        var requested = _component.ClientSize;
        if (_lastSize != requested) { _resize(requested); _lastSize = requested; }
        _connection.Synchronize(state, active, size);
    }
    internal Func<UiCloseReason, Task<bool>>[] SnapshotClosingHandlers()
        => _connection.SnapshotClosingHandlers().Select(handler => new Func<UiCloseReason, Task<bool>>(reason => handler(reason switch
        {
            UiCloseReason.Application => WindowCloseReason.Application,
            UiCloseReason.Native => WindowCloseReason.Native,
            UiCloseReason.Shutdown => WindowCloseReason.Shutdown,
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        }))).ToArray();
    public void Dispose() => _connection.Dispose();
}
