using System.Collections.Immutable;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime.Semantics;

internal static class UiSemanticCapture
{
    internal static UiSemanticSnapshot Take(long sessionId, long treeGeneration, long layoutGeneration, UiViewport viewport,
        Element root, Element? focus, Element? capture, int maximumNodes, bool includeAccessibleNames)
    {
        var nodes = ImmutableArray.CreateBuilder<UiSemanticNode>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<Children>();
        pending.Push(new([root], 0, null, new(0, 0, viewport.Width / viewport.Scale, viewport.Height / viewport.Scale), true, true));
        while (pending.TryPeek(out _) && nodes.Count < maximumNodes)
        {
            var next = pending.Pop();
            var element = next.Elements[next.Index];
            if (next.Index + 1 < next.Elements.Count) pending.Push(next with { Index = next.Index + 1 });
            var visible = next.Visible && element.IsVisible;
            var enabled = next.Enabled && element.IsEnabled;
            var bounds = element.Bounds;
            var visibleBounds = visible ? Intersect(bounds, next.Clip) : default;
            var popup = visible && element.Parent is OverlayHost overlay && ReferenceEquals(overlay.Popup, element);
            var id = element.SemanticId;
            if (id != null && !ids.Add(id)) throw new InvalidOperationException($"Duplicate UI semantic ID '{id}'.");
            var index = nodes.Count;
            nodes.Add(new(next.ParentIndex, id, Role(element, popup), includeAccessibleNames ? Label(element.AccessibleName) : null,
                bounds, visibleBounds, visible, enabled, element.Focusable, visible && enabled && ReferenceEquals(focus, element),
                visible && enabled && ReferenceEquals(capture, element), Selection(element), element.HasError, popup));
            if (element.Children is { Count: > 0 } children)
                pending.Push(new(children, 0, index, Intersect(visibleBounds, element.ChildClipBounds), visible, enabled));
        }
        return new(sessionId, treeGeneration, layoutGeneration, viewport, nodes.ToImmutable(), pending.Count != 0);
    }

    private static Rect Intersect(Rect left, Rect right)
    {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        return new(x, y, Math.Max(0, Math.Min(left.X + left.Width, right.X + right.Width) - x),
            Math.Max(0, Math.Min(left.Y + left.Height, right.Y + right.Height) - y));
    }

    private static string? Label(string text) => text.Length switch
    {
        0 => null,
        <= 256 => text,
        _ => text[..(char.IsHighSurrogate(text[255]) ? 255 : 256)]
    };

    private static bool? Selection(Element element) => element switch
    {
        CheckBox check => check.IsChecked,
        TreeView tree => tree.SelectedId != null,
        _ => null
    };

    private static UiSemanticRole Role(Element element, bool popup) => popup ? UiSemanticRole.Popup : element switch
    {
        CheckBox => UiSemanticRole.CheckBox,
        Button => UiSemanticRole.Button,
        TextBox or RichTextEditor => UiSemanticRole.TextInput,
        NumberBox => UiSemanticRole.NumberInput,
        TreeView => UiSemanticRole.Tree,
        ScrollViewer => UiSemanticRole.Scroll,
        Text => UiSemanticRole.Text,
        _ => UiSemanticRole.Group
    };

    private readonly record struct Children(IReadOnlyList<Element> Elements, int Index, int? ParentIndex, Rect Clip,
        bool Visible, bool Enabled);
}
