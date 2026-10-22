namespace TrueMoon.Aether.Scenes;

public sealed class SceneEntity
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool Interactive { get; set; }
    /// <summary>Optional control v3 world-space cube; absent entities are non-rendering groups/markers.</summary>
    public Rendering.ScenePrimitive? Primitive { get; set; }
}
