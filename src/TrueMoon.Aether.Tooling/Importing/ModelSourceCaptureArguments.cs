namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>One source identity and local input, selected by the authoring/build owner.</summary>
/// <param name="SourceId">Persistent source identity; callers retain it across reimport and directory moves.</param>
/// <param name="Directory">Absolute local directory containing the root and its dependencies.</param>
/// <param name="RootName">Relative glTF, GLB or OBJ name inside the directory.</param>
/// <param name="Limits">Optional smaller per-operation budgets; these are input policy, not global configuration.</param>
public sealed record ModelSourceCaptureArguments(Guid SourceId, string Directory, string RootName, ModelSourceCaptureLimits? Limits = null);
