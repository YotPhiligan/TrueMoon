using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

/// <summary>Diagnostic shared owner loop with two live GL windows and replacement while a sibling continues.</summary>
internal static partial class WindowSharedLoopProbe
{
    internal static void Run(string[] args)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Shared-loop control requires Windows.");
        var index = Array.IndexOf(args, "--soak-output");
        if (index >= 0 && (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException("--soak-output requires a path.");
        var path = Path.GetFullPath(index >= 0 ? args[index + 1] : "TestResults/AlloyNativeThreads/shared-loop.json");
        var released = new List<WeakReference>();
        var results = new List<object>();
        var samples = new List<object>();
        uint? warmUser = null, warmGdi = null;
        using var process = Process.GetCurrentProcess();
        using var factory = new HostedUiSessionFactory(new SkiaOpenGLRenderBackend());
        var owner = NativeWindowDiagnostics.ThreadId;
        Exception? failure = null;
        try
        {
            for (var round = 0; round < 20; round++)
            {
                results.Add(RunRound(factory, released, owner));
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Require(released.All(reference => !reference.IsAlive), "Shared-loop UI graph retained.");
                Require(factory.ActiveSessionCount == 0 && Win32WindowDpi.ActiveHooks == 0
                    && Win32WindowChrome.ActiveHooks == 0 && Win32TransparentFramebuffer.ActiveHooks == 0, "Shared-loop cleanup retained registry/hooks.");
                process.Refresh();
                var user = GuiCount(process.Handle, 1); var gdi = GuiCount(process.Handle, 0);
                samples.Add(new
                {
                    Round = round + 1,
                    User = user,
                    Gdi = gdi,
                    Handles = process.HandleCount,
                    PrivateBytes = process.PrivateMemorySize64,
                    RetainedReferences = released.Count(reference => reference.IsAlive)
                });
                if (round == 3) { warmUser = user; warmGdi = gdi; }
                if (warmUser != null) Require(user <= warmUser + 2 && gdi <= warmGdi + 4, "Shared-loop USER/GDI exceeded warm +2/+4.");
                Console.WriteLine($"Shared loop {round + 1}/20: three HWNDs, sibling continued; USER/GDI {user}/{gdi}; registry/hooks/roots 0.");
            }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    Utc = DateTime.UtcNow,
                    Completed = failure == null,
                    Failure = failure?.ToString(),
                    OwnerThread = owner,
                    RequestedRounds = 20,
                    Results = results,
                    Samples = samples,
                    TrackedReferences = released.Count,
                    RetainedReferences = released.Count(reference => reference.IsAlive),
                    Scope = "Manual Windows/OpenGL owner loop: 20 rounds, two concurrently live windows, close/recreate first while sibling renders, then dispose all. Three resize operations per HWND; text/selection, owner thread/context, registry/hooks/WeakReferences checked. USER/GDI warm round4 +2/+4. No HostedUiWindow scheduler, Vulkan, alpha/chrome/minimize, callback-failure/cancellation matrix, cross-platform proof or VRAM profiling; not full K7."
                },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error) when (failure != null) { Console.Error.WriteLine($"Shared-loop report failed: {error}"); }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object RunRound(HostedUiSessionFactory factory, List<WeakReference> released, uint owner)
    {
        LiveWindow? first = null, sibling = null, replacement = null;
        Exception? failure = null;
        var siblingFramesAtClose = 0;
        try
        {
            first = new LiveWindow(factory, "Shared loop first", 12, owner);
            sibling = new LiveWindow(factory, "Shared loop sibling", 36, owner);
            Require(factory.ActiveSessionCount == 2 && first.Handle != sibling.Handle && Win32WindowDpi.ActiveHooks == 2,
                "Two independent windows/sessions/hooks were not active.");
            var timeout = Stopwatch.StartNew();
            while (!sibling.Disposed || replacement is { Disposed: false })
            {
                Require(timeout.Elapsed < TimeSpan.FromSeconds(30), "Shared loop timed out.");
                if (!first.Disposed)
                {
                    first.Pump();
                    if (first.ReadyToClose)
                    {
                        first.Dispose(); Track(first, released);
                        siblingFramesAtClose = sibling.Frames;
                        Require(!sibling.Disposed && factory.ActiveSessionCount == 1 && Win32WindowDpi.ActiveHooks == 1,
                            "Closing first affected sibling.");
                        replacement = new LiveWindow(factory, "Shared loop replacement", 12, owner);
                        Require(factory.ActiveSessionCount == 2 && Win32WindowDpi.ActiveHooks == 2,
                            "Replacement could not start alongside sibling.");
                    }
                }
                if (!sibling.Disposed)
                {
                    sibling.Pump();
                    if (sibling.ReadyToClose) { sibling.Dispose(); Track(sibling, released); }
                }
                if (replacement is { Disposed: false })
                {
                    replacement.Pump();
                    if (replacement.ReadyToClose) { replacement.Dispose(); Track(replacement, released); }
                }
            }
            Require(first.Disposed && replacement is { Disposed: true }
                && sibling.Frames - siblingFramesAtClose >= 12, "Sibling did not continue after first teardown.");
            return new
            {
                FirstFrames = first.Frames,
                SiblingFrames = sibling.Frames,
                ReplacementFrames = replacement!.Frames,
                SiblingFramesAtFirstClose = siblingFramesAtClose,
                Resizes = first.Resizes + sibling.Resizes + replacement.Resizes
            };
        }
        catch (Exception error) { failure = error; throw; }
        finally { UiCleanup.Complete(failure, () => first?.Dispose(), () => sibling?.Dispose(), () => replacement?.Dispose()); }
    }

    private static void Track(LiveWindow window, List<WeakReference> released)
    {
        released.Add(new WeakReference(window)); released.Add(new WeakReference(window.Host));
        released.Add(new WeakReference(window.Session)); released.Add(new WeakReference(window.Session.Root));
        released.Add(new WeakReference(window.Editor));
    }

    private sealed class LiveWindow : IDisposable
    {
        internal readonly SilkWindowHost Host;
        internal readonly UiSession Session;
        internal readonly TextBox Editor;
        internal readonly nint Handle;
        private readonly uint _owner;
        private readonly int _frames;
        internal int Frames, Resizes;
        internal bool Disposed;
        internal bool ReadyToClose => Frames >= _frames;
        internal LiveWindow(HostedUiSessionFactory factory, string title, int frames, uint owner)
        {
            _owner = owner; _frames = frames;
            Host = new SilkWindowHost(320, 200, title, true);
            Editor = new TextBox { Value = "Ирина 🧑‍💻", Width = 220, Height = 36 };
            var root = new Panel().WithChildren(Editor);
            try { Session = factory.Create(root, OpenGLProbe.Target(Host), Host.Viewport, Host); }
            catch (Exception error) { UiCleanup.Complete(error, root.Dispose, Host.Dispose); throw; }
            Handle = Host.NativeWindow.Native!.Win32!.Value.Hwnd;
            Host.RenderRequested += Render;
            Host.Input += Input;
            Session.Focus(Editor);
            Session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
            Session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        }
        private void Input(UiInput input)
        {
            if (Disposed) return;
            Current(); Synchronize(); Session.HandleInput(input);
        }
        private void Current()
        {
            Require(_owner == NativeWindowDiagnostics.ThreadId, "Shared-loop owner thread changed.");
            Host.NativeWindow.GLContext!.MakeCurrent();
            Host.VerifyWindowAccess();
        }
        private void Synchronize()
        {
            if (Session.Viewport != Host.Viewport) Session.Resize(Host.Viewport);
            Session.Update();
        }
        private void Render()
        {
            if (Disposed || ReadyToClose) return;
            Current(); Synchronize(); Session.PresentOpenGL(); Host.NativeWindow.GLContext!.SwapBuffers();
            Frames++;
            if (Frames == 3) { Host.Resize(480, 300); Resizes++; }
            if (Frames == 6) { Host.Resize(360, 260); Resizes++; }
            if (Frames == 9) { Host.Resize(440, 280); Resizes++; }
            Require(Editor.Value == "Ирина 🧑‍💻" && Editor.SelectionLength > 0, "Shared-loop text/selection changed.");
        }
        internal void Pump()
        {
            Current(); Host.NativeWindow.DoEvents(); Host.VerifyWindowAccess();
            Host.DispatchPendingWindowInput(); Host.NativeWindow.DoUpdate(); Host.NativeWindow.DoRender();
        }
        public void Dispose()
        {
            if (Disposed) return;
            Current(); Disposed = true;
            Host.RenderRequested -= Render; Host.Input -= Input;
            // Session GPU resources are released while its context/HWND still exist.
            UiCleanup.Complete(null, Session.Dispose, Host.Close, Host.Dispose);
            Require(IsWindow(Handle) == 0 && Session.IsDisposed && Session.Root.IsDisposed, "Shared-loop HWND/session/tree survived teardown.");
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static uint GuiCount(nint process, uint flag)
    {
        var value = GetGuiResources(process, flag);
        if (value == 0 && Marshal.GetLastPInvokeError() is var error && error != 0) throw new Win32Exception(error);
        return value;
    }
    [LibraryImport("user32.dll", SetLastError = true)] private static partial uint GetGuiResources(nint process, uint flag);
    [LibraryImport("user32.dll")] private static partial int IsWindow(nint window);
}
