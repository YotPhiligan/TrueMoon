namespace TrueMoon.Aether.Rendering;

/// <summary>Stable CPU content validation outcomes.</summary>
public enum RenderContentError
{
    /// <summary>Empty or foreign logical identity.</summary>
    InvalidIdentity,
    /// <summary>Unsupported exact schema or codec version.</summary>
    UnsupportedVersion,
    /// <summary>Unsupported geometry or shader layout.</summary>
    UnsupportedLayout,
    /// <summary>A profile admission ceiling was exceeded.</summary>
    LimitExceeded,
    /// <summary>Invalid vertices, indices or ranges.</summary>
    InvalidGeometry,
    /// <summary>Invalid or unrepresentable bounds.</summary>
    InvalidBounds,
    /// <summary>Invalid opaque material or color.</summary>
    InvalidMaterial,
    /// <summary>Invalid exact material slot binding.</summary>
    InvalidBinding,
    /// <summary>An asset is absent from the frozen manifest.</summary>
    MissingAsset,
    /// <summary>A reference names the wrong asset kind.</summary>
    AssetKindMismatch,
    /// <summary>A reference disagrees with the pinned content revision.</summary>
    RevisionMismatch,
    /// <summary>A manifest repeats a logical asset identity.</summary>
    DuplicateAsset,
    /// <summary>Invalid projection, pose or physical extent.</summary>
    InvalidCamera,
    /// <summary>Ambiguous or invalid component data.</summary>
    InvalidComponent,
    /// <summary>Malformed or noncanonical asset bytes.</summary>
    InvalidPayload
}

/// <summary>CPU-only content failure; no native allocation or partial resource owner is implied.</summary>
public sealed class RenderContentException : Exception
{
    internal RenderContentException(RenderContentError error, string message) : base(message) => Error = error;
    /// <summary>Gets the stable failure category.</summary>
    public RenderContentError Error { get; }
}
