using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq.Expressions;

namespace TrueMoon.Argentis;

/// <summary>Fluent composition and configuration helpers.</summary>
public static partial class ElementExtensions
{
    /// <summary>Binds an empty container to the latest collection contents, preserving rows by object reference.</summary>
    /// <param name="element">The container whose children are exclusively managed by this binding.</param>
    /// <param name="source">Unique non-null reference items. Its owner must serialize changes with UI-thread enumeration.</param>
    /// <param name="factory">Creates a new unowned subtree. The binding disposes removed rows and failed preparation drafts.</param>
    /// <returns>The same concrete container.</returns>
    /// <remarks>Initial attach and notifications synchronize during Update. Reset retains surviving references; intermediate source states may be coalesced. Detach disconnects the source and retains rows.</remarks>
    public static T BindItems<T, TItem>(this T element, ObservableCollection<TItem> source, Func<TItem, Element> factory)
        where T : ElementList where TItem : class =>
        BindItemsCore(element, source, source, factory, static item => item, ReferenceKeyComparer<TItem>.Instance, null);

    /// <summary>Binds an empty container to a read-only observable collection using reference identity.</summary>
    /// <param name="element">The container whose children are exclusively managed by this binding.</param>
    /// <param name="source">Unique non-null reference items, safely readable on the UI owner thread.</param>
    /// <param name="factory">Creates a new unowned subtree; generated rows are disposed when removed.</param>
    /// <returns>The same concrete container.</returns>
    public static T BindItems<T, TItem>(this T element, ReadOnlyObservableCollection<TItem> source, Func<TItem, Element> factory)
        where T : ElementList where TItem : class =>
        BindItemsCore(element, source, source, factory, static item => item, ReferenceKeyComparer<TItem>.Instance, null);

    /// <summary>Binds children to a stable key of every item, so a row survives its item instance being replaced by an equal key.</summary>
    /// <param name="element">A container whose bound children this binding manages. Several bindings can share one container.</param>
    /// <param name="items">Items with unique non-null keys. A list that does not notify changes synchronizes only through <see cref="RefreshItems{T}"/>.</param>
    /// <param name="factory">Creates a new unowned subtree for a key without a row; the binding disposes rows of removed keys.</param>
    /// <param name="keySelector">Stable item identity. Equal keys reuse the row, its focus, its input and its subscriptions.</param>
    /// <param name="rebind">Points an existing row at a different item instance with the same key; null leaves the row subtree untouched.</param>
    /// <returns>The same concrete container.</returns>
    /// <remarks>
    /// Every synchronization reads the current list, so intermediate source states never reach the factory. Rows of
    /// surviving keys are neither recreated nor detached, whichever position they take. Observing sources also notify.
    /// </remarks>
    public static T BindItems<T, TItem, TKey>(this T element, IReadOnlyList<TItem> items, Func<TItem, Element> factory,
        Func<TItem, TKey> keySelector, Action<Element, TItem>? rebind = null)
        where T : ElementList where TItem : class where TKey : notnull =>
        BindItemsCore(element, items, items as INotifyCollectionChanged, factory, keySelector, EqualityComparer<TKey>.Default, rebind);

