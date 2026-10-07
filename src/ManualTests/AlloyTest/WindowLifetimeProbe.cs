using System.Diagnostics;
using TrueMoon.Alloy.Platform.Silk;

namespace AlloyTest;

internal static class WindowLifetimeProbe
{
    internal static void Run()
    {
        foreach (var openGL in new[] { true, false })
        {
            Verify(openGL, closeBeforeRun: false, failRender: false);
            Verify(openGL, closeBeforeRun: true, failRender: false);
            Verify(openGL, closeBeforeRun: false, failRender: true);
        }
        Console.WriteLine("Silk window lifetime passed: 6 OpenGL/Vulkan scenarios; live handle through Run/Closing/caught render failure, input before window teardown and repeated Dispose verified.");
    }

    private static void Verify(bool openGL, bool closeBeforeRun, bool failRender)
    {
        using var host = new SilkWindowHost(320, 240, "Window lifetime probe", openGL);
        var handle = host.NativeWindow.Handle;
        Require(handle != 0, "Window was not initialized.");
        var frames = 0;
        var closing = 0;
        nint closingHandle = 0;
        var deadline = Stopwatch.StartNew();
        var failure = new InvalidOperationException("Expected render failure.");
        Exception? callbackFailure = null;
        host.NativeWindow.Closing += () =>
        {
            closingHandle = host.NativeWindow.Handle;
            closing++;
        };
        host.NativeWindow.Update += _ =>
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(10))
            {
                callbackFailure = new TimeoutException("Window did not finish rendering.");
                host.Close();
            }
        };
        host.RenderRequested += () =>
        {
            // Match Hosting's boundary: never unwind a managed exception through GLFW.
            try
            {
                if (failRender) throw failure;
                if (++frames == 3) host.Close();
            }
            catch (Exception error)
            {
                callbackFailure = error;
                host.Close();
            }
        };

        if (closeBeforeRun) host.Close();
        host.Run();

        if (!failRender && callbackFailure != null) throw callbackFailure;
        Require(!failRender || ReferenceEquals(callbackFailure, failure), "Render failure was not captured.");
        Require(host.NativeWindow.IsInitialized && host.NativeWindow.Handle == handle,
            "Run destroyed the window before its input context could be released.");
        Require(closing == 1, "Closing was not raised exactly once.");
        Require(closingHandle == handle, "Closing ran after destroying the native window.");
        Require(closeBeforeRun || failRender || frames == 3, "The render loop did not reach its close request.");
        _ = host.GetText();
        host.Dispose();
        Require(!host.NativeWindow.IsInitialized && host.NativeWindow.Handle == 0, "Dispose did not destroy the window.");
        host.Dispose();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
