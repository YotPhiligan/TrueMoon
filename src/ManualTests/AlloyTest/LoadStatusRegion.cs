using System.Runtime.ExceptionServices;
using TrueMoon.Argentis;

namespace AlloyTest;

// Sample-specific composition, not a new general binding API.
internal sealed class LoadStatusRegion : ContentControl
{
    internal static readonly UiProperty<LoadStatus> StatusProperty = new("LoadStatus", LoadStatus.Ready,
        Invalidation.Render, status => Enum.IsDefined(status.Phase) && status.Message != null);
    private readonly Func<LoadStatus, Element> _factory;
    private bool _pending, _applying;
    internal LoadStatus? DisplayedStatus { get; private set; }
    internal LoadStatus Status { get => Get(StatusProperty); set => Set(StatusProperty, value); }

    internal LoadStatusRegion(Func<LoadStatus, Element> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        AttachmentChanged += () =>
        {
            _pending = false;
            if (IsAttached) RefreshContent();
        };
        ApplyContent();
    }

    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (ReferenceEquals(property, StatusProperty)) RefreshContent();
    }

    internal void RefreshContent()
    {
        VerifyAccess();
        if (_pending) return;
        _pending = true;
        try { Dispatch(() => { _pending = false; ApplyContent(); }); }
        catch { _pending = false; throw; }
    }

    private void ApplyContent()
    {
        VerifyTreeAccess();
        var status = Status;
        if (Child != null && DisplayedStatus == status) return;
        if (_applying) throw new InvalidOperationException("Conditional content preparation cannot be reentrant.");
        _applying = true;
        var previous = Child;
        Element? candidate = null;
        var errors = new List<Exception>();
        try
        {
            try
            {
                candidate = _factory(status) ?? throw new InvalidOperationException("A status factory must return an element.");
                // SetContent validates the entire candidate before changing the old subtree.
                SetContent(candidate);
            }
            catch (Exception error) { errors.Add(error); }

            if (candidate != null && ReferenceEquals(Child, candidate))
            {
                // Notification failure can occur after commit. The accepted branch must remain owned by this region.
                DisplayedStatus = status;
                if (previous != null && !ReferenceEquals(previous, candidate)) Cleanup(previous, errors);
            }
            else if (candidate != null && candidate.Parent == null && !candidate.IsAttached && !candidate.IsDisposed && !IsAncestor(candidate))
            {
                // A rejected unowned draft is ours to clean; foreign nodes and ancestors are never disposed here.
                Cleanup(candidate, errors);
            }
        }
        finally { _applying = false; }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Conditional content preparation, notifications or cleanup failed.", errors);
    }

    private bool IsAncestor(Element candidate)
    {
        for (Element? node = this; node != null; node = node.Parent)
            if (ReferenceEquals(node, candidate)) return true;
        return false;
    }

    private static void Cleanup(Element node, List<Exception> errors)
    {
        try { node.Dispose(); }
        catch (Exception error) { errors.Add(error); }
    }
}
