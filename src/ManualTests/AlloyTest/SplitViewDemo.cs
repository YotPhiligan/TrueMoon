using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using SkiaSharp;

namespace AlloyTest;

internal static class SplitViewDemo
{
    internal static async Task RunAsync(string[] args)
    {
        var smoke = args.Contains("--split-view-smoke"); var vulkan = args.Contains("--vulkan");
        var messages = 0;
        var builder = App.Builder(_ => { });
        builder.Setup(context =>
        {
            context.Services(services => services.Singleton<SplitViewDemoData>(_ => new())
                .Singleton<IViewStyle<SplitViewDemoView>>(_ => new SplitViewDemoStyle())
                .Singleton<IFactory<SplitViewDemoView>>(resolver => new Factory<SplitViewDemoView>(() =>
                    new(resolver.Resolve<SplitViewDemoData>(), resolver.Resolve<IViewStyle<SplitViewDemoView>>()))));
            context.UsePresentationFactory<SplitViewDemoView>(options =>
            {
                if (vulkan) options.UseSkiaVulkan(); else options.UseSkiaOpenGL();
                options.UseSilkWindow(window =>
                {
                    window.Width = 980; window.Height = 680; window.Title = "TrueMoon — SplitView";
                    window.FrameMode = UiFrameMode.OnDemand;
                    if (vulkan && args.Contains("--validation")) window.ValidationMessage = message =>
                    { Interlocked.Increment(ref messages); Console.Error.WriteLine(message); };
                });
            });
        });
        await using var app = builder.Build();
        var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
        var sessions = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        SplitViewDemoView? observed = null; var stages = 0;
        if (smoke) window.FramePresented += ui =>
        {
            observed = (SplitViewDemoView)ui.Root;
            if (stages++ == 0)
            {
                Verify(ui, observed);
                ui.RequestFrame();
            }
            else
            {
                if (!vulkan)
                {
                    var output = Path.GetFullPath("TestResults/SplitView/native.png"); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    using var image = ui.ReadbackOpenGLImage(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = File.Create(output); data.SaveTo(stream);
                }
                window.Close();
            }
        };
        using var deadline = new CancellationTokenSource(smoke ? TimeSpan.FromSeconds(30) : Timeout.InfiniteTimeSpan);
        await app.StartAsync(deadline.Token);
        await window.Completion.WaitAsync(deadline.Token);
        await app.StopAsync(deadline.Token);
        if (smoke)
        {
            Check(stages >= 2 && observed is { IsDisposed: true, IsAttached: false }, "Native tree was not presented/disposed.");
            Check(sessions.ActiveSessionCount == 0 && messages == 0, "Session or validation messages remained.");
            Console.WriteLine($"SplitView {(vulkan ? "Vulkan" : "OpenGL")} smoke passed: nested axes, constraints, capture/release/Esc, keyboard, draft/selection/theme, idle; sessions=0, validation={messages}. Routed input; physical input/DPI not claimed.");
        }
    }

    private static void Verify(UiSession ui, SplitViewDemoView view)
    {
        var split = view.Columns; var original = split.FirstPaneSize;
        ui.Focus(view.Editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var divider = split.Divider.Bounds;
        Check(ui.HandleInput(new(InputKind.PointerDown, X: divider.X + 2, Y: divider.Y + 30)).PointerCaptured, "Pointer capture not acquired.");
        ui.HandleInput(new(InputKind.PointerMove, X: divider.X + 62, Y: -100)); ui.Update();
        Check(split.ActualFirstPaneSize == original + 60, "Pointer delta did not resize pane.");
        Check(!ui.HandleInput(new(InputKind.PointerUp, X: divider.X + 62, Y: -100)).PointerCaptured, "Capture survived outside release.");
        var rows = view.Rows; var before = rows.FirstPaneSize; divider = rows.Divider.Bounds;
        ui.HandleInput(new(InputKind.PointerDown, X: divider.X + 30, Y: divider.Y + 2));
        ui.HandleInput(new(InputKind.PointerMove, X: divider.X + 30, Y: divider.Y + 42)); ui.Update();
        Check(rows.IsResizing && rows.FirstPaneSize == before + 40 && !split.IsResizing, "Nested divider routed to wrong owner.");
        Check(!ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)).PointerCaptured && rows.FirstPaneSize == before, "Escape did not roll back.");
        ui.Focus(split.Divider); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Right));
        Check(split.FirstPaneSize == original + 68, "Keyboard resize failed.");
        ui.Focus(view.Editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.SetTheme(Theme.Light); ui.Update();
        Check(view.Editor.Value == view.DataContext!.Draft && view.Editor.IsFocused && view.Editor.SelectionLength == view.Editor.Value.Length, "Theme/resize lost retained draft or focus.");
        Check(!ui.Update(), "Unchanged split tree did not become idle.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

/// <summary>Data borrowed by the split view demonstration; independent of controls and renderer.</summary>
public sealed class SplitViewDemoData
{
    /// <summary>The initial unsaved text retained while resizing.</summary>
    public string Draft { get; } = "Черновик сохраняется при изменении панелей";
}

/// <summary>Nested two-pane layouts composed once, with a separately supplied style.</summary>
public sealed class SplitViewDemoView : View<SplitViewDemoData>
{
    /// <summary>Horizontal navigation/workspace division.</summary>
    public SplitView Columns { get; }
    /// <summary>Vertical workspace/timeline division.</summary>
    public SplitView Rows { get; }
    /// <summary>Retained draft field.</summary>
    public TextBox Editor { get; }
    /// <summary>Creates the owned tree on the UI owner thread.</summary>
    /// <param name="data">Borrowed application data.</param><param name="style">Borrowed appearance declaration.</param>
    public SplitViewDemoView(SplitViewDemoData data, IViewStyle<SplitViewDemoView> style)
    {
        ArgumentNullException.ThrowIfNull(data); ArgumentNullException.ThrowIfNull(style);
        DataContext = data; Editor = new TextBox { Value = data.Draft };
        Rows = new SplitView(VStack.Create().WithChildren(new Text("Рабочая область"), Editor,
            new Text("Tab → разделитель; стрелки / Shift; Home / End; Esc отменяет drag")), new Text("Таймлайн"))
        { Orientation = SplitOrientation.Vertical, FirstPaneSize = 380 };
        Rows.FirstPane.MinHeight = 120; Rows.SecondPane.MinHeight = 80;
        Columns = new SplitView(new Text("Навигация"), Rows) { FirstPaneSize = 220 };
        Columns.FirstPane.MinWidth = 140; Columns.FirstPane.MaxWidth = 400; Columns.SecondPane.MinWidth = 240;
        Columns.Divider.AccessibleName = "Ширина навигации"; Rows.Divider.AccessibleName = "Высота рабочей области";
        SetContent(Columns); this.WithViewStyle(style);
    }
}

/// <summary>The demonstration's theme-derived appearance, without retained view references.</summary>
public sealed class SplitViewDemoStyle : IViewStyle<SplitViewDemoView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<SplitViewDemoView> styles)
    {
        styles.Root().Background(theme => theme.Surface).Padding(12);
        styles.Part(view => view.Columns.FirstPane).Padding(12).Background(theme => theme.Control);
        styles.Part(view => view.Rows.FirstPane).Padding(12).Background(theme => theme.Control);
        styles.Part(view => view.Rows.SecondPane).Padding(12).Background(theme => theme.Control);
    }
}
