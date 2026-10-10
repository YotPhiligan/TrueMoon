using TrueMoon.Alloy;
using TrueMoon.Argentis;

namespace AlloyTest;

internal sealed class CustomTitleBar : Panel
{
    private readonly Text _title = new("TrueMoon Alloy");
    private readonly TextBox _note = new() { Value = "Заметка в заголовке" };
    private readonly Button _minimize, _maximize, _close;
    private float _titleWidth, _noteWidth;
    internal CustomTitleBar(IWindowCommands commands)
    {
        Height = 44; Padding = new Thickness(8); WindowRegion = WindowRegionRole.Caption;
        _minimize = new Button("—").OnClick(commands.Minimize);
        _maximize = new Button("□") { WindowRegion = WindowRegionRole.Maximize }.OnClick(() =>
        { if (commands.State == UiWindowState.Maximized) commands.Restore(); else commands.Maximize(); });
        _close = new Button("×").OnClick(commands.Close);
        Add(_title); Add(_note); Add(_minimize); Add(_maximize); Add(_close);
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var captionWidth = Math.Max(0, available.Width - 120);
        _titleWidth = Math.Min(240, Math.Max(0, captionWidth - 160));
        _noteWidth = captionWidth - _titleWidth;
        foreach (var child in Children) child.Measure(available, text);
        return new Size(available.Width, 28);
    }
    protected override void ArrangeCore(Rect area)
    {
        var captionWidth = Math.Max(0, area.Width - 120);
        _title.Arrange(new Rect(area.X, area.Y, Math.Min(_titleWidth, captionWidth), area.Height));
        _note.Arrange(new Rect(area.X + _titleWidth, area.Y, _noteWidth, area.Height));
        _minimize.Arrange(new Rect(area.X + captionWidth, area.Y, 40, area.Height));
        _maximize.Arrange(new Rect(area.X + captionWidth + 40, area.Y, 40, area.Height));
        _close.Arrange(new Rect(area.X + captionWidth + 80, area.Y, 40, area.Height));
    }
    protected override void DrawCore(IDrawingContext context) => context.Fill(Bounds, Theme.Control);
}
