using System.Diagnostics;
using TrueMoon;

namespace EneritPipesProbe;

public interface IProbeService
{
    Task<Payload?> TransformAsync(Payload? value, CancellationToken cancellationToken = default);
    Task<string?> EchoAsync(string? value, CancellationToken cancellationToken = default);
    Task<List<int>?> ReverseAsync(List<int>? value, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>?> MapAsync(Dictionary<string, int>? value, CancellationToken cancellationToken = default);
    Task<int?> IncrementAsync(int? value, CancellationToken cancellationToken = default);
    Task<Memory<byte>> BytesAsync(Memory<byte> value, CancellationToken cancellationToken = default);
    Task<int> HoldAsync(CancellationToken cancellationToken = default);
    Task AckAsync(bool fail, CancellationToken cancellationToken = default);
    void AckSync(bool fail);
    Task<int> PingAsync(int value, CancellationToken cancellationToken = default);
    Task<string> TraceAsync(CancellationToken cancellationToken = default);
}

public class Payload
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public List<int?>? Values { get; set; }
    public Dictionary<string, int>? Tags { get; set; }
    public Memory<byte> Bytes { get; set; }
}

internal static class GeneratedContract
{
    // Trigger the real source generator without adding App/DI composition to this transport proof.
    internal static void Register() => UseInvocationService<IProbeService>();
    private static void UseInvocationService<T>() { }
}

internal sealed class ProbeService : IProbeService
{
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<Payload?> TransformAsync(Payload? value, CancellationToken cancellationToken = default)
    {
        if (value is null) return null;
        try
        {
            await Task.Delay(value.Id % 3, cancellationToken);
            return new Payload
            {
                Id = value.Id + 100,
                Text = value.Text is null ? null : value.Text + " 🌙",
                Values = value.Values?.Select(item => item is null ? null : (int?)(item + 1)).Reverse().ToList(),
                Tags = value.Tags?.ToDictionary(pair => pair.Key, pair => pair.Value + 1),
                Bytes = value.Bytes.ToArray().Reverse().ToArray()
            };
        }
        finally { value.Bytes.TryReturn(); }
    }
    public Task<string?> EchoAsync(string? value, CancellationToken cancellationToken = default) => Task.FromResult(value);
    public Task<List<int>?> ReverseAsync(List<int>? value, CancellationToken cancellationToken = default) => Task.FromResult(value?.AsEnumerable().Reverse().ToList());
    public Task<Dictionary<string, int>?> MapAsync(Dictionary<string, int>? value, CancellationToken cancellationToken = default)
        => Task.FromResult(value?.ToDictionary(pair => pair.Key, pair => pair.Value + 1));
    public Task<int?> IncrementAsync(int? value, CancellationToken cancellationToken = default) => Task.FromResult(value + 1);
    public Task<Memory<byte>> BytesAsync(Memory<byte> value, CancellationToken cancellationToken = default)
    {
        try { return Task.FromResult<Memory<byte>>(value.ToArray()); }
        finally { value.TryReturn(); }
    }
    public async Task<int> HoldAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("WAITING");
        await Release.Task.WaitAsync(cancellationToken);
        return 73;
    }
    public Task AckAsync(bool fail, CancellationToken cancellationToken = default)
    {
        if (fail) throw new InvalidOperationException("remote-probe-failure");
        return Task.CompletedTask;
    }
    public void AckSync(bool fail)
    {
        if (fail) throw new InvalidOperationException("remote-sync-failure");
    }
    public Task<int> PingAsync(int value, CancellationToken cancellationToken = default) => Task.FromResult(value + 1);
    public async Task<string> TraceAsync(CancellationToken cancellationToken = default)
    {
        var before = Activity.Current;
        await Task.Yield();
        var activity = Activity.Current ?? throw new InvalidOperationException("Missing server activity.");
        if (!ReferenceEquals(before, activity)) throw new InvalidOperationException("Server activity did not survive await.");
        return string.Join('|', activity.TraceId.ToHexString(), activity.SpanId.ToHexString(), activity.ParentSpanId.ToHexString(),
            activity.Kind, activity.HasRemoteParent, (int)activity.ActivityTraceFlags, Environment.ProcessId,
            activity.TraceStateString ?? "-", activity.Baggage.Count());
    }
}
