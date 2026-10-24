namespace TrueMoon.Argentis;

/// <summary>A UI command declaration over borrowed application actions; asynchronous work remains tracked by those actions.</summary>
public sealed class DelegateUiCommand : IUiCommand
{
    private readonly Action _execute;
    private readonly Func<bool> _available;
    /// <summary>Creates a command declaration.</summary>
    /// <param name="execute">Shared action.</param><param name="available">Live availability, or always available.</param>
    public DelegateUiCommand(Action execute, Func<bool>? available = null)
    { ArgumentNullException.ThrowIfNull(execute); _execute = execute; _available = available ?? (() => true); }
    /// <inheritdoc />
    public bool CanExecute => _available();
    /// <inheritdoc />
    public event Action? CanExecuteChanged;
    /// <inheritdoc />
    public void Execute() { if (CanExecute) _execute(); }
    /// <summary>Notifies bound presentations after the borrowed application's state changes.</summary>
    public void Refresh() => CanExecuteChanged?.Invoke();
}
