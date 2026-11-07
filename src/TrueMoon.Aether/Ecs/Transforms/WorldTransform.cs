using System.Numerics;

namespace TrueMoon.Aether.Ecs.Transforms;

/// <summary>Derived complete affine matrix at a published transform tick. Never decompose it to discard shear.</summary>
/// <param name="Matrix">Row-vector local*parentWorld matrix.</param><param name="Tick">Evaluation tick, including initial zero.</param>
public readonly record struct WorldTransform(Matrix4x4 Matrix, ulong Tick);
