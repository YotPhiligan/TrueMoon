using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis.Layout;

/// <summary>An element with replaceable content.</summary>
public class ContentControl : Element
{
    private Element[] _children = [];
    /// <summary>The current child.</summary>
    public Element? Child => _children.Length == 0 ? null : _children[0];
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => _children;
    /// <summary>Replaces content. The caller owns the detached old subtree and must reuse or dispose it.</summary>
    /// <param name="content">An unowned live subtree, or null to clear content.</param>
    /// <remarks>Validation failures preserve the tree. Notification failures are reported after the replacement commits.</remarks>
    public void SetContent(Element? content)
    {
        VerifyTreeAccess();
        var previous = Child;
        if (ReferenceEquals(content, previous)) return;
        if (content != null)
            ValidateChildChange([content], previous == null ? [] : [previous]);
        using var change = new TreeChange(this, previous, content);
        _children = content == null ? [] : [content];
        if (previous != null) Orphan(previous, change);
        if (content != null) Adopt(content, change);
        change.Schedule(() => Invalidate(Invalidation.Tree | Invalidation.Layout));
        change.Complete();
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) { Child?.Measure(available, text); return Child?.DesiredSize ?? default; }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area) => Child?.Arrange(area);
}
