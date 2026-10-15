using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using TrueMoon;
using TrueMoon.Diagnostics;

namespace EneritBaseline;

internal readonly record struct Snapshot(long AllocatedBytes, int Gen0, int Gen1, int Gen2, long Calls, int Errors)
{
    internal static Snapshot Capture(long calls = 0, int errors = 0) => new(GC.GetTotalAllocatedBytes(precise: true),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), calls, errors);

    internal Snapshot Subtract(Snapshot before) => new(AllocatedBytes - before.AllocatedBytes,
        Gen0 - before.Gen0, Gen1 - before.Gen1, Gen2 - before.Gen2, Calls - before.Calls, Errors - before.Errors);
}

internal sealed record Case(string Shape, int Bytes, int Concurrency)
{
    internal static readonly Case[] All = (from shape in new[] { "scalar", "bytes-256", "bytes-16384", "bytes-65536", "dto-1024" }
        from concurrency in new[] { 1, 8 }
        select Create(shape, concurrency)).ToArray();

    internal static Case Create(string shape, int concurrency)
    {
        if (concurrency is not (1 or 8)) throw new ArgumentOutOfRangeException(nameof(concurrency));
        var bytes = shape switch { "scalar" => 0, "bytes-256" => 256, "bytes-16384" => 16384, "bytes-65536" => 65536, "dto-1024" => 1024,
            _ => throw new ArgumentException("Unknown shape.", nameof(shape)) };
        return new Case(shape, bytes, concurrency);
    }
}

internal sealed class Workload(Case descriptor, IBaselineService service)
{
    private readonly byte[] _input = Enumerable.Range(0, descriptor.Bytes).Select(index => (byte)((index * 31) % 251)).ToArray();
    private readonly BaselineDto _dto = new()
    {
        Id = 17, Text = "TrueMoon IPC Привет 🌙", Values = Enumerable.Range(0, 16).ToList(),
        Bytes = Enumerable.Range(0, 1024).Select(index => (byte)((index * 31) % 251)).ToArray()
    };

    internal Task ExecuteAsync(int operations, long[]? durations, CancellationToken token)
    {
        return Task.WhenAll(Enumerable.Range(0, descriptor.Concurrency).Select(Worker));

        async Task Worker(int worker)
        {
            for (var index = worker; index < operations; index += descriptor.Concurrency)
            {
                var start = Stopwatch.GetTimestamp();
                if (descriptor.Shape == "scalar")
                {
                    var result = await service.PingAsync(index + 101, token).ConfigureAwait(false);
                    if (durations is not null) durations[index] = Stopwatch.GetTimestamp() - start;
                    Check(result == index + 102, "Scalar result differs.");
                }
                else if (descriptor.Shape.StartsWith("bytes-", StringComparison.Ordinal))
                {
                    var result = await service.EchoAsync(_input, token).ConfigureAwait(false);
                    if (durations is not null) durations[index] = Stopwatch.GetTimestamp() - start;
                    try { Check(result.Span.SequenceEqual(_input), "Byte response differs."); }
                    finally { Check(result.TryReturn(), "Response bytes rental not returned."); }
                }
                else
                {
                    var result = await service.TransformAsync(_dto, token).ConfigureAwait(false);
                    if (durations is not null) durations[index] = Stopwatch.GetTimestamp() - start;
                    Check(result is not null, "DTO response is null.");
                    try
                    {
                        Check(result!.Id == 18 && result.Text == _dto.Text + "!", "DTO fields differ.");
                        Check(result.Values is { Count: 16 }, "DTO list count differs.");
                        for (var item = 0; item < 16; item++) Check(result.Values![item] == item + 1, "DTO list differs.");
                        Check(result.Bytes.Span.SequenceEqual(_input), "DTO bytes differ.");
                    }
                    finally { Check(result!.Bytes.TryReturn(), "DTO bytes rental not returned."); }
                }
            }
        }
    }

    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

internal sealed record Sample(int Index, int Operations, double Seconds, double CallsPerSecond,
    double P50Microseconds, double P95Microseconds, double P99Microseconds,
    Snapshot Client, Snapshot Server, long[] LatencyTicks);

internal static class Statistics
{
    internal static double Percentile(long[] sorted, double probability)
        => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * probability) - 1, 0, sorted.Length - 1)] * 1_000_000d / Stopwatch.Frequency;

    internal static object EnvironmentInfo() => new
    {
        Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
        RuntimeInformation.ProcessArchitecture, Environment.ProcessorCount,
        GCSettings.IsServerGC, GCLatencyMode = GCSettings.LatencyMode.ToString(), Stopwatch.Frequency,
        Configuration = typeof(Statistics).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
            .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration,
        TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
        ServerGCOverride = Environment.GetEnvironmentVariable("DOTNET_gcServer")
    };
}

internal sealed class ProbeEvents<T> : IEventsSource<T>
{
    internal int Connections, Errors;
    public string Name => typeof(T).Name;
    public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
    public void StopActivity(Activity activity) => activity.Stop();
    public void Write(Func<object>? func, string? category = null, string? caller = null)
    {
        var value = func?.Invoke();
        if (value is "Connected") Interlocked.Increment(ref Connections);
        if (value is Exception) Interlocked.Increment(ref Errors);
    }
}
