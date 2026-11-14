namespace TrueMoon.Aether.Runtime.Gpu.OpenGl;

/// <summary>Borrowed current GL context and asynchronous owner dispatch. Keep it alive until Aether GPU cleanup succeeds.</summary>
public sealed class GpuOpenGlContext
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly Func<string, nint> _resolve;
    private readonly Action _verify;
    internal Func<Action, Task> Invoke { get; }
    /// <summary>Captures the current owner; callbacks do not transfer ownership of the window/context to Aether.</summary>
    public GpuOpenGlContext(Func<string, nint> resolve, Action verifyCurrent, Func<Action, Task> invokeOnOwner)
    { _resolve = resolve; _verify = verifyCurrent; Invoke = invokeOnOwner; Verify(); }
    internal void Verify()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("GPU import requires its GL owner thread.");
        _verify();
    }
    /// <summary>Reads the exact current Windows GL adapter identity, without creating a device or importing resources.</summary>
    /// <returns>One matching adapter UUID pair.</returns>
    public unsafe GpuAdapterId ReadAdapter()
    {
        Verify();
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException("The shared GPU profile requires Windows x64.");
        var integer = (delegate* unmanaged<uint, int*, void>)Require("glGetIntegerv");
        var bytes = (delegate* unmanaged<uint, byte*, void>)Require("glGetUnsignedBytevEXT");
        var indexed = (delegate* unmanaged<uint, uint, byte*, void>)Require("glGetUnsignedBytei_vEXT");
        var error = (delegate* unmanaged<uint>)Require("glGetError");
        if (error() != 0) throw new InvalidOperationException("OpenGL context has a pending error.");
        int count = 0; integer(0x9596, &count); if (count != 1) throw new NotSupportedException("One GL device UUID is required.");
        byte* driver = stackalloc byte[16]; byte* device = stackalloc byte[16]; bytes(0x9598, driver); indexed(0x9597, 0, device);
        if (error() != 0) throw new NotSupportedException("OpenGL device UUID query failed.");
        return new(Convert.ToHexString(new ReadOnlySpan<byte>(device, 16)), Convert.ToHexString(new ReadOnlySpan<byte>(driver, 16)));
    }
    internal nint Require(string name) => _resolve(name) is var address && address != 0 ? address : throw new NotSupportedException("Missing GL import entry point " + name);
}
