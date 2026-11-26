namespace TrueMoon.Argentis.Views;

// Owns constructor-injected views or explicitly retained drafts until transferred.
internal sealed class ViewDependencyOwnership(int capacity, View? draftOwner = null) : IDisposable
{
    private Element?[]? _children;
    private int _count;
    private bool _constructionComplete;
    internal View? DraftOwner => draftOwner;

    internal TView Track<TView>(TView child) where TView : View
    {
        child.VerifyAccess();
        if (child is not { IsDisposed: false, IsAttached: false, Parent: null, DependencyOwnership: null })
            throw new InvalidOperationException($"The factory for '{typeof(TView)}' must return a fresh, unowned view.");

        Add(child);
        return child;
    }

    internal void Add(Element child)
    {
        _children ??= new Element?[capacity];
        if (_count == _children.Length) Array.Resize(ref _children, Math.Max(1, _count * 2));
        _children[_count++] = child;
        child.DependencyOwnership = this;
    }

    internal void Release(Element child)
    {
        var children = _children!;
        var index = 0;
        while (!ReferenceEquals(children[index], child)) index++;
        var remaining = --_count - index;
        if (remaining > 0) Array.Copy(children, index + 1, children, index, remaining);
        children[_count] = null;
        child.DependencyOwnership = null;
        if (_count == 0 && (_constructionComplete || draftOwner != null)) _children = null;
    }

    internal void TransferTo(View owner)
    {
        _constructionComplete = true;
        if (_count == 0) _children = null;
        else owner.Own(this);
    }

    public void Dispose()
    {
        List<Exception>? errors = null;
        while (_count != 0)
        {
            var child = _children![--_count]!;
            _children[_count] = null;
            child.DependencyOwnership = null;
            try { child.Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        _children = null;
        TreeChange.ThrowErrors(errors);
    }
}
