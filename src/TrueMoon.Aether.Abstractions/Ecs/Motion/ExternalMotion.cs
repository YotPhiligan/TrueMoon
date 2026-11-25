namespace TrueMoon.Aether.Ecs.Motion;

/// <summary>Marks entities whose local pose is advanced by another system, such as a physics bridge.</summary>
/// <remarks>Built-in velocity movement skips these entities. The owning subsystem adds/removes this marker at
/// structural phase boundaries. It contains no backend handle and does not select a provider.</remarks>
public readonly record struct ExternalMotion : IComponent;
