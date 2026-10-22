namespace TrueMoon.Aether.Scenes;

/// <summary>Persistent identity of an authoring scene, independent of its revision and runtime session.</summary>
public readonly record struct SceneId
{
    /// <summary>Creates a nonempty identity.</summary>
    /// <param name="value">Persistent nonempty GUID.</param>
    public SceneId(Guid value) { AuthoringValidation.Id(value, nameof(value)); Value = value; }
    /// <summary>Gets the persistent identifier.</summary>
    public Guid Value { get; }
}
