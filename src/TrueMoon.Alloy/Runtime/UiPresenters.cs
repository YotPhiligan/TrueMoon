using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Owns content presenters for a UI session or standalone renderer.</summary>
public sealed class UiPresenters : IDisposable
{
    private readonly Dictionary<Type, ElementPresentation> _entries = [];
    private readonly Dictionary<ElementPresentation, BoundPresenter> _bound = [];
    private readonly Dictionary<Type, BoundPresenter?> _resolved = [];
    private readonly List<IDisposable> _owned = [];
    private bool _sealed, _disposed;

    private UiPresenters() { }

    /// <summary>Creates a reusable presenter registry for drawing on the UI owner thread.</summary>
    /// <param name="configure">Optional callback to register exact-type overrides before the registry is sealed.</param>
    /// <returns>A configured registry that the caller must dispose.</returns>
    public static UiPresenters Create(Action<UiPresenters>? configure = null)
    {
        var presenters = new UiPresenters();
        try
        {
            configure?.Invoke(presenters);
            ObjectDisposedException.ThrowIf(presenters._disposed, presenters);
            presenters._sealed = true;
            return presenters;
        }
        catch (Exception error) { UiCleanup.Complete(error, presenters.Dispose); throw; }
    }

    /// <summary>Replaces the glyph presenter for exactly <see cref="IconButton"/>.</summary>
    /// <param name="factory">Borrowed factory returning a registry-owned presenter.</param>
    public void RegisterIconButton(IFactory<IElementPresenter<IconButtonPaintState>> factory) =>
        Register<IconButton, IconButtonPaintState>(IconButtonPaintState.From, factory);

    /// <summary>Declares that this exact type must have an application registration, even if a base type has stock painting.</summary>
    public void Require<TControl>() where TControl : Element
    {
        VerifyConfigurable();
        _entries.TryAdd(typeof(TControl), ElementPresentation.Required);
    }

    /// <summary>Registers a stateless drawing operation for this exact type.</summary>
    public void Register<TControl>(Action<TControl, IDrawingContext> paint) where TControl : Element =>
        Add(typeof(TControl), ElementPresentation.Create(paint));

    /// <summary>Registers a content presenter, created on the first draw of the exact control type.</summary>
    /// <param name="capture">Reads the current visual inputs without modifying the control.</param>
    /// <param name="factory">Creates a new presenter owned exclusively by this registration.</param>
    public void Register<TControl, TState>(Func<TControl, TState> capture, IFactory<IElementPresenter<TState>> factory) where TControl : Element =>
        Add(typeof(TControl), ElementPresentation.Create(capture, factory));

    private void VerifyConfigurable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sealed) throw new InvalidOperationException("Register presenters in the configuration callback.");
    }

    private void Add(Type type, ElementPresentation descriptor)
    {
        VerifyConfigurable();
        if (_entries.TryGetValue(type, out var existing) && existing != ElementPresentation.Required)
            throw new InvalidOperationException($"A presenter for {type} is already registered.");
        _entries[type] = descriptor;
    }

    internal BoundPresenter? Resolve(Element element)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var type = element.GetType();
        if (_resolved.TryGetValue(type, out var presenter)) return presenter;
        var descriptor = _entries.TryGetValue(type, out var exact) ? exact : StockPresenters.Find(type);
        if (descriptor != null && !_bound.TryGetValue(descriptor, out presenter))
        {
            presenter = descriptor.Bind(type);
            _bound.Add(descriptor, presenter);
            if (presenter.Resource is { } resource) _owned.Add(resource);
        }
        _resolved.Add(type, presenter);
        return presenter;
    }

    /// <summary>Releases all presenters created by this registry.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entries.Clear();
        _bound.Clear();
        _resolved.Clear();
        var cleanup = _owned.Select(resource => (Action)resource.Dispose).Reverse().ToArray();
        _owned.Clear();
        UiCleanup.Complete(null, cleanup);
    }
}
