
namespace TrueMoon.Aether.Scenes;


/// <summary>Legacy bounded scene input. SceneSnapshot copies it before activation.</summary>
public sealed class SceneEntity
{
    /// <summary>Scene-local persistent identity.</summary>
    public string Id { get; set; } = "";
    /// <summary>Finite X position.</summary>
    public float X { get; set; }
    /// <summary>Finite Y position.</summary>
    public float Y { get; set; }
    /// <summary>Finite Z position.</summary>
    public float Z { get; set; }
    /// <summary>Whether interaction is enabled.</summary>
    public bool Interactive { get; set; }
    /// <summary>Optional control v3 world-space cube; absent entities are non-rendering groups/markers.</summary>
    public Rendering.ScenePrimitive? Primitive { get; set; }
}
