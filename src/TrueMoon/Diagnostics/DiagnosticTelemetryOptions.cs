namespace TrueMoon.Diagnostics;

/// <summary>Copied limits for listener-driven operation telemetry; no exporters or timers are installed.</summary>
public sealed class DiagnosticTelemetryOptions
{
    /// <summary>Whether ActivitySource sampling is enabled.</summary>
    public bool TracingEnabled { get; set; } = true;
    /// <summary>Whether completed operations publish counter and duration measurements.</summary>
    public bool MetricsEnabled { get; set; } = true;
    /// <summary>Maximum distinct admitted operation names per owner, from 1 to 256.</summary>
    public int MaxOperationNames { get; set; } = 64;
    /// <summary>Maximum inspected custom trace properties, from 0 to 64.</summary>
    public int MaxProperties { get; set; } = 16;
    /// <summary>Maximum characters in a trace property value, from 1 to 4096.</summary>
    public int MaxPropertyChars { get; set; } = 256;
    /// <summary>Maximum characters in operation names and trace property keys, from 1 to 512.</summary>
    public int MaxNameChars { get; set; } = 128;

    internal DiagnosticTelemetryOptions Snapshot()
    {
        if (MaxOperationNames is < 1 or > 256 || MaxProperties is < 0 or > 64 ||
            MaxPropertyChars is < 1 or > 4096 || MaxNameChars is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(DiagnosticTelemetryOptions));
        return new() { TracingEnabled = TracingEnabled, MetricsEnabled = MetricsEnabled,
            MaxOperationNames = MaxOperationNames, MaxProperties = MaxProperties,
            MaxPropertyChars = MaxPropertyChars, MaxNameChars = MaxNameChars };
    }
}
