using System.Numerics;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Preview;

/// <summary>One detached projected cube, with persistent identity and original draw position.</summary>
/// <param name="Entity">Persistent authoring ID.</param><param name="DrawOrder">Position in the frozen projected entity sequence.</param>
/// <param name="World">Full affine transform, including hierarchy shear.</param>
public readonly record struct PreviewPickPrimitive(EntityId Entity, int DrawOrder, Matrix4x4 World);
