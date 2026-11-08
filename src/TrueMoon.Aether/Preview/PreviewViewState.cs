namespace TrueMoon.Aether.Preview;

/// <summary>Whole immutable external camera capture for one explicit document/view/content context.</summary>
public sealed record PreviewViewState
{
    /// <summary>Creates validated identity and camera values; extent-dependent admission is separate.</summary>
    /// <param name="context">Explicit context owned by the caller.</param><param name="camera">Detached immutable camera.</param>
    public PreviewViewState(PreviewViewContext context, PreviewCamera camera)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(camera); context.Validate();
        Context = context; Camera = camera;
    }
    /// <summary>Gets the opening/view/content context.</summary>
    public PreviewViewContext Context { get; }
    /// <summary>Gets the exact rigid camera and navigation values.</summary>
    public PreviewCamera Camera { get; }
    /// <summary>Gets the stamp captured with rendered geometry, independent of tick or surface generation.</summary>
    public PreviewFrameStamp Stamp => new(Context.DocumentEpoch, Context.ViewId, Context.ContentId, Context.SceneId, Context.SceneRevision, Camera.Revision);
}

/// <summary>Versioned preview identity in frame metadata. Null denotes a non-pickable legacy/game/reference frame.</summary>
/// <param name="DocumentEpoch">Opening epoch.</param><param name="ViewId">Viewport binding.</param><param name="ContentId">Committed payload token.</param>
/// <param name="SceneId">Persistent scene GUID.</param><param name="SceneRevision">Authoring revision.</param><param name="CameraRevision">Exact positive camera revision.</param>
public sealed record PreviewFrameStamp(Guid DocumentEpoch, Guid ViewId, Guid ContentId, Guid SceneId, long SceneRevision, ulong CameraRevision)
{
    /// <summary>Gets the explicit preview stamp schema version, including in GPU JSON metadata.</summary>
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; init; } = PreviewViewCodec.Version;
    /// <summary>Validates stamps after CPU/GPU decode and before publication.</summary>
    public void Validate()
    {
        if (Version != PreviewViewCodec.Version) throw new InvalidDataException("Unsupported preview frame stamp version.");
        new PreviewViewContext(DocumentEpoch, ViewId, ContentId, SceneId, SceneRevision).Validate();
        if (CameraRevision == 0) throw new InvalidDataException("Invalid preview camera revision.");
    }
}
