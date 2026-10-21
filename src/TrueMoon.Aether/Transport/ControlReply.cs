using TrueMoon.Aether.Checkpoints;

namespace TrueMoon.Aether.Transport;

public sealed class ControlReply
{
    public ControlRequest Stamp { get; set; } = new();
    public string Phase { get; set; } = "";
    public long Revision { get; set; }
    public long Tick { get; set; }
    public int ProcessId { get; set; }
    public long SurfaceGeneration { get; set; }
    public long DroppedFrames { get; set; }
    /// <summary>Explicit checkpoint acknowledgement; runtime Phase still describes the current mode.</summary>
    public int CheckpointOutcome { get; set; }
    /// <summary>Detached encoded snapshot for a successful capture.</summary>
    public byte[]? Checkpoint { get; set; }
    /// <summary>GameInputOutcome for SetInput; zero for other operations.</summary>
    public int InputOutcome { get; set; }
}
