using TrueMoon.Argentis;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Alloy.Runtime;

internal static partial class StockPresenters
{
    internal static void AddFields(Dictionary<Type, ElementPresentation> entries)
    {
        entries.Add(typeof(TextBox), ElementPresentation.Create<TextBox>(DrawTextBox));
        entries.Add(typeof(NumberBox), ElementPresentation.Create<NumberBox>(DrawNumberBox));
        entries.Add(typeof(RichTextEditor), ElementPresentation.Create<RichTextEditor>(DrawRichTextEditor));
        entries.Add(typeof(TreeView), ElementPresentation.Create<TreeView>(DrawTreeView));
        entries.Add(typeof(SplitView.DividerElement), ElementPresentation.Create<SplitView.DividerElement>(DrawSplitDivider));
        entries.Add(typeof(ScrollViewer), ElementPresentation.Create<ScrollViewer>(DrawScrollViewer));
    }

    private static void DrawTextBox(TextBox field, IDrawingContext context)
    {
        var bounds = field.Bounds;
        if (field.IsReadOnly && !field.HasError)
            context.Fill(new(bounds.X + bounds.Width - 4, bounds.Y + 4, 2, Math.Max(0, bounds.Height - 8)), field.Theme.Border);
        var x = field.TextViewport.X - field.HorizontalOffset;
        var y = field.LineY;
        var selection = field.IsFocused ? field.SelectionLength : 0;
        var highlight = field.Theme.Accent with { A = 100 };
        context.Save();
        try
        {
            context.Clip(field.TextViewport);
            if (field.ShapedLine is { } shaped)
            {
                if (context is not IRichTextDrawingContext rich)
                    throw new InvalidOperationException("Shaped layout requires a matching rich text drawing context.");
                rich.RichText(shaped, x, y, field.SelectionStart, selection, highlight);
            }
            else
            {
                if (selection > 0)
                    context.Fill(new(x + Math.Min(field.CaretX, field.AnchorX), y,
                        Math.Abs(field.CaretX - field.AnchorX), field.LineHeight), highlight);
                context.EditorText(field.Value, x, y, field.IsEffectivelyEnabled ? field.Foreground : field.Theme.Disabled,
                    field.FontSize, field.FontFamily, field.FontWeight, field.FontItalic);
            }
            if (field.IsFocused) context.Fill(new(x + field.CaretX, y, 1, field.LineHeight), field.Foreground);
        }
        finally { context.Restore(); }
    }

    private static void DrawNumberBox(NumberBox box, IDrawingContext context)
    {
        var dragBounds = box.DragBounds;
        if (!box.AdjustmentEnabled || box.Prefix.Value.Length != 0 || dragBounds.Width <= 0) return;
        var color = box.Editor.IsEffectivelyEnabled && !box.Editor.IsReadOnly ? box.Theme.EditorColor(box.ColorRole) : box.Theme.Disabled;
        var size = Math.Min(12, Math.Min(dragBounds.Width, dragBounds.Height));
        UiSymbols.Draw(context, UiSymbol.AdjustHorizontal,
            new(dragBounds.X + (dragBounds.Width - size) / 2, dragBounds.Y + (dragBounds.Height - size) / 2, size, size), color);
    }

    private static void DrawRichTextEditor(RichTextEditor editor, IDrawingContext context)
    {
        if (editor.PreparedLayout is not { } layout) return;
        if (context is not IRichTextDrawingContext rich) throw new NotSupportedException("RichTextEditor requires an IRichTextDrawingContext backend.");
        var bounds = editor.Bounds;
        if (editor.Options.IsReadOnly && !editor.HasError)
            context.Fill(new(bounds.X + bounds.Width - 4, bounds.Y + 4, 2, Math.Max(0, bounds.Height - 8)), editor.Theme.Border);
        var area = editor.TextViewport;
        var x = area.X - editor.HorizontalOffset;
        var y = area.Y - editor.VerticalOffset;
        context.Save();
        try
        {
            context.Clip(area);
            var selected = editor.IsFocused || editor.Options.ShowSelectionWhenUnfocused;
            rich.RichText(layout, x, y, editor.SelectionStart, selected ? editor.SelectionLength : 0,
                editor.Options.SelectionColor ?? (editor.Theme.Accent with { A = 100 }));
            if (editor.IsFocused && editor.IsEffectivelyEnabled)
            {
                var caret = editor.CaretBounds;
                context.Fill(new(x + caret.X, y + caret.Y, 1, caret.Height), editor.Foreground);
            }
        }
        finally { context.Restore(); }
    }

