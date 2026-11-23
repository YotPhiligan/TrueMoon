namespace TrueMoon.Argentis;

internal sealed class ButtonCommandBinding : IDisposable
{
    private readonly Button _button;
    private readonly IUiCommand _command;
    private readonly Func<bool>? _isCurrent;
    private Action? _changed;
    private bool _disposed;

    internal ButtonCommandBinding(Button button, IUiCommand command, Func<bool>? isCurrent = null)
    {
        _button = button;
        _command = command;
        _isCurrent = isCurrent;
        button.Click += Execute;
        button.AttachmentChanged += Reconnect;
        try
        {
            if (button.IsAttached) Reconnect();
            else Refresh();
        }
        catch (Exception original)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }

    private bool IsCurrent => !_disposed && (_isCurrent?.Invoke() ?? true);

    private void Reconnect()
    {
        Disconnect();
        if (!_button.IsAttached || !IsCurrent) return;

        var dispatch = _button.CaptureAttachmentDispatcher();
        _changed = () => { if (IsCurrent) dispatch(Refresh); };
        try
        {
            _command.CanExecuteChanged += _changed;
            Refresh();
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    private void Refresh()
    {
        if (!IsCurrent) return;
        var enabled = _command.CanExecute;
        if (IsCurrent) _button.IsEnabled = enabled;
    }

    private void Execute()
    {
        if (IsCurrent && _command.CanExecute && IsCurrent) _command.Execute();
    }

    private void Disconnect()
    {
        if (_changed == null) return;
        var changed = _changed;
        _changed = null;
        _command.CanExecuteChanged -= changed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _button.AttachmentChanged -= Reconnect;
        _button.Click -= Execute;
        Disconnect();
    }
}
