using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Scenes;
/// <summary>Resolution failure with a machine-readable category and ID-based authoring path.</summary>
public sealed class EntityResolutionException : Exception
{
    internal EntityResolutionException(EntityResolutionError code, string path, string message) : base($"{path}: {message}")
    { Code = code; Path = path; }
    /// <summary>Gets the failure category.</summary>
    public EntityResolutionError Code { get; }
    /// <summary>Gets the target definition/instance, component and optional property path.</summary>
    public string Path { get; }
}
