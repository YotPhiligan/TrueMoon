using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Views;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Maps concrete controls to their borrowed, application-configured factories.</summary>
public sealed class UiControlFactories : IUiControlFactories
{
    private static readonly FrozenDictionary<(Type Control, Type? Arguments), object> BuiltIn = CreateBuiltIns();
    private readonly ConcurrentDictionary<(Type Control, Type? Arguments), object> _factories = new();



    /// <summary>Registers a factory for a concrete control type before building views that use it.</summary>
    public void Register<TControl>(IFactory<TControl> factory) where TControl : Element
    {
        Register((typeof(TControl), null), factory);
    }

    /// <summary>Registers a factory with an explicit constructor argument type.</summary>
    public void Register<TControl, TArguments>(IFactory<TControl, TArguments> factory) where TControl : Element =>
        Register((typeof(TControl), typeof(TArguments)), factory);

    /// <inheritdoc />
    public TControl Create<TControl>() where TControl : Element =>
        ((IFactory<TControl>)GetFactory((typeof(TControl), null))).Create()
        ?? throw new InvalidOperationException($"The UI factory for '{typeof(TControl)}' returned no control.");

    /// <inheritdoc />
    public TControl Create<TControl, TArguments>(TArguments arguments) where TControl : Element =>
        ((IFactory<TControl, TArguments>)GetFactory((typeof(TControl), typeof(TArguments)))).Create(arguments)
        ?? throw new InvalidOperationException($"The UI factory for '{typeof(TControl)}' returned no control.");

    private void Register((Type Control, Type? Arguments) key, object factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(key, factory))
            throw new InvalidOperationException($"A UI factory for '{key.Control}' with arguments '{key.Arguments}' is already registered.");
    }

    private object GetFactory((Type Control, Type? Arguments) key)
    {
        if (_factories.TryGetValue(key, out var factory) || BuiltIn.TryGetValue(key, out factory))
            return factory;
        throw new InvalidOperationException($"No UI factory is registered for '{key.Control}' with arguments '{key.Arguments}'.");
    }

    private static FrozenDictionary<(Type Control, Type? Arguments), object> CreateBuiltIns()
    {
        var factories = new UiControlFactories();
        factories.Register(new Factory<Text>(() => new Text()));
        factories.Register(new Factory<Button>(() => new Button()));
        factories.Register(new Factory<TextBox>(() => new TextBox()));
        factories.Register(new Factory<HStack>(() => new HStack()));
        factories.Register(new Factory<VStack>(() => new VStack()));
        factories.Register(new Factory<Panel>(() => new Panel()));
        factories.Register(new Factory<ContentControl>(() => new ContentControl()));
        factories.Register(new Factory<Border>(() => new Border()));
        factories.Register(new Factory<ScrollViewer>(() => new ScrollViewer()));
        factories.Register(new Factory<TreeView>(() => new TreeView()));
        factories.Register(new Factory<CheckBox>(() => new CheckBox()));
        factories.Register(new Factory<Image>(() => new Image()));
        factories.Register(new Factory<ProgressBar>(() => new ProgressBar()));
        factories.Register(new Factory<Slider>(() => new Slider()));
        factories.Register(new Factory<SplitView>(() => new SplitView()));
        factories.Register(new Factory<RichTextEditor>(() => new RichTextEditor()));
        factories.Register<RichTextEditor, RichTextEditorOptions>(new Factory<RichTextEditor, RichTextEditorOptions>(options => new(options)));
        factories.Register<OverlayHost, Element>(new Factory<OverlayHost, Element>(content => new(content)));
        factories.Register<IconButton, (UiSymbol Symbol, string Name)>(new Factory<IconButton, (UiSymbol Symbol, string Name)>(
            args => new(args.Symbol, args.Name)));
        factories.Register(new Factory<Line>(() => new Line()));
        factories.Register(new Factory<Rectangle>(() => new Rectangle()));
        factories.Register(new Factory<NumberBox>(() => new NumberBox()));
        factories.Register(new Factory<Vector3Editor>(() => new Vector3Editor()));

        return factories._factories.ToFrozenDictionary();
    }
}
