namespace TrueMoon.Argentis.Views;

// Owns products of constructor-injected view factories until a tree or session takes them.
internal sealed class ViewDependencyOwnership(int capacity) : IDisposable
{
    private View?[]? _children;
    private int _count;
    private bool _constructionComplete;

    internal TView Track<TView>(TView child) where TView : View
    {
        child.VerifyAccess();
        if (child is not { IsDisposed: false, IsAttached: false, Parent: null, DependencyOwnership: null })
            throw new InvalidOperationException($"The factory for '{typeof(TView)}' must return a fresh, unowned view.");

        (_children ??= new View?[capacity])[_count++] = child;
        child.DependencyOwnership = this;
        return child;
    }

    internal void Release(View child)
    {
        var children = _children!;
        var index = 0;
        while (!ReferenceEquals(children[index], child)) index++;
        var remaining = --_count - index;
        if (remaining > 0) Array.Copy(children, index + 1, children, index, remaining);
        children[_count] = null;
        child.DependencyOwnership = null;
        if (_count == 0 && _constructionComplete) _children = null;
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
