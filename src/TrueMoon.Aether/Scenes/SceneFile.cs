namespace TrueMoon.Aether.Scenes;

/// <summary>A completely decoded file and the identity used to protect its next save.</summary>
/// <param name="Document">Validated authoring data.</param><param name="Stamp">Exact observed bytes.</param>
public sealed record SceneFile(SceneDocument Document, SceneFileStamp Stamp);
