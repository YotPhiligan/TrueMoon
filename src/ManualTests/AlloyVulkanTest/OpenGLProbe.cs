using Silk.NET.OpenGL;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

internal static class OpenGLProbe
{
    internal static OpenGLUiTarget Target(SilkWindowHost window)
    {
        var context = window.NativeWindow.GLContext ?? throw new NotSupportedException("Missing host GL context.");
        return new OpenGLUiTarget(name => context.TryGetProcAddress(name, out var address) ? address : 0, context.MakeCurrent);
    }
    internal static List<RasterProbe.Frame> RenderFrames()
    {
        using var window = new SilkWindowHost(320, 180, "Alloy OpenGL comparison", true);
        using var gl = GL.GetApi(window.NativeWindow);
        var frames = RasterProbe.RenderFrames(new SkiaOpenGLRenderBackend(), Target(window), ui =>
        {
            using var image = ui.ReadbackOpenGLImage();
            Require(!image.IsTextureBacked, "Diagnostic readback must be an independent CPU image.");
            var pixels = RasterProbe.ReadSnapshot(image);
            VerifyComposition(gl, ui, pixels);
            return pixels;
        });
        Require(gl.GetError() == GLEnum.NoError, "OpenGL comparison/teardown generated a GL error.");
        return frames;
    }
    internal static void Run()
    {
        using var window = new SilkWindowHost(320, 180, "Alloy borrowed OpenGL host", true);
        using var gl = GL.GetApi(window.NativeWindow);
        Console.WriteLine($"OpenGL host: {gl.GetStringS(StringName.Version)} / {gl.GetStringS(StringName.Renderer)}");
        var target = Target(window);
        var backend = new SkiaOpenGLRenderBackend();
        Reject<ArgumentException>(() => backend.CreateSurface(new RasterUiTarget(), new UiViewport(32, 32)));
        Reject<ArgumentOutOfRangeException>(() => backend.CreateSurface(target, new UiViewport(-1, 32)));
        Reject<InvalidOperationException>(() => backend.CreateSurface(new OpenGLUiTarget(_ => 0, () => throw new InvalidOperationException("Host unavailable")), new UiViewport(32, 32)));
        Reject<NotSupportedException>(() => backend.CreateSurface(new OpenGLUiTarget(_ => 0, () => { }), new UiViewport(32, 32)));
        Reject<InvalidOperationException>(() => backend.CreateSurface(new OpenGLUiTarget(_ => throw new InvalidOperationException("Loader unavailable"), () => { }), new UiViewport(32, 32)));
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UseAlloy(options => options.UseSkiaOpenGL().UseExternalHost()));
        using var app = builder.Build(); app.StartAsync().GetAwaiter().GetResult();
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
        Require(app.Services.GetService(typeof(HostedUiWindow)) == null, "External OpenGL hosting must not allocate a window.");
        var button = new Button("Borrowed OpenGL HUD");
        var ui = factory.Create(button, target, new UiViewport(320, 180));
        Require(ui.Rendering is SkiaOpenGLSurface, "Hosting must select the OpenGL renderer.");
        Reject<InvalidOperationException>(() => ui.ReadbackOpenGLImage().Dispose());
        Reject<InvalidOperationException>(() => ui.PresentOpenGL());
        Require(ui.Update() && !ui.Update(), "OpenGL must respect retained invalidation.");
        using var snapshot = ui.ReadbackOpenGLImage();
        var savedPixels = RasterProbe.ReadSnapshot(snapshot);
        var clicks = 0; button.Click += () => { clicks++; button.Value = "Updated HUD"; };
        Require(ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)).PointerCaptured, "Expected pointer capture.");
        Require(ui.HandleInput(new UiInput(InputKind.PointerUp, 10, 10)).Handled && clicks == 1, "Expected retained button activation.");
        Require(ui.Update(), "Button activation must invalidate OpenGL UI.");
        var surface = (SkiaOpenGLSurface)ui.Rendering;
        Reject<ArgumentException>(() => surface.Render(button, new UiViewport(16, 16)));
        Exception? foreignError = null;
        var thread = new Thread(() => { try { surface.ReadbackImage().Dispose(); } catch (Exception error) { foreignError = error; } });
        thread.Start(); thread.Join(); Require(foreignError is InvalidOperationException, "Foreign-thread access must fail before native GL calls.");
        Task.Run(() => ui.Post(() => button.Value = "Posted HUD")).GetAwaiter().GetResult();
        Require(ui.Update(), "Posted changes must update on the creator thread.");
        ui.Resize(new UiViewport(0, 0)); Require(!ui.Update(), "Zero viewport must suspend rendering.");
        Reject<InvalidOperationException>(() => surface.ReadbackImage().Dispose());
        ui.Resize(new UiViewport(160, 90, 2)); Require(ui.Update(), "Restore must recreate the GPU surface.");
        ui.PresentOpenGL(); window.NativeWindow.GLContext!.SwapBuffers();
        app.StopAsync().GetAwaiter().GetResult();
        Require(ui.IsDisposed && button.IsDisposed && factory.ActiveSessionCount == 0, "App stop must release UI and registry.");
        Require(RasterProbe.ReadSnapshot(snapshot).SequenceEqual(savedPixels), "CPU readback must remain unchanged across redraw/resize/disposal.");
        using (var next = UiSession.Create(new Panel(), backend, target, new UiViewport(32, 32)))
            Require(next.Update(), "Disposing a UI session must preserve the borrowed host context.");
        var failing = new RetryView();
        using (var retry = UiSession.Create(failing, backend, target, new UiViewport(32, 32)))
        {
            failing.DuringDraw = () =>
            {
                Reject<InvalidOperationException>(() => ((SkiaOpenGLSurface)retry.Rendering).ReadbackImage().Dispose());
                Reject<InvalidOperationException>(() => retry.Resize(new UiViewport(40, 40)));
            };
            Reject<InvalidOperationException>(() => retry.Update());
            Reject<InvalidOperationException>(() => retry.ReadbackOpenGLImage().Dispose());
            failing.Fail = false; Require(retry.Update() && !retry.Update(), "Failed drawing must remain dirty and retry cleanly.");
        }
        Require(gl.GetError() == GLEnum.NoError, "OpenGL lifecycle generated a GL error.");
        Require(RenderFrames().Count == 3, "Expected three shared-view OpenGL frames.");
        Console.WriteLine("OpenGL passed: App/DI, retained input/redraw, thread/loader/target/output/reentrancy guards, draw failure/retry, suspend/restore, independent readback, borrowed context reuse; 3 shared-view GPU compositions with premultiplied alpha and host FBO ownership.");
    }
    internal static async Task RunFailureAsync()
    {
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UsePresentation<OpenGLFailingView>(options => options.UseSkiaOpenGL().UseSilkWindow()));
        await using var app = builder.Build();
        var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await app.StartAsync(timeout.Token);
            await window.Completion.WaitAsync(timeout.Token);
            throw new InvalidOperationException("Expected the draw failure to reach the host.");
        }
        catch (InvalidOperationException error) when (error.Message == "OpenGL draw failure probe") { }
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
        Require(factory.ActiveSessionCount == 0, "Failed window must release its session on the UI thread.");
        // Completion explicitly carries the rendering failure; stop remains faulted and must not silently succeed.
        try { await window.StopAsync(timeout.Token); }
        catch (InvalidOperationException error) when (error.Message == "OpenGL draw failure probe") { }
        Console.WriteLine("OpenGL failure propagation passed: draw error reaches Completion/StopAsync, session registry empty and tree released on its UI thread.");
        // DisposeAsync repeats StopAsync, so App disposal would intentionally propagate the same failure.
        try { await app.DisposeAsync(); }
        catch (InvalidOperationException error) when (error.Message == "OpenGL draw failure probe") { }
    }
    private sealed class RetryView : Panel
    {
        internal bool Fail = true;
        internal Action? DuringDraw;
        protected override void DrawCore(IDrawingContext context)
        { DuringDraw?.Invoke(); if (Fail) throw new InvalidOperationException("Retry drawing"); }
    }
    private static unsafe void VerifyComposition(GL gl, UiSession ui, byte[] source)
    {
        var width = ui.Viewport.Width; var height = ui.Viewport.Height;
        var texture = gl.GenTexture(); var framebuffer = gl.GenFramebuffer();
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
            Require(gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete, "Host framebuffer incomplete.");
            gl.Disable(EnableCap.ScissorTest); gl.Disable(EnableCap.FramebufferSrgb);
            gl.ColorMask(true, true, true, true); gl.ClearColor(20 / 255f, 40 / 255f, 60 / 255f, 1);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            ui.PresentOpenGL(framebuffer, stencilBits: 0, clear: false);
            // Assertions only: window/HUD composition above has no CPU transfer.
            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, framebuffer);
            var destination = new byte[width * height * 4];
            fixed (byte* pixels = destination) gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            var panel = ui.Root.Children[0]; var button = panel.Children[0].Children[2];
            foreach (var point in new[] { (width - 1, height - 1),
                ((int)((panel.Bounds.X + 4) * ui.Viewport.Scale), (int)((panel.Bounds.Y + 4) * ui.Viewport.Scale)),
                ((int)((button.Bounds.X + button.Bounds.Width - 8) * ui.Viewport.Scale), (int)((button.Bounds.Y + button.Bounds.Height / 2) * ui.Viewport.Scale)) })
            {
                var src = (point.Item2 * width + point.Item1) * 4;
                var dst = ((height - point.Item2 - 1) * width + point.Item1) * 4; // GL readback has bottom-left origin.
                var alpha = source[src + 3] / 255f;
                for (var c = 0; c < 3; c++)
                    Require(Math.Abs(destination[dst + c] - (source[src + c] + new[] { 20, 40, 60 }[c] * (1 - alpha))) <= 1,
                        "OpenGL GPU composition must preserve scene and blend premultiplied UI.");
                Require(destination[dst + 3] == 255, "The composed scene must remain opaque.");
            }
            Require(gl.IsFramebuffer(framebuffer) && gl.IsTexture(texture), "UI must not delete host framebuffer/texture.");
            Require(gl.GetError() == GLEnum.NoError, "OpenGL GPU composition generated a GL error.");
        }
        finally { gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0); gl.DeleteFramebuffer(framebuffer); gl.DeleteTexture(texture); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}

/// <summary>Exercises diagnostic propagation from a failing standalone UI draw.</summary>
public sealed class OpenGLFailingView : Panel
{
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => throw new InvalidOperationException("OpenGL draw failure probe");
}
