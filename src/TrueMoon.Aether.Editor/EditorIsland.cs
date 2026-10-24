using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>A parent-owned panel island with a bounded reveal transition and a soft local shadow.</summary>
public sealed class EditorIsland : ContentControl, IUiFrameParticipant
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(160);
    private readonly ScalarTransition _reveal = new();
    private readonly bool _fromRight;
    private bool _contentEnabled = true;
    /// <summary>Requested contextual visibility; closing content stops accepting input immediately.</summary>
    public static readonly UiProperty<bool> IsOpenProperty = new("IsOpen", true, Invalidation.Layout | Invalidation.Tree);
    /// <summary>Creates an island adopting a fresh content tree. The parent owns this element and its content.</summary>
    /// <param name="content">Unowned panel view or surface.</param><param name="fromRight">Slide in from the right rather than left.</param>
    public EditorIsland(Element content, bool fromRight = false)
    {
        ArgumentNullException.ThrowIfNull(content); _fromRight = fromRight; _contentEnabled = content.IsEnabled;
        _reveal.MoveTo(1, TimeSpan.Zero); SetContent(content); AttachmentChanged += Attachment;
    }
    /// <summary>Whether the panel's entity or tool context is open.</summary>
    public bool IsOpen { get => Get(IsOpenProperty); set => Set(IsOpenProperty, value); }
    /// <summary>Current group opacity and reveal progress.</summary>
    public float Reveal => _reveal.Value;
    /// <inheritdoc />
    public bool NeedsFrame => _reveal.IsRunning;
    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (!ReferenceEquals(property, IsOpenProperty)) return;
        if (IsOpen)
        {
            IsVisible = true;
            if (Child != null) Child.IsEnabled = _contentEnabled;
        }
        else if (Child != null) { _contentEnabled = Child.IsEnabled; Child.IsEnabled = false; }
        _reveal.MoveTo(IsOpen ? 1 : 0, IsAttached ? Duration : TimeSpan.Zero);
        ApplyReveal();
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    { VerifyAccess(); if (_reveal.Advance(elapsed)) ApplyReveal(); }
    private void ApplyReveal()
    {
        Opacity = Reveal;
        if (!IsOpen && Reveal == 0) IsVisible = false;
        Invalidate(Invalidation.Layout);
    }
    private void Attachment()
    {
        if (IsAttached) return;
        _reveal.Complete(); Opacity = Reveal;
        if (!IsOpen) IsVisible = false;
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var slide = (1 - Reveal) * 8 * (_fromRight ? 1 : -1);
        Child?.Arrange(new(area.X + slide, area.Y, area.Width, area.Height));
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        if (Child is not { IsVisible: true } child) return;
        var area = child.Bounds;
        for (var spread = 6; spread >= 1; spread--)
            context.Fill(new(area.X - spread, area.Y - spread + 2, area.Width + spread * 2, area.Height + spread * 2),
                new(0, 0, 0, (byte)(Theme.Surface.R < 128 ? 8 : 5)), child.CornerRadius + spread);
    }
    /// <inheritdoc />
    public override void Dispose()
    { if (IsDisposed) return; AttachmentChanged -= Attachment; _reveal.Complete(); base.Dispose(); }
}
