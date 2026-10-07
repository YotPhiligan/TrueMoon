using System.Runtime.ExceptionServices;

namespace TrueMoon.Argentis;

// Keeps structural changes coherent even when user notifications or cleanup throw.
internal sealed class TreeChange : IDisposable
{
    private readonly Element[] _locked;
    private readonly List<Exception> _errors = [];
    private readonly Queue<Action> _notifications = new();

    internal TreeChange(params Element?[] elements)
    {
        var locked = new HashSet<Element>();
        foreach (var element in elements)
        {
            if (element == null) continue;
            element.VerifyTreeAccess();
            locked.Add(element);
            var root = element;
            while (root.Parent != null) root = root.Parent;
            locked.Add(root);
        }
        _locked = locked.ToArray();
        foreach (var element in _locked) element.ChangingTree = true;
    }

    internal void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _errors.Add(error); }
    }

    internal void Notify(Action? handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList()) Schedule(handler);
    }

    internal void Schedule(Action action) => _notifications.Enqueue(action);

    internal void Flush()
    {
        while (_notifications.TryDequeue(out var action)) Run(action);
    }

    internal void Complete()
    {
        Flush();
        ThrowErrors(_errors);
    }

    internal static void ThrowErrors(IReadOnlyCollection<Exception>? errors)
    {
        if (errors == null || errors.Count == 0) return;
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors.First()).Throw();
        throw new AggregateException("UI tree notifications or cleanup failed after the change committed.", errors);
    }

    public void Dispose()
    {
        foreach (var element in _locked) element.ChangingTree = false;
    }
}
