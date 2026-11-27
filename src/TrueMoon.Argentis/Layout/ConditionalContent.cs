using System.Runtime.ExceptionServices;

namespace TrueMoon.Argentis.Layout;

/// <summary>A content slot whose single subtree follows one typed state; the factory builds the branch or returns null to leave the slot empty.</summary>
/// <typeparam name="TState">The state type. A nullable state describes a slot that can be empty.</typeparam>
/// <remarks>
/// The branch is prepared before the old one is released, so a validation or factory failure keeps the tree intact.
/// A state equal to the installed one keeps the existing branch: focus, drafts and typed input survive a no-change sync.
/// Call <see cref="Refresh"/> to re-read the source while the state itself stays equal.
/// Content belongs to the slot: the replaced branch is disposed here, so callers must not call <see cref="ContentControl.SetContent"/> directly.
/// </remarks>
public class ConditionalContent<TState> : ContentControl
{
    /// <summary>The state property driving this slot; an ordinary property binding can target it.</summary>
    public static readonly UiProperty<TState> StateProperty = new("ConditionalState", default!);

    private readonly Func<TState, Element?> _factory;
    private TState _appliedState = default!;
    private bool _hasApplied, _pending, _applying;

    /// <summary>Creates the slot, installs the branch of the initial state and rebuilds it on every later state change.</summary>
    /// <param name="state">The state whose branch is installed first.</param>
    /// <param name="factory">Builds the branch for a state; returning null leaves the slot empty.</param>
    public ConditionalContent(TState state, Func<TState, Element?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        AttachmentChanged += () =>
        {
            // A refresh queued by the previous attachment must not apply an old state after reattach.
            _pending = false;
            if (IsAttached) Refresh();
        };
        Set(StateProperty, state);
        // Set stays silent for a value equal to the property default, so the initial branch is applied here too.
        Apply();
    }

    /// <summary>Gets or sets the requested state of the branch.</summary>
    public TState State { get => Get(StateProperty); set => Set(StateProperty, value); }
    /// <summary>Gets the state whose branch is currently installed.</summary>
    public TState AppliedState => _appliedState;
    /// <summary>Gets whether a branch is installed; false after the factory cleared the content.</summary>
    public bool HasBranch => Child != null;

    /// <summary>Requests one coalesced re-evaluation of the branch on the UI dispatcher.</summary>
    public void Refresh()
    {
        VerifyAccess();
        if (_pending) return;
        _pending = true;
        try { Dispatch(() => { _pending = false; Apply(); }); }
        catch { _pending = false; throw; }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (ReferenceEquals(property, StateProperty)) Refresh();
    }

    private void Apply()
    {
        VerifyTreeAccess();
        var state = State;
        // A compatible state keeps the branch: rows, focus and typed input survive a no-change sync.
        if (_hasApplied && EqualityComparer<TState>.Default.Equals(_appliedState, state)) return;
        if (_applying) throw new InvalidOperationException("Conditional content preparation cannot be reentrant.");
        _applying = true;
        var previous = Child;
        Element? candidate = null;
        List<Exception>? errors = null;
        try
        {
            try
            {
                candidate = _factory(state);
                // SetContent validates the entire candidate before changing the old subtree.
                SetContent(candidate);
            }
            catch (Exception error) { (errors ??= []).Add(error); }

            var accepted = candidate == null ? Child == null : ReferenceEquals(Child, candidate);
            if (accepted)
            {
                // Notification failure can occur after commit. The accepted branch must remain owned by this slot.
                _appliedState = state;
                _hasApplied = true;
                if (previous != null && !ReferenceEquals(previous, candidate)) Cleanup(previous, ref errors);
            }
            else if (candidate != null && candidate.Parent == null && !candidate.IsAttached && !candidate.IsDisposed && !IsAncestor(candidate))
            {
                // A rejected unowned draft is ours to clean; foreign nodes and ancestors are never disposed here.
                Cleanup(candidate, ref errors);
            }
        }
        finally { _applying = false; }
        if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is { Count: > 1 }) throw new AggregateException("Conditional content preparation, notifications or cleanup failed.", errors);
    }

    private bool IsAncestor(Element candidate)
    {
        for (Element? node = this; node != null; node = node.Parent)
            if (ReferenceEquals(node, candidate)) return true;
        return false;
    }

    private static void Cleanup(Element node, ref List<Exception>? errors)
    {
        try { node.Dispose(); }
        catch (Exception error) { (errors ??= []).Add(error); }
    }
}
