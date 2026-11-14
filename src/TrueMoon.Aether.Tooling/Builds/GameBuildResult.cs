namespace TrueMoon.Aether.Tooling.Builds;

/// <summary>Tooling result: a runtime artifact plus process-local source/compiler witnesses.</summary>
public sealed record GameBuildResult(GameBuildArtifact Artifact, GameBuildInputSnapshot? Inputs = null,
    GameBuildCompilationSnapshot? Compilation = null)
{
    public string DirectoryPath => Artifact.DirectoryPath;
    public string BuildId => Artifact.BuildId;
    public string HostContractId => Artifact.HostContractId;
    public string EntryAssembly => Artifact.EntryAssembly;
    public string EntryType => Artifact.EntryType;
    public bool RuntimeAccess => Artifact.RuntimeAccess;
    public long SourceRevision => Artifact.SourceRevision;
    public IReadOnlyList<string> Files => Artifact.Files;
    public IDisposable Pin() => Artifact.Pin();
    internal GameBuildResult WithInputs(GameBuildInputSnapshot inputs, GameBuildCompilationSnapshot? compilation = null)
        => this with { Inputs = inputs, Compilation = compilation };
    public static implicit operator GameBuildArtifact(GameBuildResult value) => value.Artifact;
    public static implicit operator GameBuildResult(GameBuildArtifact value) => new(value);
}
