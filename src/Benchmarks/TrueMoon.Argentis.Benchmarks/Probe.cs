using System.Diagnostics;
using System.Text.Json;

namespace TrueMoon.Argentis.Benchmarks;

internal sealed record ProbeOptions(string Group, int Size, string OutputPath, int Samples, int WarmupSamples, int WarmupMilliseconds);

/// <summary>
/// Runs one case group for one scene size in one process. Warmup satisfies both the call count and the elapsed time
/// before any sample is taken; samples are raw per-call ticks and thread allocation deltas, so the runner keeps every
/// observation and computes nearest-rank statistics itself.
/// </summary>
internal static class Probe
{
    internal static void Run(ProbeOptions options)
    {
        var frequency = Stopwatch.Frequency;
        var cases = new List<object>();
        foreach (var descriptor in Cases.For(options.Group))
        {
            var instance = descriptor.Factory(options.Size);
            instance.Setup();

            var warmupStart = Stopwatch.GetTimestamp();
            var warmupCalls = 0;
            while (warmupCalls < options.WarmupSamples ||
                   ElapsedMilliseconds(warmupStart, Stopwatch.GetTimestamp(), frequency) < options.WarmupMilliseconds)
            {
                instance.Run();
                warmupCalls++;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            var ticks = new long[options.Samples];
            var bytes = new long[options.Samples];
            for (var index = 0; index < options.Samples; index++)
            {
                var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                instance.Run();
                ticks[index] = Stopwatch.GetTimestamp() - start;
                bytes[index] = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            }

            cases.Add(new
            {
                Method = descriptor.Method,
                WarmupCalls = warmupCalls,
                Ticks = ticks,
                Bytes = bytes,
                Collections = new
                {
                    Gen0 = GC.CollectionCount(0) - gen0,
                    Gen1 = GC.CollectionCount(1) - gen1,
                    Gen2 = GC.CollectionCount(2) - gen2,
                },
            });

            instance.Cleanup();
            Console.WriteLine($"{options.Group}.{descriptor.Method} size={options.Size} warmup={warmupCalls} " +
                $"medianMicroseconds={Microseconds(Median(ticks), frequency):F3} medianBytes={Median(bytes)}");
        }

        var payload = new
        {
            Completed = true,
            Scene = "inspector-v1",
            Group = options.Group,
            Size = options.Size,
            Samples = options.Samples,
            WarmupSamples = options.WarmupSamples,
            WarmupMilliseconds = options.WarmupMilliseconds,
            TickFrequency = frequency,
            ProcessId = Environment.ProcessId,
            // A process id can be handed out again by the operating system, so the batch check needs the moment this
            // process started: the pair is what proves every group, size and pass ran in a fresh process.
            ProcessStartedAt = DateTime.UtcNow,
            Runtime = Environment.Version.ToString(),
            Cases = cases,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath))!);
        File.WriteAllText(options.OutputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static double ElapsedMilliseconds(long start, long end, long frequency) => (end - start) * 1000d / frequency;

    private static double Microseconds(long ticks, long frequency) => ticks * 1_000_000d / frequency;

    private static long Median(long[] values)
    {
        var sorted = (long[])values.Clone();
        Array.Sort(sorted);
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
