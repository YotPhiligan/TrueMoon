using System.Diagnostics;
using MiniAudioEx.Native;

namespace MiniAudioExResearch;

// Managed callback is deliberate diagnostic instrumentation, not the proposed production callback path.
internal sealed unsafe class PlaybackTelemetry
{
    private readonly ma_engine_ptr engine;
    private readonly float[] pcm;
    private readonly long[] durations = new long[20000];
    private int samples, blocks, failures, exhausted;
    internal ma_device_data_proc Callback { get; }
    internal int Blocks => Volatile.Read(ref blocks);
    internal int Failures => Volatile.Read(ref failures);
    internal int OverflowBlocks => Volatile.Read(ref exhausted);

    internal PlaybackTelemetry(ma_engine_ptr engine, int seconds)
    {
        this.engine = engine;
        pcm = new float[seconds * 48000 * 2];
        Callback = Process;
    }

    private void Process(ma_device_ptr unused, nint output, nint input, uint frameCount)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            var result = MiniAudioNative.ma_engine_read_pcm_frames(engine, output, frameCount);
            if (result != ma_result.success) Interlocked.Increment(ref failures);
            int length = checked((int)frameCount * 2);
            int at = samples;
            if (at + length <= pcm.Length)
            {
                new ReadOnlySpan<float>((void*)output, length).CopyTo(pcm.AsSpan(at));
                Volatile.Write(ref samples, at + length);
            }
            else Interlocked.Increment(ref exhausted);
        }
        catch
        {
            Interlocked.Increment(ref failures);
            new Span<float>((void*)output, checked((int)frameCount * 2)).Clear();
        }
        int block = Interlocked.Increment(ref blocks) - 1;
        if (block < durations.Length) durations[block] = Stopwatch.GetTimestamp() - started;
    }

    internal float[] CopyPcm() => pcm.AsSpan(0, Volatile.Read(ref samples)).ToArray();

    internal object Snapshot()
    {
        long[] measured = durations.Take(Math.Min(Blocks, durations.Length)).Order().ToArray();
        double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        return new
        {
            Blocks, Frames = samples / 2, Failures = failures, CaptureOverflowBlocks = exhausted,
            CallbackIncludesManagedInstrumentation = true,
            P50Milliseconds = measured.Length == 0 ? 0 : Ms(measured[measured.Length / 2]),
            P99Milliseconds = measured.Length == 0 ? 0 : Ms(measured[(int)((measured.Length - 1) * .99)]),
            MaxMilliseconds = measured.Length == 0 ? 0 : Ms(measured[^1])
        };
    }
}
