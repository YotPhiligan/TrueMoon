namespace EngineSessionProbe.Measurements;

/// <summary>One successful CPU-observed stage; timestamps use the Windows system-wide QPC clock.</summary>
public sealed record FrameSample(string Phase, string Session, long Runtime, long Surface, long Frame, long Started, long Ended)
{
    /// <summary>Includes both generations because frame numbers restart after GPU resize and runtime replacement.</summary>
    public string Key => $"{Session}/{Runtime}/{Surface}/{Frame}";
}

/// <summary>Cumulative counters from this process, including instrumentation overhead.</summary>
public sealed record ProcessSample(long Timestamp, long AllocatedBytes, long PrivateBytes, long WorkingSet,
    int Handles, double CpuMilliseconds, int Gen0, int Gen1, int Gen2);

/// <summary>A bounded steady interval, excluding startup, resize, replacement and warmup.</summary>
public sealed record MeasurementWindow(string Name, int Width, int Height, long Started, long Ended);

/// <summary>End-to-end Editor operation, including its acknowledged first presentation or cleanup.</summary>
public sealed record OperationSample(string Name, long Started, long Ended, bool Succeeded, string? Error = null);
