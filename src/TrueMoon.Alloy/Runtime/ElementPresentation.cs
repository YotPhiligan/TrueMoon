using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Alloy.Runtime;

/// <summary>A drawing registration, without an element instance or a live presenter.</summary>
internal abstract class ElementPresentation
{
    private protected ElementPresentation() { }

    /// <summary>Declares that this kind of node needs an exact registration supplied by the application.</summary>
    public static ElementPresentation Required { get; } = new RequiredPresentation();

    /// <summary>Declares a factory and the prepared inputs read immediately before drawing.</summary>
    public static ElementPresentation Create<TControl, TState>(Func<TControl, TState> capture,
        IFactory<IElementPresenter<TState>> factory) where TControl : Element
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(factory);
        return new TypedPresentation<TControl, TState>(capture, factory);
    }

    /// <summary>Declares a factory for a presenter that reads the control without retaining it.</summary>
    public static ElementPresentation Create<TControl>(IFactory<IElementPresenter<TControl>> factory) where TControl : Element =>
        Create(static (TControl element) => element, factory);

    /// <summary>Declares a stateless drawing operation. The renderer owns the resulting presenter.</summary>
    public static ElementPresentation Create<TControl>(Action<TControl, IDrawingContext> paint) where TControl : Element
    {
        ArgumentNullException.ThrowIfNull(paint);
        return Create<TControl>(new Factory<IElementPresenter<TControl>>(() => new CallbackPresenter<TControl>(paint)));
    }

    internal abstract BoundPresenter Bind(Type controlType);

    private sealed class RequiredPresentation : ElementPresentation
    {
        internal override BoundPresenter Bind(Type controlType) => throw new InvalidOperationException($"No presenter registered for {controlType.FullName}.");
    }

    private sealed class CallbackPresenter<T>(Action<T, IDrawingContext> paint) : IElementPresenter<T>
    {
        public void Draw(T state, IDrawingContext context) => paint(state, context);
    }

    private sealed class TypedPresentation<TControl, TState>(Func<TControl, TState> capture,
        IFactory<IElementPresenter<TState>> factory) : ElementPresentation where TControl : Element
    {
        internal override BoundPresenter Bind(Type controlType) => new Bound(capture,
            factory.Create() ?? throw new InvalidOperationException($"Presenter factory returned null for {controlType.FullName}."));

        private sealed class Bound(Func<TControl, TState> capture, IElementPresenter<TState> presenter) : BoundPresenter
        {
            internal override void Before(Element node, IDrawingContext context) => presenter.Draw(capture((TControl)node), context);
            internal override void After(Element node, IDrawingContext context)
            {
                if (presenter is IAfterChildrenPresenter<TState> overlay) overlay.DrawAfterChildren(capture((TControl)node), context);
            }
            internal override IDisposable? Resource => presenter as IDisposable;
        }
    }
}

internal abstract class BoundPresenter
{
    internal abstract void Before(Element node, IDrawingContext context);
    internal abstract void After(Element node, IDrawingContext context);
    internal abstract IDisposable? Resource { get; }
}
