namespace TrueMoon.Mithril;

/// <summary>Validated immutable process-boundary identity and unit launch control. Never a mutable user profile.</summary>
public sealed record UnitLaunchSettings
{
    /// <summary>Creates an immutable launch snapshot.</summary>
    /// <param name="UnitId">Optional positive selected unit.</param><param name="ParentProcessId">Optional borrowed parent, different from this process.</param>
    /// <param name="ControlPipe">Optional bounded pipe name requiring a parent.</param><param name="UnitsDisabled">Whether parent unit declarations are disabled.</param>
    public UnitLaunchSettings(int? UnitId = null, int? ParentProcessId = null, string? ControlPipe = null, bool UnitsDisabled = false)
    {
        if (UnitId is <= 0) throw new ArgumentException("Invalid unit identity.", nameof(UnitId));
        if (ParentProcessId is <= 0 || ParentProcessId == Environment.ProcessId) throw new ArgumentException("Invalid parent identity.", nameof(ParentProcessId));
        if (ControlPipe is not null && (ParentProcessId is null || string.IsNullOrWhiteSpace(ControlPipe) || ControlPipe.Length > 256 ||
            ControlPipe.Any(character => char.IsControl(character) || character is '/' or '\\' or ':')))
            throw new ArgumentException("Invalid control endpoint.", nameof(ControlPipe));
        this.UnitId = UnitId; this.ParentProcessId = ParentProcessId; this.ControlPipe = ControlPipe; this.UnitsDisabled = UnitsDisabled;
    }
    /// <summary>Selected child unit, or null for the parent App.</summary>
    public int? UnitId { get; }
    /// <summary>Borrowed parent process, never an owned process handle.</summary>
    public int? ParentProcessId { get; }
    /// <summary>Optional owned control listener endpoint.</summary>
    public string? ControlPipe { get; }
    /// <summary>Whether parent unit declarations are disabled.</summary>
    public bool UnitsDisabled { get; }
}
