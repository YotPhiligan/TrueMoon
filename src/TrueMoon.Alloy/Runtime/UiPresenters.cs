using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Owns content presenters for a UI session or standalone renderer.</summary>
public sealed class UiPresenters : IDisposable
{
    private readonly Dictionary<Type, ElementPresentation> _entries = [];
    private readonly Dictionary<ElementPresentation, BoundPresenter> _bound = [];
    private readonly List<IDisposable> _owned = [];
    private bool _sealed, _disposed;
    internal static IFactory<IElementPresenter<IconButtonPaintState>> StockIconFactory => IconButton.StockFactory;

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

    /// <summary>Registers a content presenter, created on the first draw of the exact control type.</summary>
    /// <param name="capture">Reads the current visual inputs without modifying the control.</param>
    /// <param name="factory">Creates a new presenter owned exclusively by this registration.</param>
    public void Register<TControl, TState>(Func<TControl, TState> capture, IFactory<IElementPresenter<TState>> factory) where TControl : Element
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sealed) throw new InvalidOperationException("Register presenters in the configuration callback.");
        var descriptor = ElementPresentation.Create(capture, factory);
        if (!_entries.TryAdd(typeof(TControl), descriptor))
            throw new InvalidOperationException($"A presenter for {typeof(TControl)} is already registered.");
    }

    internal BoundPresenter? Resolve(Element element)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var type = element.GetType();
        var descriptor = _entries.TryGetValue(type, out var exact) ? exact : element.Presentation;
        if (descriptor == null) return null;
        if (_bound.TryGetValue(descriptor, out var presenter)) return presenter;
        presenter = descriptor.Bind(type);
        _bound.Add(descriptor, presenter);
        if (presenter.Resource is { } resource) _owned.Add(resource);
        return presenter;
    }

    /// <summary>Releases all presenters created by this registry.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entries.Clear();
        _bound.Clear();
        var cleanup = _owned.Select(resource => (Action)resource.Dispose).Reverse().ToArray();
        _owned.Clear();
        UiCleanup.Complete(null, cleanup);
    }
}
