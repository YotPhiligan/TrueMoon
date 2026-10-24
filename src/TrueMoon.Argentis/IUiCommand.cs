namespace TrueMoon.Argentis;

/// <summary>Shared synchronous command admission for buttons, menus and keyboard adapters. The application owns asynchronous work.</summary>
public interface IUiCommand
{
    /// <summary>Gets current availability; implementations recheck it when executing.</summary>
    bool CanExecute { get; }
    /// <summary>Reports availability changes on the owning UI thread.</summary>
    event Action? CanExecuteChanged;
    /// <summary>Executes the same action used by all presentations, if available.</summary>
    void Execute();
}
