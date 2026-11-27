using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Alloy.Runtime;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TrueMoon.Argentis.Benchmarks;

/// <summary>Fixed text metrics so the View layer is measured without any font engine. Every line is length*size/2 by size.</summary>
internal sealed class FixedTextLayout : ITextLayoutService
{
    public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize);
}

/// <summary>Headless surface. <see cref="Render"/> does not walk or draw the tree, so a dirty <c>Update</c> covers bindings and layout only.</summary>
internal sealed class HeadlessSurface : IUiRenderSurface
{
    public const int Width = 1200;
    public const int Height = 2000;

    public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
    public int Resizes { get; private set; }
    public int Renders { get; private set; }
    public void VerifyAvailable() { }
    public void Resize(UiViewport viewport) => Resizes++;
    public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) => Renders++;
    public void Dispose() { }
}

/// <summary>Fixed-size leaf used instead of a real editor so text shaping stays out of the measured path.</summary>
internal sealed class SizedElement(float width = 90, float height = 18) : Element
{
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(width, height);
}

/// <summary>Row/notification source of the inspector-shaped scene.</summary>
internal sealed class RowModel(int index) : INotifyPropertyChanged
{
    private string _name = "row-" + index.ToStringInvariant();
    private float _number;
    public int Id { get; } = index;

    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; Notify(); }
    }

    public float Number
    {
        get => _number;
        set { if (_number == value) return; _number = value; Notify(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised for a property without changing its stored value.</summary>
    public void NotifySilently([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

internal static class SceneExtensions
{
    public static string ToStringInvariant(this int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal static class Scene
{
    /// <summary>Nodes per inspector row: the row container, the label, the editor leaf, the value and the unit labels.</summary>
    public const int NodesPerRow = 5;

    /// <summary>Total nodes of an inspector scene, including the root panel.</summary>
    public static int Nodes(int rows) => rows * NodesPerRow + 1;

    /// <summary>Inspector-shaped detached tree: one row per index with label, editor leaf and two value labels.</summary>
    public static VStack Inspector(int rows)
    {
        var root = new VStack();
        for (var index = 0; index < rows; index++)
        {
            var label = "field-" + index.ToStringInvariant();
            root.Add(new HStack().WithChildren(
                new Text(label),
                new SizedElement(),
                new Text(index.ToStringInvariant()),
                new Text("mm")));
        }
        return root;
    }

    /// <summary>Detached chain of nested panels with the leaf at <paramref name="depth"/>; used for walk-cost depth.</summary>
    public static (VStack Root, Element Leaf) Chain(int depth)
    {
        var root = new VStack();
        ElementList parent = root;
        for (var level = 1; level < depth; level++)
        {
            var nested = new VStack();
            parent.Add(nested);
            parent = nested;
        }
        var leaf = new SizedElement();
        parent.Add(leaf);
        return (root, leaf);
    }

    /// <summary>Bound row: label element bound one-way to the model name plus a fixed editor leaf.</summary>
    public static Element BoundRow(RowModel model) =>
        new HStack().WithChildren(
            new Text().Bind(Text.ValueProperty, model, row => row.Name),
            new SizedElement());

    public static ObservableCollection<RowModel> Models(int rows)
    {
        var source = new ObservableCollection<RowModel>();
        for (var index = 0; index < rows; index++) source.Add(new RowModel(index));
        return source;
    }

    /// <summary>The same rows in the shape an application view model exposes: a plain mutable list.</summary>
    public static List<RowModel> PlainModels(int rows)
    {
        var source = new List<RowModel>(rows);
        for (var index = 0; index < rows; index++) source.Add(new RowModel(index));
        return source;
    }

    public static UiSession Session(Element root) => new(root, new HeadlessSurface(), new UiViewport(HeadlessSurface.Width, HeadlessSurface.Height));

    public static Size Available => new(HeadlessSurface.Width, HeadlessSurface.Height);
    public static Rect Slot => new(0, 0, HeadlessSurface.Width, HeadlessSurface.Height);
}
