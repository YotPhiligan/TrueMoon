namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>One registered model source available to scenes in the project.</summary>
/// <param name="SourceId">Persistent model source identity.</param>
/// <param name="Name">Authoring display name.</param>
/// <param name="SourcePath">Portable path to the root model file.</param>
public sealed record ProjectModelCatalogItem(Guid SourceId, string Name, string SourcePath);