    private static void DrawTreeView(TreeView tree, IDrawingContext context)
    {
        var visible = tree.VisibleArea(); if (visible.Width <= 0 || visible.Height <= 0) return;
        var theme = tree.Theme;
        var rows = tree.Rows;
        var first = Math.Max(0, (int)MathF.Floor((visible.Y - tree.Bounds.Y - tree.Padding.Top) / tree.RowHeight));
        var end = Math.Min(rows.Length, (int)MathF.Ceiling((visible.Y + visible.Height - tree.Bounds.Y - tree.Padding.Top) / tree.RowHeight));
        for (var i = first; i < end; i++)
        {
            ref readonly var row = ref rows[i]; var bounds = tree.RowBounds(i); var selected = row.Item.Id == tree.SelectedId;
            if (selected) context.Fill(bounds, tree.IsEffectivelyEnabled ? theme.Accent : theme.ControlDisabled);
            else if (tree.IsHovered && row.Item.Id == tree.HoveredId) context.Fill(bounds, theme.ControlHover);
            var color = !tree.IsEffectivelyEnabled ? theme.Disabled : selected ? theme.OnAccent : tree.Foreground;
            var contentWidth = Math.Max(0, bounds.Width - 36);
            var indent = Math.Min(row.Depth * tree.Indent, contentWidth);
            var x = bounds.X + indent;
            var y = bounds.Y + (bounds.Height - 14) / 2;
            if (row.HasChildren)
                UiSymbols.Draw(context, row.Expanded ? UiSymbol.ChevronDown : UiSymbol.ChevronRight, new(x, y, 14, 14), color);
            UiSymbols.Draw(context, row.Item.Icon, new(x + 16, y, 14, 14), color);
            context.Text(tree.FitLabel(in row, contentWidth - indent), x + 34,
                bounds.Y + (bounds.Height - tree.FontSize) / 2, color, tree.FontSize, tree.FontFamily, tree.FontWeight, tree.FontItalic);
            if (tree.HasDropTarget)
            {
                if (tree.DropPosition == TreeDropPosition.Into && tree.DropParent == row.Item.Id) context.Stroke(bounds.Deflate(new Thickness(1)), theme.Focus, 2);
                else if (tree.DropPosition != TreeDropPosition.Into && tree.DropAnchor == row.Item.Id)
                    context.Fill(new(bounds.X, tree.DropPosition == TreeDropPosition.Before ? bounds.Y : bounds.Y + bounds.Height - 2, bounds.Width, 2), theme.Focus);
            }
        }
        if (tree.HasDropTarget && tree.DropPosition == TreeDropPosition.Into && tree.DropParent == null) context.Stroke(visible.Deflate(new Thickness(1)), theme.Focus, 2);
    }

    private static void DrawSplitDivider(SplitView.DividerElement divider, IDrawingContext context)
    {
        var bounds = divider.Bounds;
        var horizontal = divider.Horizontal;
        context.Fill(horizontal ? new(bounds.X + Math.Max(0, (bounds.Width - 2) / 2), bounds.Y, Math.Min(2, bounds.Width), bounds.Height)
            : new(bounds.X, bounds.Y + Math.Max(0, (bounds.Height - 2) / 2), bounds.Width, Math.Min(2, bounds.Height)), divider.Foreground);
    }

    private static void DrawScrollViewer(ScrollViewer viewer, IDrawingContext context)
    {
        if (!viewer.HasScrollBar) return;
        var theme = viewer.Theme;
        context.Fill(viewer.Track, theme.Surface);
        context.Fill(viewer.Thumb, !viewer.IsEffectivelyEnabled ? theme.Disabled : viewer.IsScrollDragging || viewer.IsHovered ? theme.Accent : theme.Border);
    }
}
