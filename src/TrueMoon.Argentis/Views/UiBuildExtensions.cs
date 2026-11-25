using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace TrueMoon.Argentis.Views;

/// <summary>Fluent composition and contextual bindings for deferred view content.</summary>
public static class UiBuildExtensions
{
    extension<TData>(IUiBuildContext<TData> ui)
    {
        /// <summary>Creates a text label through the registered factory.</summary>
        public UiControl<Text, TData> Text(string text = "") => ui.Create<Text>().Value(text);

        /// <summary>Creates a text button.</summary>
        public UiControl<Button, TData> Button(string text = "") => ui.Create<Button>().Value(text);

        /// <summary>Creates a checkbox with the supplied label.</summary>
        public UiControl<CheckBox, TData> CheckBox(string text = "") => ui.Create<CheckBox>().Value(text);

        /// <summary>Creates a single-line editor.</summary>
        public UiControl<TextBox, TData> TextBox() => ui.Create<TextBox>();

        /// <summary>Creates and populates a horizontal stack through the registered factory.</summary>
        public UiControl<HStack, TData> HStack(params Element[] children) => ui.Create<HStack>().WithChildren(children);

        /// <summary>Creates and populates a vertical stack.</summary>
        public UiControl<VStack, TData> VStack(params Element[] children) => ui.Create<VStack>().WithChildren(children);

        /// <summary>Creates and populates a panel.</summary>
        public UiControl<Panel, TData> Panel(params Element[] children) => ui.Create<Panel>().WithChildren(children);

        /// <summary>Creates a bordered content container.</summary>
        public UiControl<Border, TData> Border(Element? child = null) => ui.Create<Border>().Content(child);

        /// <summary>Creates a scrolling content container.</summary>
        public UiControl<ScrollViewer, TData> ScrollViewer(Element? child = null) => ui.Create<ScrollViewer>().Content(child);

        /// <summary>Creates a tree control.</summary>
        public UiControl<TreeView, TData> TreeView() => ui.Create<TreeView>();

        /// <summary>Creates a rich-text editor, using the typed factory when options are supplied.</summary>
        public UiControl<RichTextEditor, TData> RichTextEditor(RichTextEditorOptions? options = null) => options is null
            ? ui.Create<RichTextEditor>() : ui.Create<RichTextEditor, RichTextEditorOptions>(options);

        /// <summary>Creates an overlay host owning its normal content.</summary>
        public UiControl<OverlayHost, TData> OverlayHost(Element child) => ui.Create<OverlayHost, Element>(child);

        /// <summary>Creates an icon button with the supplied semantic name.</summary>
        public UiControl<IconButton, TData> IconButton(UiSymbol symbol, string name) =>
            ui.Create<IconButton, (UiSymbol Symbol, string Name)>((symbol, name));
    }

    extension<TControl, TData>(UiControl<TControl, TData> control) where TControl : Element
    {
        /// <summary>Names the underlying layout element in its owning view.</summary>
        public UiControl<TControl, TData> Named(string name)
        {
            control.Element.Named(name);
            return control;
        }

        /// <summary>Configures the underlying control and continues its typed chain.</summary>
        public UiControl<TControl, TData> Configure(Action<TControl> configure)
        {
            control.Element.Configure(configure);
            return control;
        }

        /// <summary>Sets a typed UI property.</summary>
        public UiControl<TControl, TData> SetValue<TValue>(UiProperty<TValue> property, TValue value)
        {
            control.Element.Set(property, value);
            return control;
        }

        /// <summary>Sets the background.</summary>
        public UiControl<TControl, TData> Background(Color color) => control.SetValue(UiProperties.Background, color);

        /// <summary>Sets the automation target ID.</summary>
        public UiControl<TControl, TData> SemanticId(string id)
        {
            control.Element.SemanticId = id;
            return control;
        }

        /// <summary>Shares a stack's remaining space.</summary>
        public UiControl<TControl, TData> Fill(bool enabled = true) => control.SetValue(StackBase.FillProperty, enabled);

        /// <summary>Sets the explicit width.</summary>
        public UiControl<TControl, TData> Width(float width)
        {
            control.Element.Width = width;
            return control;
        }

        /// <summary>Sets the explicit height.</summary>
        public UiControl<TControl, TData> Height(float height)
        {
            control.Element.Height = height;
            return control;
        }

        /// <summary>Sets uniform padding.</summary>
        public UiControl<TControl, TData> Padding(float padding)
        {
            control.Element.Padding(padding);
            return control;
        }

        /// <summary>Sets the local font weight.</summary>
        public UiControl<TControl, TData> FontWeight(int weight)
        {
            control.Element.FontWeight(weight);
            return control;
        }

        /// <summary>Sets local italic rendering.</summary>
        public UiControl<TControl, TData> FontItalic(bool italic = true)
        {
            control.Element.FontItalic(italic);
            return control;
        }

        /// <summary>Sets a local backdrop material.</summary>
        public UiControl<TControl, TData> WithBackdrop(BackdropMaterial? material)
        {
            control.Element.WithBackdrop(material);
            return control;
        }

        /// <summary>Binds to an explicit stable model.</summary>
        public UiControl<TControl, TData> Bind<TSource, TValue>(UiProperty<TValue> property, TSource source,
            Expression<Func<TSource, TValue>> selector) where TSource : class, INotifyPropertyChanged
        {
            control.Element.Bind(property, source, selector);
            return control;
        }

