using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Connects default Argentis frame widgets to borrowed window commands without owning native resources.</summary>
public sealed class DefaultWindowFrameBinding
{
    private readonly WindowFrame _frame;
    private readonly IWindowCommands _commands;
    /// <summary>Connects buttons once. Dispose the owning UI tree before disposing its window.</summary>
    /// <param name="frame">Default frame owned by a UI session.</param><param name="commands">Borrowed owner-thread window service.</param>
    /// <param name="closeRequest">Optional host close coordinator; otherwise uses the window's close request.</param>
    public DefaultWindowFrameBinding(WindowFrame frame, IWindowCommands commands, Action? closeRequest = null)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(commands);
        var title = frame.DefaultTitleBar ?? throw new ArgumentException("A default WindowFrame is required.", nameof(frame));
        frame.VerifyAccess(); _frame = frame; _commands = commands;
        title.MinimizeButton.Click += commands.Minimize;
        title.MaximizeButton.Click += () => { if (commands.State == UiWindowState.Maximized) commands.Restore(); else commands.Maximize(); };
        title.CloseButton.Click += closeRequest ?? commands.Close;
    }
    /// <summary>Refreshes presentation only when observed state changes; call on the window/UI owner thread.</summary>
    /// <param name="active">Actual host focus state.</param>
    public void Synchronize(bool active)
    {
        _frame.VerifyAccess();
        var chrome = _commands as IWindowChromeHost;
        var supported = chrome?.ChromeCapabilities.CustomFrame == true;
        var resizable = supported && chrome!.Chrome?.Resizable == true && chrome.ChromeCapabilities.NativeResize;
        var maximized = _commands.State == UiWindowState.Maximized;
        var previous = _frame.DefaultTitleBar!.State;
        if (previous.Active != active || previous.Maximized != maximized || previous.CanMinimize != supported || previous.CanMaximize != resizable || previous.CanResize != resizable)
            _frame.ApplyWindowState(new(active, maximized, supported, resizable, resizable));
    }
}
