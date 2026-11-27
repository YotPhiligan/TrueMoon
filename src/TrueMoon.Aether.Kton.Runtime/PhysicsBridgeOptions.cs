using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Aether.Kton.Runtime;

/// <summary>Copied ECS contact projection policy and authored collision geometry; world options remain with the provider.</summary>
public sealed class PhysicsBridgeOptions
{
    private Dictionary<Guid, PhysicsStaticMesh>? _staticMeshes;

    /// <summary>Maximum projected contacts per successful step,1..256. Overflow faults instead of silently truncating.</summary>
    public int MaximumContactEvents { get; set; } = 64;

    /// <summary>Validates and copies a CPU triangle mesh for authored scene preparation.</summary>
    /// <param name="assetId">Nonempty, unique stable collision asset ID.</param>
    /// <param name="vertices">Vertex positions in metres. Keep the input unchanged for this call.</param>
    /// <param name="indices">Triangle indices. Keep the input unchanged for this call.</param>
    public void AddStaticMesh(Guid assetId, ReadOnlyMemory<Vector3> vertices, ReadOnlyMemory<int> indices)
    {
        if (assetId == Guid.Empty || _staticMeshes?.ContainsKey(assetId) == true)
            throw new ArgumentException("A static mesh asset ID must be nonempty and unique.", nameof(assetId));
        PhysicsValidation.ValidateShape(new PhysicsStaticMesh(vertices, indices), new PhysicsLimits()).ThrowIfFailure();
        var mesh = new PhysicsStaticMesh(vertices.ToArray(), indices.ToArray());
        (_staticMeshes ??= new()).Add(assetId, mesh);
    }

    internal bool TryGetStaticMesh(Guid assetId, [NotNullWhen(true)] out PhysicsStaticMesh? mesh)
    {
        mesh = null;
        return _staticMeshes?.TryGetValue(assetId, out mesh) == true;
    }

    internal PhysicsBridgeOptions Snapshot()
    {
        if (MaximumContactEvents is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(MaximumContactEvents));
        return new() { MaximumContactEvents = MaximumContactEvents, _staticMeshes = _staticMeshes is null ? null : new(_staticMeshes) };
    }
}
