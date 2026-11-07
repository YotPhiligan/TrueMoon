namespace TrueMoon.Aether.Ecs.Transforms;

/// <summary>Same-World hierarchy edge. The default handle means root; destruction never implicitly reparents children.</summary>
/// <param name="Parent">Live transform node, or the default root marker.</param>
public readonly record struct TransformParent(EntityHandle Parent);
