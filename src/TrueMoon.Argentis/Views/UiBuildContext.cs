using TrueMoon.Argentis.Bindings;

namespace TrueMoon.Argentis.Views;

internal sealed class UiBuildContext<TData>(View view, IUiControlFactories factories, IDataContext<TData>? context) : IUiBuildContext<TData>
{
    private readonly HashSet<Element> _drafts = new(ReferenceEqualityComparer.Instance);
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _closed;

    public IDataContext<TData>? DataContext => context;

    public UiControl<TControl, TData> Create<TControl>() where TControl : Element
    {
        VerifyOpen();
        return Track(factories.Create<TControl>());
    }

    public UiControl<TControl, TData> Create<TControl, TArguments>(TArguments arguments) where TControl : Element
    {
        VerifyOpen();
        return Track(factories.Create<TControl, TArguments>(arguments));
    }

    public UiControl<TControl, TData> Create<TControl>(IFactory<TControl> factory) where TControl : Element
    {
        VerifyOpen();
        ArgumentNullException.ThrowIfNull(factory);
        return Track(factory.Create());
    }

    public UiControl<TControl, TData> Create<TControl, TArguments>(IFactory<TControl, TArguments> factory, TArguments arguments)
        where TControl : Element
    {
        VerifyOpen();
        ArgumentNullException.ThrowIfNull(factory);
        return Track(factory.Create(arguments));
    }

    public UiControl<TControl, TData> Adopt<TControl>(TControl control) where TControl : Element
    {
        VerifyOpen();
        ArgumentNullException.ThrowIfNull(control);
        control.VerifyAccess();
        if (control is not { Parent: null, IsAttached: false })
            throw new InvalidOperationException("Only a live, unowned control can be adopted by the build context.");
        var ownership = control.DependencyOwnership;
        if (ownership?.DraftOwner is { } owner && !ReferenceEquals(owner, view))
            throw new InvalidOperationException("The draft belongs to another view.");
        if (!_drafts.Add(control))
            throw new InvalidOperationException("A control can be adopted by the build context only once.");
        ownership?.Release(control);
        return new(control, context);
    }

    private void VerifyOpen()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("UI controls must be created on the UI owner thread.");
        if (_closed) throw new InvalidOperationException("The UI content factory has already finished.");
    }

    private UiControl<TControl, TData> Track<TControl>(TControl? control) where TControl : Element
    {
        if (control is not { Parent: null, IsAttached: false, IsDisposed: false, DependencyOwnership: null })
            throw new InvalidOperationException($"The UI factory for '{typeof(TControl)}' must return a live, unowned control.");
        if (!_drafts.Add(control))
            throw new InvalidOperationException($"The UI factory for '{typeof(TControl)}' returned the same control twice.");
        return new(control, context);
    }

    internal Element Build(Func<IUiBuildContext<TData>, Element> factory)
    {
        try
        {
            var root = factory(this);
            if (root == null || !_drafts.Contains(root) || root.Parent != null || root.IsAttached || root.IsDisposed)
                throw new InvalidOperationException("Deferred content must return a live, unowned root created or adopted by its build context.");

            foreach (var draft in _drafts)
            {
                var owner = draft;
                while (owner.Parent is { } parent) owner = parent;
                if (owner != root)
                    throw new InvalidOperationException("Controls created or adopted by the build context must belong to the returned tree.");
            }
            return root;
        }
        catch (Exception original)
        {
            _closed = true;
            List<Exception>? errors = null;
            foreach (var draft in _drafts)
            {
                if (draft.Parent != null || draft.IsAttached || draft.IsDisposed) continue;
                try { draft.Dispose(); }
                catch (Exception cleanup) { (errors ??= [original]).Add(cleanup); }
            }
            TreeChange.ThrowErrors(errors);
            throw;
        }
        finally
        {
            _closed = true;
            _drafts.Clear();
        }
    }
}
