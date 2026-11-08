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
    /// <summary>Full-scene capability version, or zero for a legacy game/preview.</summary>
    public int SceneWorldVersion { get; set; }
    /// <summary>Exact process-local component registry generation.</summary>
    public string SceneRegistryId { get; set; } = "";
    /// <summary>Maximum complete envelope bytes, independent of the control-message limit.</summary>
    public int SceneMaximumBytes { get; set; }
    /// <summary>Maximum contiguous scene chunk bytes.</summary>
    public int SceneChunkBytes { get; set; }
    /// <summary>Acknowledged initial transfer operation.</summary>
    public string SceneLoadId { get; set; } = "";
    /// <summary>Complete contiguous bytes acknowledged so far.</summary>
    public int SceneLoadOffset { get; set; }
    /// <summary>Active ECS World identity, empty until complete activation.</summary>
    public string SceneWorldId { get; set; } = "";
    /// <summary>Initial active entity cardinality.</summary>
    public int SceneEntityCount { get; set; }
    /// <summary>Initial active component cardinality.</summary>
    public int SceneComponentCount { get; set; }
    /// <summary>Explicit preview view capability; zero for game/legacy/reference consumers.</summary>
    public int PreviewViewVersion { get; set; }
    /// <summary>Exact accepted capture stamp; control acknowledgement does not prove presentation.</summary>
    public byte[]? PreviewViewStamp { get; set; }
}
