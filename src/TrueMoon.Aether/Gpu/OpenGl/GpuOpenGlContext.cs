namespace TrueMoon.Aether.Gpu.OpenGl;

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
    internal nint Require(string name) => _resolve(name) is var address && address != 0 ? address : throw new NotSupportedException("Missing GL import entry point " + name);
}
