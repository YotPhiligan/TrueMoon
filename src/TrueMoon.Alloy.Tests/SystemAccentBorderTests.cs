using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SystemAccentBorderTests
{
    private static readonly Color Accent = new(100, 150, 200);

    [Fact]
    public void HostAccent_RefreshesExistingFrameAndReadFailureReturnsToThemeBorder()
    {
        using var test = new Fixture(); test.Ui.Update();
        Assert.Equal(Accent, test.Frame.BorderColor);
        var bounds = test.Scene.Bounds; var title = test.Frame.DefaultTitleBar;
        test.Host.AccentColor = new(200, 100, 50); test.Binding.Synchronize(true); test.Ui.Update();
        Assert.Equal(new Color(200, 100, 50), test.Frame.BorderColor);
        test.Host.AccentColor = null; test.Binding.Synchronize(true); test.Ui.Update();
        Assert.Null(test.Frame.SystemAccentColor); Assert.Equal(Theme.Dark.Border, test.Frame.BorderColor);
        test.Ui.SetTheme(Theme.Light); test.Ui.Update();
        Assert.Equal(Theme.Light.Border, test.Frame.BorderColor);
        test.Host.AccentColor = Accent; test.Binding.Synchronize(true); test.Ui.Update();
        Assert.Equal(Accent, test.Frame.BorderColor);
        Assert.Equal(bounds, test.Scene.Bounds); Assert.Same(title, test.Frame.DefaultTitleBar);
        Assert.Equal(1, test.Frame.BorderWidth); Assert.False(test.Ui.NeedsUpdate);
    }

    [Fact]
    public void UnsupportedHost_UsesThemeFallbackAndDarkensItOnFocusLoss()
    {
        var frame = WindowFrame.CreateDefault("window", new Panel()); frame.UseSystemAccentBorder = true;
        var binding = new DefaultWindowFrameBinding(frame, new Commands());
        binding.Synchronize(false);
        using var ui = new UiSession(frame, new Surface(), new(400, 200)); ui.Update();
        Assert.Null(frame.SystemAccentColor);
        Assert.Equal(Dim(Theme.Dark.Border), frame.BorderColor);
        Assert.False(frame.NeedsFrame); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void FocusLoss_DarkensBorderBy40PercentAt160MillisecondsWithoutChangingSceneOrText()
    {
        using var test = new Fixture(); test.Ui.Update(); test.Ui.Focus(test.Scene);
        test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Home));
        for (var i = 0; i < 2; i++) test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Right));
        for (var i = 0; i < 3; i++) test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Right, Shift: true));
        var bounds = test.Scene.Bounds; var close = test.Frame.DefaultTitleBar!.CloseButton.Bounds;
        var foreground = test.Scene.Foreground;
        test.Binding.Synchronize(false); test.Advance(80);
        Assert.Equal(new Color(80, 120, 160), test.Frame.BorderColor);
        Assert.True(test.Frame.NeedsFrame);
        test.Advance(79); Assert.True(test.Frame.NeedsFrame);
        test.Advance(1);
        Assert.Equal(new Color(60, 90, 120), test.Frame.BorderColor);
        Assert.False(test.Frame.NeedsFrame); Assert.False(test.Ui.NeedsUpdate);
        Assert.Equal(bounds, test.Scene.Bounds); Assert.Equal(close, test.Frame.DefaultTitleBar.CloseButton.Bounds);
        Assert.Equal("unchanged", test.Scene.Value); Assert.Equal(2, test.Scene.SelectionStart); Assert.Equal(3, test.Scene.SelectionLength);
        Assert.True(test.Scene.IsFocused); Assert.Equal(foreground, test.Scene.Foreground);
        Assert.Equal(1, test.Scene.Opacity); Assert.Equal(1, test.Frame.Opacity);
        test.Binding.Synchronize(true); test.Advance(160);
        Assert.Equal(Accent, test.Frame.BorderColor); Assert.False(test.Ui.NeedsUpdate);
    }

    [Fact]
    public void ReversalDetachAndOptOut_FinishWithoutRetainingFramesOrChangingOwners()
    {
        using var test = new Fixture(); test.Ui.Update();
        test.Binding.Synchronize(false); test.Advance(80);
        Assert.Equal(new Color(80, 120, 160), test.Frame.BorderColor);
        test.Binding.Synchronize(true); test.Advance(80);
        Assert.Equal(new Color(90, 135, 180), test.Frame.BorderColor);
        test.Root.Items.Remove(test.Frame);
        Assert.Equal(Accent, test.Frame.BorderColor); Assert.False(test.Frame.NeedsFrame);
        Assert.False(test.Frame.IsDisposed); Assert.Same(test.Frame, test.Scene.Parent);
        test.Advance(160); Assert.False(test.Ui.NeedsUpdate);
        test.Root.Add(test.Frame); test.Ui.Update();
        test.Binding.Synchronize(false); test.Advance(80);
        test.Frame.UseSystemAccentBorder = false; test.Ui.Update();
        Assert.Equal(Theme.Dark.Border, test.Frame.BorderColor); Assert.False(test.Frame.NeedsFrame);
        Assert.False(test.Ui.NeedsUpdate);
        test.Frame.UseSystemAccentBorder = true; test.Ui.Update();
        Assert.Equal(Dim(Accent), test.Frame.BorderColor); Assert.False(test.Frame.NeedsFrame);
        Assert.Same(test.Frame, test.Scene.Parent); Assert.True(test.Scene.IsAttached);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitLocalAndStyleColors_TakePriorityAndRemovingThemRestoresCurrentSystemState(bool light)
    {
        using var test = new Fixture(); test.Ui.Update(); test.Binding.Synchronize(false); test.Advance(160);
        var theme = light ? Theme.Light : Theme.Dark; test.Ui.SetTheme(theme); test.Ui.Update();
        test.Frame.Style = new Style().With(UiProperties.BorderColor, new Color(20, 30, 40));
        Assert.Equal(new Color(20, 30, 40), test.Frame.BorderColor);
        test.Frame.BorderColor = new(230, 210, 190); test.Host.AccentColor = new(80, 100, 120);
        test.Binding.Synchronize(false); test.Ui.Update();
        Assert.Equal(new Color(230, 210, 190), test.Frame.BorderColor);
        test.Frame.Clear(UiProperties.BorderColor);
        Assert.Equal(new Color(20, 30, 40), test.Frame.BorderColor);
        test.Frame.Style = null;
        Assert.Equal(new Color(48, 60, 72), test.Frame.BorderColor);
        test.Host.AccentColor = null; test.Binding.Synchronize(false); test.Ui.Update();
        Assert.Equal(Dim(theme.Border), test.Frame.BorderColor);
        Assert.Equal(1, test.Frame.BorderWidth); Assert.False(test.Ui.NeedsUpdate);
    }

    [Fact]
    public void OptOutAndRepeatedSynchronize_KeepOrdinaryFrameAppearanceAndSettledUiIdle()
    {
        using var test = new Fixture(); test.Ui.Update();
        test.Frame.UseSystemAccentBorder = false; test.Binding.Synchronize(false); test.Ui.Update();
        Assert.Equal(Theme.Dark.Border, test.Frame.BorderColor); Assert.False(test.Frame.NeedsFrame);
        for (var i = 0; i < 4; i++) { test.Binding.Synchronize(false); Assert.False(test.Ui.Update()); }
        test.Frame.UseSystemAccentBorder = true; test.Ui.Update();
        Assert.Equal(Dim(Accent), test.Frame.BorderColor);
        for (var i = 0; i < 4; i++)
        {
            test.Binding.Synchronize(false); Assert.False(test.Frame.NeedsFrame);
            Assert.False(test.Ui.NeedsUpdate); Assert.False(test.Ui.Update());
        }
    }

    private static Color Dim(Color color) => new((byte)MathF.Round(color.R * .6f), (byte)MathF.Round(color.G * .6f), (byte)MathF.Round(color.B * .6f), color.A);
    private sealed class Fixture : IDisposable
    {
        public ManualTime Time { get; } = new();
        public ColorsHost Host { get; } = new() { AccentColor = Accent };
        public Panel Root { get; } = new();
        public TextBox Scene { get; } = new() { Value = "unchanged" };
        public WindowFrame Frame { get; }
        public DefaultWindowFrameBinding Binding { get; }
        public UiSession Ui { get; }
        public Fixture()
        {
            Frame = WindowFrame.CreateDefault("window", Scene); Frame.UseSystemAccentBorder = true;
            Frame.TitleBarOverContent = true; Root.Add(Frame);
            Binding = new(Frame, Host); Binding.Synchronize(true);
            Ui = new(Root, new Surface(), new(400, 200), null, new UiDispatchOptions(), Time);
        }
        public void Advance(int milliseconds) { Time.Advance(milliseconds); Ui.Update(); }
        public void Dispose() => Ui.Dispose();
    }
    private class Commands : IWindowCommands
    {
        public UiViewport Viewport => new(400, 200);
        public UiWindowState State => UiWindowState.Normal;
        public event Action? RenderRequested { add { } remove { } }
        public event Action<UiInput>? Input { add { } remove { } }
        public void Run() { } public void Close() { } public void Minimize() { } public void Maximize() { } public void Restore() { } public void Dispose() { }
    }
    private sealed class ColorsHost : Commands, IWindowSystemColors { public Color? AccentColor { get; set; } }
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(int milliseconds) => _ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { }
    }
}
