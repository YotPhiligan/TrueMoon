using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using TrueMoon.Aether.Sessions;

namespace EngineSessionProbe.Measurements;

internal sealed class BaselineCapture : EventListener
{
    internal const string EnvironmentKey = "TRUEMOON_FRAME_BASELINE";
    private const int Capacity = 100_000;
    private readonly Lock _gate = new();
    private readonly List<FrameSample> _frames = new();
    private readonly List<ProcessSample> _process = new();
    private readonly List<string> _errors = new();
    private readonly CancellationTokenSource _end = new();
    private readonly Task _sampler;
    private int _overflow;

    internal BaselineCapture()
    {
        // Also covers sources created before this listener; OnEventSourceCreated covers later sources.
        foreach (var source in EventSource.GetSources()) Enable(source);
        SampleProcess();
        _sampler = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            try { while (await timer.WaitForNextTickAsync(_end.Token).ConfigureAwait(false)) SampleProcess(); }
            catch (OperationCanceledException) when (_end.IsCancellationRequested) { }
            catch (Exception error) { lock (_gate) _errors.Add(error.ToString()); }
        });
    }

    protected override void OnEventSourceCreated(EventSource source) => Enable(source);
    private void Enable(EventSource source)
    { if (source.Name == "TrueMoon-Aether-Frames") EnableEvents(source, EventLevel.Informational); }

    protected override void OnEventWritten(EventWrittenEventArgs data)
    {
        // EventSource emits EventId0 for malformed manifests/payloads: never silently accept a broken trace.
        if (data.EventId == 0) { lock (_gate) _errors.Add(string.Join(";", data.Payload ?? [])); return; }
        if (data.EventId != 1 || data.Payload is not { Count: 7 } p) return;
        Add(new((string)p[0]!, (string)p[1]!, (long)p[2]!, (long)p[3]!, (long)p[4]!, (long)p[5]!, (long)p[6]!));
    }

    internal void Record(string phase, SessionIdentity identity, ulong surface, ulong frame, long started, long ended)
        => Add(new(phase, identity.SessionId.ToString("N"), checked((long)identity.Generation), checked((long)surface), checked((long)frame), started, ended));

    private void Add(FrameSample sample)
    {
        lock (_gate)
        {
            if (sample.Started <= 0 || sample.Ended < sample.Started) { _errors.Add("Invalid stage timestamps."); return; }
            if (_frames.Count == Capacity) { _overflow++; return; }
            _frames.Add(sample);
        }
    }

    private void SampleProcess()
    {
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var sample = new ProcessSample(Stopwatch.GetTimestamp(), GC.GetTotalAllocatedBytes(), process.PrivateMemorySize64,
            process.WorkingSet64, process.HandleCount, process.TotalProcessorTime.TotalMilliseconds,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        lock (_gate) { if (_process.Count < 4096) _process.Add(sample); else _overflow++; }
    }

    internal CaptureData Snapshot()
    { lock (_gate) return new(Stopwatch.Frequency, _frames.ToArray(), _process.ToArray(), _overflow, _errors.ToArray()); }

    internal async Task SaveAsync(string directory, string name)
    {
        void Progress(string stage) => Records.Write(directory, name + ".state.json", new { Stage = stage });
        Progress("draining-sampler");
        _end.Cancel(); await _sampler.ConfigureAwait(false); SampleProcess();
        Progress("serializing");
        var json = JsonSerializer.Serialize(Snapshot());
        Progress("writing");
        await File.WriteAllTextAsync(Path.Combine(directory, name), json).ConfigureAwait(false);
        Progress("complete");
    }

    public override void Dispose()
    {
        _end.Cancel(); _sampler.GetAwaiter().GetResult(); base.Dispose(); _end.Dispose();
    }
}

/// <summary>Raw bounded capture, persisted only outside measured frame paths.</summary>
public sealed record CaptureData(long Frequency, FrameSample[] Frames, ProcessSample[] Process, int Overflow, string[] Errors);