        /// <summary>Binds to an explicit replaceable context.</summary>
        public UiControl<TControl, TData> Bind<TSource, TValue>(UiProperty<TValue> property, IDataContext<TSource> context,
            Expression<Func<TSource, TValue>> selector) where TSource : class, INotifyPropertyChanged
        {
            control.Element.Bind(property, context, selector);
            return control;
        }

        /// <summary>Binds bidirectionally to an explicit stable model.</summary>
        public UiControl<TControl, TData> BindTwoWay<TSource, TValue>(UiProperty<TValue> property, TSource source,
            Expression<Func<TSource, TValue>> selector) where TSource : class, INotifyPropertyChanged
        {
            control.Element.BindTwoWay(property, source, selector);
            return control;
        }

        /// <summary>Binds bidirectionally to an explicit replaceable context.</summary>
        public UiControl<TControl, TData> BindTwoWay<TSource, TValue>(UiProperty<TValue> property, IDataContext<TSource> context,
            Expression<Func<TSource, TValue>> selector) where TSource : class, INotifyPropertyChanged
        {
            control.Element.BindTwoWay(property, context, selector);
            return control;
        }
    }

    extension<TControl, TData>(UiControl<TControl, TData> control)
        where TControl : Element where TData : class, INotifyPropertyChanged
    {
        /// <summary>Binds to the view's current context.</summary>
        public UiControl<TControl, TData> Bind<TValue>(UiProperty<TValue> property, Expression<Func<TData, TValue>> selector) =>
            control.Bind(property, control.Context, selector);

        /// <summary>Binds bidirectionally to the view's current context.</summary>
        public UiControl<TControl, TData> BindTwoWay<TValue>(UiProperty<TValue> property, Expression<Func<TData, TValue>> selector) =>
            control.BindTwoWay(property, control.Context, selector);

        /// <summary>Observes notifications from the current context for the control's attachment lifetime.</summary>
        public UiControl<TControl, TData> ObservePropertyChanged(PropertyChangedEventHandler handler, Action? synchronize = null)
        {
            control.Element.ObservePropertyChanged(control.Context, handler, synchronize);
            return control;
        }
    }

    extension<TControl, TData>(UiControl<TControl, TData> control) where TControl : Text
    {
        /// <summary>Sets a text value.</summary>
        public UiControl<TControl, TData> Value(string value)
        {
            control.Element.Value = value;
            return control;
        }
    }

    extension<TControl, TData>(UiControl<TControl, TData> control)
        where TControl : Text where TData : class, INotifyPropertyChanged
    {
        /// <summary>Binds text from the view's current context.</summary>
        public UiControl<TControl, TData> BindText(Expression<Func<TData, string>> selector) =>
            control.Bind(global::TrueMoon.Argentis.Text.ValueProperty, selector);
    }

    extension<TControl, TData>(UiControl<TControl, TData> control) where TControl : ElementList
    {
        /// <summary>Adds a batch of child elements in paint order.</summary>
        /// <remarks>Validates every child before adopting any. Attachment notifications run after the entire batch is adopted.</remarks>
        /// <param name="children">The unowned children to add.</param>
        /// <returns>The same control with its data context.</returns>
        /// <seealso cref="ElementExtensions.WithChildren{T}(T, Element[])"/>
        public UiControl<TControl, TData> WithChildren(params Element[] children)
        {
            control.Element.WithChildren(children);
            return control;
        }

        /// <summary>Binds children to an observable collection.</summary>
        public UiControl<TControl, TData> BindItems<TItem>(ObservableCollection<TItem> items, Func<TItem, Element> factory)
            where TItem : class
        {
            control.Element.BindItems(items, factory);
            return control;
        }

        /// <summary>Binds children to a read-only observable collection.</summary>
        public UiControl<TControl, TData> BindItems<TItem>(ReadOnlyObservableCollection<TItem> items, Func<TItem, Element> factory)
            where TItem : class
        {
            control.Element.BindItems(items, factory);
            return control;
        }
    }

    extension<TControl, TData>(UiControl<TControl, TData> control) where TControl : ContentControl
    {
        /// <summary>Adopts the supplied content.</summary>
        public UiControl<TControl, TData> Content(Element? child)
        {
            control.Element.SetContent(child);
            return control;
        }
    }

    extension<TControl, TData>(UiControl<TControl, TData> control) where TControl : Button
    {
        /// <summary>Registers a click handler for the control's lifetime.</summary>
        public UiControl<TControl, TData> OnClick(Action action)
        {
            control.Element.OnClick(action);
            return control;
        }

        /// <summary>Connects a stable command.</summary>
        public UiControl<TControl, TData> WithCommand(IUiCommand command)
        {
            control.Element.WithCommand(command);
            return control;
        }

        /// <summary>Connects a command selected from an explicit context.</summary>
        public UiControl<TControl, TData> WithCommand<TSource>(IDataContext<TSource> context, Func<TSource, IUiCommand?> selector)
            where TSource : class
        {
            control.Element.WithCommand(context, selector);
            return control;
        }
    }

    extension<TControl, TData>(UiControl<TControl, TData> control) where TControl : Button where TData : class
    {
        /// <summary>Connects a command selected from the view's current context.</summary>
        public UiControl<TControl, TData> WithCommand(Func<TData, IUiCommand?> selector)
        {
            control.Element.WithCommand(control.Context, selector);
            return control;
        }
    }

    /// <summary>Adds a typed styling role while retaining the build context.</summary>
    public static UiControl<TControl, TData> WithRole<TControl, TData, TElement>(
        this UiControl<TControl, TData> control, ViewStyleRole<TElement> role)
        where TControl : TElement where TElement : Element
    {
        control.Element.WithRole(role);
        return control;
    }
}
