namespace TrueMoon.Diagnostics;

/// <summary>Limits the independent emergency channel for failures of diagnostics itself.</summary>
public sealed class DiagnosticEmergencyOptions
{
    /// <summary>Whether internal diagnostic failures can be reported to the emergency worker.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Borrowed writer; null selects Console.Error at pipeline creation. Never disposed by the pipeline.</summary>
    public TextWriter? Writer { get; set; }
    /// <summary>Maximum queued plus executing reports, from 1 to 128.</summary>
    public int QueueCapacity { get; set; } = 8;
    /// <summary>Total report attempts per pipeline, from 1 to 1024; subsequent attempts are counted as suppressed.</summary>
    public int MaxReports { get; set; } = 16;
    /// <summary>Maximum characters per report including its literal LF, from 128 to 4096.</summary>
    public int MaxMessageChars { get; set; } = 1024;

    internal DiagnosticEmergencyOptions Snapshot()
    {
        if (QueueCapacity is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        if (MaxReports is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaxReports));
        if (MaxMessageChars is < 128 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaxMessageChars));
        return new() { Enabled = Enabled, Writer = Writer, QueueCapacity = QueueCapacity, MaxReports = MaxReports, MaxMessageChars = MaxMessageChars };
    }
}
