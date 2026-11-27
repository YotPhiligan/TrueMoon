
namespace TrueMoon.Argentis;

/// <summary>
/// One attachment-scoped window that folds structural work requested for an element into a single queued pass.
/// Requests arriving while the window is open only widen the accumulated request; the queued pass runs on the UI
/// dispatcher, after the tree change that requested it has finished, so it may rebuild subtrees.
/// </summary>
/// <remarks>
/// The accumulated request is cleared before the callback runs, so a notification raised inside the callback opens the
/// next window instead of being swallowed. A request equal to <c>none</c> never opens a window.
/// Any attach or detach of the owner ends the window without running it and releases input blocking: superseded work
/// must not create nodes for a context that is already gone. A reattached owner re-requests its current state.
/// While the owner is detached the synchronization runs inline, the way <see cref="Element.Dispatch"/> does, which is
/// what lets a view present its first context during content preparation.
/// </remarks>
/// <typeparam name="TRequest">The request value; typically a flags enum. <c>none</c> means "nothing requested".</typeparam>
public sealed class DeferredTreeSync<TRequest>
{
    private readonly Element _owner;
    private readonly TRequest _none;
    private readonly Func<TRequest, TRequest, TRequest> _combine;
    private readonly Action<TRequest> _synchronize;
    private readonly IEqualityComparer<TRequest> _equals = EqualityComparer<TRequest>.Default;
    private Element[] _blocked = [];
    private TRequest _pending;
    private bool _open, _armed, _blocking;

    /// <summary>Creates the queue for one owner element.</summary>
    /// <param name="owner">The element whose attachment scopes the queued work.</param>
    /// <param name="none">The request value that means "nothing": such a request is ignored.</param>
    /// <param name="combine">Folds two requests into the work one pass must cover.</param>
    /// <param name="synchronize">Performs the requested work; runs with the tree unlocked.</param>
    public DeferredTreeSync(Element owner, TRequest none, Func<TRequest, TRequest, TRequest> combine, Action<TRequest> synchronize)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(combine);
        ArgumentNullException.ThrowIfNull(synchronize);
        _owner = owner;
        _none = none;
        _pending = none;
        _combine = combine;
        _synchronize = synchronize;
        owner.AttachmentChanged += EndWindow;
    }

    /// <summary>Whether a pass is already queued or running for the current attachment.</summary>
    public bool IsPending => _open;

    /// <summary>Declares the subtrees <see cref="BlockInput"/> excludes from input routing; the last call wins.</summary>
    /// <param name="subtrees">Containers to block. Disposed ones are skipped when blocking and when releasing.</param>
    public DeferredTreeSync<TRequest> BlocksInput(params Element[] subtrees)
    {
        _owner.VerifyAccess();
        _blocked = [.. subtrees];
        return this;
    }

    /// <summary>Blocks input on the declared subtrees until the window this call belongs to has run.</summary>
    /// <remarks>Calling this while a pass is running arms the blocking for the window the callback opens.</remarks>
    public void BlockInput()
    {
        if (_owner.IsDisposed) return;
        _owner.VerifyAccess();
        if (_blocked.Length == 0) return;
        _armed = true;
        if (_blocking) return;
        ApplyBlocking(true);
        _blocking = true;
    }

    /// <summary>Requests one coalesced pass covering <paramref name="request"/>; opens the window when none is open.</summary>
    /// <param name="request">The work requested. A value equal to <c>none</c> is ignored.</param>
    /// <remarks>A rejected dispatch (queue capacity) rolls the window back and propagates the error, so the next
    /// request can open it again.</remarks>
    public void Request(TRequest request)
    {
        if (_owner.IsDisposed || _equals.Equals(request, _none)) return;
        _owner.VerifyAccess();
        if (_open)
        {
            _pending = _combine(_pending, request);
            return;
        }

        _pending = request;
        _open = true;
        try { _owner.Dispatch(Run); }
        catch { _open = false; _pending = _none; throw; }
    }

    private void Run()
    {
        var request = _pending;
        _pending = _none;
        _open = false;
        // Clear the arm before the callback: blocking stays applied only if the callback opened a new window that armed it.
        var held = _blocking;
        _armed = false;
        try { _synchronize(request); }
        finally { if (held && !_armed) ReleaseBlocking(); }
    }

    private void EndWindow()
    {
        _open = false;
        _pending = _none;
        _armed = false;
        if (_blocking) ReleaseBlocking();
    }

    private void ApplyBlocking(bool blocked)
    {
        foreach (var subtree in _blocked)
            if (subtree is not null && !subtree.IsDisposed) subtree.IsInputBlocked = blocked;
    }

    private void ReleaseBlocking()
    {
        ApplyBlocking(false);
        _blocking = false;
    }
}
