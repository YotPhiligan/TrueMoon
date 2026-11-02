namespace TrueMoon.Aether.Builds;

/// <summary>Process-local provenance of one referenced library compiled from the shared frozen graph.</summary>
/// <param name="ProjectPath">Original project path.</param>
/// <param name="EntryAssembly">Declared flat private DLL name.</param>
/// <param name="Inputs">Evaluated inputs belonging to the shared pre-evaluation closure.</param>
/// <param name="Compilation">Captured compiler invocation for this library.</param>
public sealed record GameBuildProjectCompilationSnapshot(string ProjectPath, string EntryAssembly,
    GameBuildInputSnapshot Inputs, GameBuildCompilationSnapshot Compilation);