    private static T BindItemsCore<T, TItem, TKey>(
        T element,
        IReadOnlyList<TItem> items,
        INotifyCollectionChanged? source,
        Func<TItem, Element> factory,
        Func<TItem, TKey> keySelector,
        IEqualityComparer<TKey> comparer,
        Action<Element, TItem>? rebind)
        where T : ElementList where TItem : class where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(keySelector);
        var binding = new CollectionBinding<TItem, TKey>(element, items, source, factory, keySelector, comparer, rebind);
        try { element.Own(binding); }
        catch { binding.Dispose(); throw; }
        return element;
    }

    /// <summary>Binds a scrolling container to a window of rows: only rows the viewport can show exist as children.</summary>
    /// <param name="element">A <see cref="VirtualizingStack"/> inside a scroll viewport; its children are managed by this binding.</param>
    /// <param name="items">Items with unique non-null keys. A list that does not notify changes synchronizes only through <see cref="RefreshItems{T}(T)"/>.</param>
    /// <param name="factory">Creates an unowned subtree for a row that enters the window without a reusable one.</param>
    /// <param name="keySelector">Stable item identity; a row is reused while its key stays inside the window.</param>
    /// <param name="rebind">Points a recycled row at a different item. Without it rows are created and disposed per scroll step, because their closures keep the old item.</param>
    /// <returns>The same container.</returns>
    /// <remarks>
    /// The container measures and arranges the window plus <see cref="VirtualizingStack.Buffer"/> rows and reports the
    /// full extent from measured and estimated row heights. A source notification only marks the window stale, so its
    /// cost no longer grows with the list.
    /// </remarks>
    public static VirtualizingStack BindVirtualizedItems<TItem, TKey>(
        this VirtualizingStack element,
        IReadOnlyList<TItem> items,
        Func<TItem, Element> factory,
        Func<TItem, TKey> keySelector,
        Action<Element, TItem>? rebind = null)
        where TItem : class where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(keySelector);
        var binding = new VirtualizingCollectionBinding<TItem, TKey>(element, items, items as INotifyCollectionChanged,
            factory, keySelector, null, rebind);
        try { element.Own(binding); }
        catch { binding.Dispose(); throw; }
        return element;
    }

    /// <summary>Requests synchronization of a registered collection binding at the next Update.</summary>
    /// <param name="element">The bound container. Detached containers refresh on their next attachment.</param>
    /// <returns>The same concrete container.</returns>
    public static T RefreshItems<T>(this T element) where T : ElementList
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Items.RefreshBindings();
        return element;
    }

    /// <summary>Sets a fixed width.</summary>
    public static T Width<T>(this T element, float width) where T : Element { element.Width = width; return element; }
    /// <summary>Sets a fixed height.</summary>
    public static T Height<T>(this T element, float height) where T : Element { element.Height = height; return element; }
    /// <summary>Sets a reusable background material; null removes the effect. Background should be transparent to expose it.</summary>
    public static T WithBackdrop<T>(this T element, BackdropMaterial? material) where T : Element
    { element.Backdrop = material; return element; }
    /// <summary>Sets a typed property.</summary>
    public static T SetValue<T, TValue>(this T element, UiProperty<TValue> property, TValue value) where T : Element { element.Set(property, value); return element; }
    /// <summary>Sets the stable semantic target ID. IDs must be unique within a session.</summary>
    public static T SemanticId<T>(this T element, string id) where T : Element { element.SemanticId = id; return element; }
    /// <summary>Names an unowned, unattached element in the nearest view's part scope.</summary>
    /// <remarks>Names use ordinal comparison. A nested View begins its own scope, including its root name.</remarks>
    public static T Named<T>(this T element, string name) where T : Element
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        element.SetPartName(name);
        return element;
    }
    /// <summary>Gives an element an optional generated part name; runtime name scoping is unchanged.</summary>
    public static T NamedOptional<T>(this T element, string name) where T : Element => element.Named(name);
    /// <summary>Binds a typed UI property to one directly selected source property until element disposal.</summary>
    /// <param name="element">The element, preserving its concrete type in the Fluent chain.</param>
    /// <param name="property">The destination UI property; only one binding per property is allowed.</param>
    /// <param name="source">The notifying source. Its getter must be safe to read on the UI owner thread.</param>
    /// <param name="selector">A direct instance property access, for example x =&gt; x.Name. Nested paths and computed expressions are unsupported.</param>
    /// <returns>The configured element.</returns>
    /// <remarks>One-way only. Initial synchronization runs on attach, including reattach. Source events queue reads through Dispatch for the next host update. Detach removes the subscription; local UI edits do not update the source.</remarks>
    public static T Bind<T, TSource, TValue>(this T element, UiProperty<TValue> property, TSource source, Expression<Func<TSource, TValue>> selector)
        where T : Element where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        element.OwnBinding(property, () => new PropertyBinding<TSource, TValue>(element, property, source, selector));
        return element;
    }
    /// <summary>Binds a typed UI property to a writable source property in both directions until disposal.</summary>
    /// <param name="element">The element, preserving its concrete type.</param>
    /// <param name="property">The UI property; one binding of either direction is allowed per property.</param>
    /// <param name="source">The notifying source, whose getter and setter run on the UI owner thread.</param>
    /// <param name="selector">A direct public readable/writable property of exactly TValue; conversions and init-only setters are unsupported.</param>
    /// <returns>The configured element.</returns>
    /// <remarks>Initial attach reads the source. Attached UI changes write immediately and read back its canonical value. On failure, readback restoration is attempted before the error propagates; partial source changes are not rolled back. Detach disconnects both directions.</remarks>
    public static T BindTwoWay<T, TSource, TValue>(this T element, UiProperty<TValue> property, TSource source, Expression<Func<TSource, TValue>> selector)
        where T : Element where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        element.OwnBinding(property, () => new PropertyBinding<TSource, TValue>(element, property, source, selector, twoWay: true));
        return element;
    }

    /// <summary>Binds a UI property to the currently selected property of a notifying data context.</summary>
    /// <remarks>While attached, a context replacement reads the new source immediately. A null context suspends the binding and retains the current UI value.</remarks>
    public static T Bind<T, TData, TValue>(this T element, UiProperty<TValue> property,
        IDataContext<TData> context, Expression<Func<TData, TValue>> selector)
        where T : Element where TData : class, INotifyPropertyChanged =>
        BindContext(element, property, context, selector, twoWay: false);

    /// <summary>Binds a UI property in both directions to a writable property of the current data context.</summary>
    /// <remarks>Only the current source receives edits. Detach and context replacement disconnect the old source and fence its queued notifications.</remarks>
    public static T BindTwoWay<T, TData, TValue>(this T element, UiProperty<TValue> property,
        IDataContext<TData> context, Expression<Func<TData, TValue>> selector)
        where T : Element where TData : class, INotifyPropertyChanged =>
        BindContext(element, property, context, selector, twoWay: true);

    private static T BindContext<T, TData, TValue>(T element, UiProperty<TValue> property,
        IDataContext<TData> context, Expression<Func<TData, TValue>> selector, bool twoWay)
        where T : Element where TData : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selector);
        element.OwnBinding(property, () => new PropertyBinding<TData, TValue>(element, property, null, selector, twoWay, context));
        return element;
    }

    /// <summary>Binds plain text to a directly selected notifying source property.</summary>
    /// <param name="element">The text element.</param>
    /// <param name="source">The notifying source.</param>
    /// <param name="selector">A direct string property access, for example x =&gt; x.Name.</param>
    /// <returns>The same concrete text element.</returns>
    public static T BindText<T, TSource>(this T element, TSource source, Expression<Func<TSource, string>> selector)
        where T : Text where TSource : class, INotifyPropertyChanged => element.Bind(Text.ValueProperty, source, selector);
    /// <summary>Binds plain text to a directly selected property of the current data context.</summary>
    public static T BindText<T, TData>(this T element, IDataContext<TData> context, Expression<Func<TData, string>> selector)
        where T : Text where TData : class, INotifyPropertyChanged => element.Bind(Text.ValueProperty, context, selector);
    /// <summary>Applies arbitrary strongly typed configuration.</summary>
    public static T Configure<T>(this T element, Action<T> configure) where T : Element { configure(element); return element; }
    /// <summary>Applies internal spacing.</summary>
    public static T Padding<T>(this T element, float padding) where T : Element { element.Padding = new Thickness(padding); return element; }
    /// <summary>Applies a local font weight.</summary>
    /// <typeparam name="T">Concrete element type.</typeparam><param name="element">The element.</param>
    /// <param name="weight">Weight100..900;400 is regular and600 semibold.</param><returns>The same element.</returns>
    public static T FontWeight<T>(this T element, int weight) where T : Element { element.FontWeight = weight; return element; }
    /// <summary>Applies a local italic selection.</summary>
    /// <typeparam name="T">Concrete element type.</typeparam><param name="element">The element.</param>
    /// <param name="italic">Whether to select italic.</param><returns>The same element.</returns>
    public static T FontItalic<T>(this T element, bool italic = true) where T : Element { element.FontItalic = italic; return element; }
    /// <summary>Applies a background color.</summary>
    public static T Background<T>(this T element, Color color) where T : Element { element.Background = color; return element; }
    /// <summary>Adds a batch of child elements in paint order.</summary>
    /// <remarks>Validates every child before adopting any. Attachment notifications run after the entire batch is adopted.</remarks>
    public static T WithChildren<T>(this T element, params Element[] children) where T : ElementList
    {
        ArgumentNullException.ThrowIfNull(children);
        element.Items.AddRange(children);
        return element;
    }
    /// <summary>Lets the child share a stack's remaining space equally with its other visible fill children.</summary>
    /// <remarks>During measurement an unbounded axis uses natural sizes. The arranged slot observes the child's own size and alignment rules.</remarks>
    public static T Fill<T>(this T element, bool enabled = true) where T : Element =>
        element.SetValue(StackBase.FillProperty, enabled);
    /// <summary>Assigns replaceable content.</summary>
    public static T Content<T>(this T element, Element? content) where T : ContentControl { element.SetContent(content); return element; }
    /// <summary>Registers a button action.</summary>
    public static T OnClick<T>(this T element, Action action) where T : Button { element.Click += action; return element; }
    /// <summary>Observes a borrowed source's property notifications while the element is attached.</summary>
    /// <remarks>Notifications are delivered in queue order on the UI owner thread. Synchronization runs on each attachment after subscribing.</remarks>
    public static T ObservePropertyChanged<T>(this T element, INotifyPropertyChanged source,
        PropertyChangedEventHandler handler, Action? synchronize = null) where T : Element =>
        element.ObservePropertyChanged(source, handler, synchronize, UiSynchronization.Immediate);

    /// <summary>Observes a borrowed source's property notifications while the element is attached.</summary>
    /// <param name="element">The element to observe while attached.</param>
    /// <param name="source">The borrowed notifying source.</param>
    /// <param name="handler">Handles each notification on the UI owner thread in queue order.</param>
    /// <param name="synchronize">Reads the current state on each attachment, or null to skip synchronization.</param>
    /// <param name="timing">Whether to synchronize inline or on the attachment queue.</param>
    /// <returns>The configured element.</returns>
    public static T ObservePropertyChanged<T>(this T element, INotifyPropertyChanged source,
        PropertyChangedEventHandler handler, Action? synchronize, UiSynchronization timing) where T : Element
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        return OwnObserver(element, () => new AttachmentPropertyObserver(element, source, handler, synchronize, timing));
    }

    /// <summary>Observes property notifications from the current notifying data context while attached.</summary>
    /// <remarks>Notifications are queued on the UI owner thread. Synchronization runs at attach and on context replacement, including null.</remarks>
    public static T ObservePropertyChanged<T, TData>(this T element, IDataContext<TData> context,
        PropertyChangedEventHandler handler, Action? synchronize = null)
        where T : Element where TData : class, INotifyPropertyChanged =>
        element.ObservePropertyChanged(context, handler, synchronize, UiSynchronization.Immediate);

    /// <summary>Observes the current notifying data context while attached.</summary>
    /// <param name="element">The element to observe while attached.</param>
    /// <param name="context">The borrowed data context.</param>
    /// <param name="handler">Handles each notification on the UI owner thread in queue order.</param>
    /// <param name="synchronize">Reads the current state on attach and replacement, including null. Pass null to skip synchronization.</param>
    /// <param name="timing">Whether to synchronize inline or on the attachment queue.</param>
    /// <returns>The configured element.</returns>
    public static T ObservePropertyChanged<T, TData>(this T element, IDataContext<TData> context,
        PropertyChangedEventHandler handler, Action? synchronize, UiSynchronization timing)
        where T : Element where TData : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handler);
        return OwnObserver(element, () => new AttachmentDataContext<TData>(element, context,
            (source, current) => new AttachmentPropertyObserver(element, source, handler, null, isCurrent: current),
            synchronize == null ? null : _ => synchronize(), timing));
    }

    /// <summary>Synchronizes the current state of one or more borrowed sources while attached.</summary>
    /// <param name="element">The element, preserving its concrete type.</param>
    /// <param name="synchronize">Reads the current state on the UI owner thread.</param>
    /// <param name="sources">The notifying sources, captured at registration.</param>
    /// <returns>The configured element.</returns>
    /// <remarks>Synchronizes immediately on each attachment after subscribing. Any property notification
    /// requests a queued synchronization, with at most one pending across all sources for that attachment.</remarks>
    public static T ObserveState<T>(this T element, Action synchronize, params ReadOnlySpan<INotifyPropertyChanged> sources)
        where T : Element => element.ObserveState(synchronize, UiSynchronization.Immediate, sources);

    /// <summary>Synchronizes the current state of one or more borrowed sources while attached.</summary>
    /// <param name="element">The element to observe while attached.</param>
    /// <param name="synchronize">Reads the current state on the UI owner thread.</param>
    /// <param name="timing">Whether to synchronize on attach inline or on the attachment queue.</param>
    /// <param name="sources">The notifying sources, captured at registration.</param>
    /// <returns>The configured element.</returns>
    /// <remarks>Queued synchronization coalesces notifications and any deferred attachment synchronization.</remarks>
    public static T ObserveState<T>(this T element, Action synchronize, UiSynchronization timing,
        params ReadOnlySpan<INotifyPropertyChanged> sources) where T : Element
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(synchronize);
        if (sources.IsEmpty) throw new ArgumentException("At least one source is required.", nameof(sources));
        foreach (var source in sources)
            if (source is null) throw new ArgumentException("Sources cannot contain null.", nameof(sources));

        var snapshot = sources.ToArray();
        return OwnObserver(element, () => new AttachmentStateObserver(element, snapshot, synchronize, timing));
    }

    private static T OwnObserver<T>(T element, Func<IDisposable> create) where T : Element
    {
        element.VerifyAccess();
        var observer = create();
        try { element.Own(observer); }
        catch (Exception error)
        {
            try { observer.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
        return element;
    }

    /// <summary>Connects the button to the borrowed command and binds its enabled value to command availability.</summary>
    /// <remarks>Use one enabled binding per button. The command is observed only while attached, and the button owns cleanup.</remarks>
    public static T WithCommand<T>(this T button, IUiCommand command) where T : Button
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(command);
        button.OwnBinding(UiProperties.Enabled, () => new ButtonCommandBinding(button, command));
        return button;
    }

    /// <summary>Connects the button to a borrowed command selected from the current data context.</summary>
    /// <remarks>The selector runs on attach and context replacement. Null data or a null command disables the button.</remarks>
    public static T WithCommand<T, TData>(this T button, IDataContext<TData> context, Func<TData, IUiCommand?> selector)
        where T : Button where TData : class
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selector);
        button.OwnBinding(UiProperties.Enabled, () =>
        {
            if (!button.IsAttached) button.IsEnabled = false;
            return new AttachmentDataContext<TData>(button, context,
                (data, current) => selector(data) is { } command ? new ButtonCommandBinding(button, command, current) : null,
                connected => { if (!connected) button.IsEnabled = false; });
        });
        return button;
    }
}
