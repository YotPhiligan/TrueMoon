namespace EngineSessionProbe.Measurements;

/// <summary>Analysis of complete raw captures; missing measurements remain explicitly unavailable.</summary>
public static class BaselineAnalysis
{
    /// <summary>Nearest-rank percentiles over the entire interval, without truncating to the last256samples.</summary>
    public static Distribution Summarize(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Any(value => !double.IsFinite(value) || value < 0)) throw new InvalidDataException("Invalid latency sample.");
        return sorted.Length == 0 ? new(0, null, null, null) : new(sorted.Length,
            sorted[(int)Math.Ceiling(sorted.Length * .50) - 1], sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], sorted[^1]);
    }

    /// <summary>Joins exact identities/surfaces; repeated UI presentations count only once per produced frame.</summary>
    public static WindowResult Analyze(MeasurementWindow window, CaptureData editor, CaptureData[] children, bool gpu)
    {
        if (window.Ended <= window.Started || editor.Frequency <= 0 || children.Any(c => c.Frequency != editor.Frequency))
            throw new InvalidDataException("Incompatible measurement clocks/intervals.");
        if (children.Append(editor).Any(c => c.Overflow != 0 || c.Errors.Length != 0)) throw new InvalidDataException("Incomplete frame capture.");
        var runtime = children.SelectMany(c => c.Frames).ToArray();
        bool Inside(FrameSample frame) => frame.Started >= window.Started && frame.Ended <= window.Ended;
        double Milliseconds(long ticks) => ticks * 1000d / editor.Frequency;
        var produced = runtime.Where(f => f.Phase == "production" && Inside(f)).ToDictionary(f => f.Key);
        var received = editor.Frames.Where(f => f.Phase == (gpu ? "receive" : "copy")).Select(f => f.Key).ToHashSet();
        var presented = editor.Frames.Where(f => f.Phase == "present").GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.Min(f => f.Ended));
        var latencies = produced.Values.Where(f => presented.ContainsKey(f.Key)).Select(f => Milliseconds(presented[f.Key] - f.Started)).ToArray();
        var seconds = (window.Ended - window.Started) / (double)editor.Frequency;
        var receivedCount = produced.Keys.Count(received.Contains); var presentedCount = latencies.Length;
        Distribution Stage(string phase, IEnumerable<FrameSample> source) => Summarize(source.Where(f => f.Phase == phase && Inside(f)).Select(f => Milliseconds(f.Ended - f.Started)));
        return new(window, seconds, produced.Count, receivedCount, presentedCount, runtime.Count(f => f.Phase == "producer-drop" && Inside(f)),
            produced.Count - receivedCount, produced.Count - presentedCount, presentedCount / seconds, Summarize(latencies),
            new Dictionary<string, Distribution>
            {
                ["Production"] = Stage("production", runtime), ["PatternOnly"] = Stage("pattern", runtime),
                ["CpuCopyIncludingCrc"] = Stage("copy", editor.Frames), ["GpuProduceRoundTrip"] = Stage("receive", editor.Frames),
                ["RetainedCopySubmission"] = Stage("retained-copy", editor.Frames), ["GpuUseIncludingOwnerQueueAndFence"] = Stage("gpu-use", editor.Frames)
            }, Resources(window, editor), children.Select(c => Resources(window, c)).Where(c => c.Available).ToArray());
    }

    private static ResourceResult Resources(MeasurementWindow window, CaptureData capture)
    {
        var samples = capture.Process.Where(p => p.Timestamp >= window.Started && p.Timestamp <= window.Ended).OrderBy(p => p.Timestamp).ToArray();
        if (samples.Length < 2) return new(false, "Fewer than two process samples in this interval.", null, null, null, null, null, null, null, null, null, null);
        var first = samples[0]; var last = samples[^1]; var seconds = (last.Timestamp - first.Timestamp) / (double)capture.Frequency;
        return new(true, null, first.Timestamp, last.Timestamp, seconds, last.AllocatedBytes - first.AllocatedBytes,
            (last.AllocatedBytes - first.AllocatedBytes) / seconds / (1024 * 1024), (last.CpuMilliseconds - first.CpuMilliseconds) / (seconds * 1000),
            samples.Max(p => p.PrivateBytes), samples.Max(p => p.WorkingSet), samples.Max(p => p.Handles),
            new[] { last.Gen0 - first.Gen0, last.Gen1 - first.Gen1, last.Gen2 - first.Gen2 });
    }
}

/// <summary>Null values mean no samples, never a measured zero latency.</summary>
public sealed record Distribution(int Samples, double? P50Ms, double? P95Ms, double? MaxMs);
/// <summary>Counter deltas cover the reported inner polling interval; CPU is average logical cores used.</summary>
public sealed record ResourceResult(bool Available, string? Reason, long? Started, long? Ended, double? Seconds, long? AllocatedBytes,
    double? AllocatedMiBPerSecond, double? CpuCores, long? PeakPrivateBytes, long? PeakWorkingSet, int? PeakHandles, int[]? Collections);
/// <summary>Frame accounting follows the production cohort; unreceived and unpresented overlap and must not be added.</summary>
public sealed record WindowResult(MeasurementWindow Window, double Seconds, int Produced, int Received, int Presented, int ProducerDroppedAttempts,
    int Unreceived, int Unpresented, double PresentedFps, Distribution ProduceToPresent, Dictionary<string, Distribution> Stages,
    ResourceResult EditorResources, ResourceResult[] RuntimeResources);
