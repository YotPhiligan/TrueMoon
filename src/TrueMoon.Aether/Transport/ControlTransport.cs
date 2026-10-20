using System.Buffers;
using System.Diagnostics;
using TrueMoon.Aether.Contracts;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Aether.Transport;

public sealed class AetherEvents<T>(Action<object>? observe = null) : IEventsSource<T>
{
    public string Name => typeof(T).Name;
    public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new Activity(Name).Start();
    public void StopActivity(Activity activity) => activity.Stop();
    public void Write(Func<object>? func, string? category = null, string? caller = null)
    { if (func != null) observe?.Invoke(func()); }
}

// Enforce the actual generated Enerit payload size, not an estimate from JSON.
public sealed class BoundedControlClient(IInvocationClient<IEngineControl> inner) : IInvocationClient<IEngineControl>
{
    public const int PayloadLimit = 64 * 1024;
    public static void ValidateLength(int length)
    { if (length < 0 || length > PayloadLimit) throw new InvalidDataException("Engine control payload exceeds 64KiB."); }
    private static Action<IBufferWriter<byte>>? Bounded(Action<IBufferWriter<byte>>? write)
    {
        if (write == null) return null;
        var bytes = new ArrayBufferWriter<byte>(); write(bytes); ValidateLength(bytes.WrittenCount);
        return destination => destination.Write(bytes.WrittenSpan);
    }
    public Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action,
        Func<ReadOnlyMemory<byte>, TResult> func, CancellationToken cancellationToken = default)
        => inner.InvokeAsync(methodCode, Bounded(action), memory => { ValidateLength(memory.Length); return func(memory); }, cancellationToken);
    public Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
        => inner.InvokeAsync(methodCode, Bounded(action), cancellationToken);
    public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func)
        => InvokeAsync(methodCode, action, func).GetAwaiter().GetResult();
    public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action) => InvokeAsync(methodCode, action).GetAwaiter().GetResult();
}

public sealed class BoundedControlHandler(IInvocationServerHandler<IEngineControl> inner) : IInvocationServerHandler<IEngineControl>
{
    public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> bufferWriter,
        CancellationToken cancellationToken = default)
    {
        BoundedControlClient.ValidateLength(readMemory.Length);
        var response = new ArrayBufferWriter<byte>();
        var outcome = await inner.HandleAsync(method, readMemory, response, cancellationToken).ConfigureAwait(false);
        BoundedControlClient.ValidateLength(response.WrittenCount); bufferWriter.Write(response.WrittenSpan);
        return outcome;
    }
}

internal static class GeneratorDiscovery
{
    // Same discovery hook as EneritPipesProbe, without adding a transport-owned
    // client singleton to the long-lived Editor App (each generation owns its client).
    internal static void Register() => UseInvocationService<IEngineControl>();
    private static void UseInvocationService<T>() { }
}
