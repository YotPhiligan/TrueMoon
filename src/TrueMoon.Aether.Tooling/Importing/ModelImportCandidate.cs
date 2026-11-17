using System.Numerics;
using System.Security.Cryptography;
using TrueMoon.Aether.Abstractions.Rendering;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Detached CPU projection. Array indices are local to this candidate, never persistent asset identities.</summary>
public sealed class ModelImportCandidate
{
    internal ModelImportCandidate(Guid sourceId, string inputRevision, IEnumerable<ModelImportMesh> meshes,
        IEnumerable<OpaqueUnlitMaterialDefinition> materials, IEnumerable<ModelImportInstance> instances, ModelImportSemantics? semantics = null)
    {
        if (sourceId == Guid.Empty || inputRevision.Length != 64 || !inputRevision.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid model input identity.");
        SourceId = sourceId; InputRevision = inputRevision;
        Meshes = Array.AsReadOnly(meshes.Take(65).ToArray());
        Materials = Array.AsReadOnly(materials.Take(129).ToArray());
        Instances = Array.AsReadOnly(instances.Take(4097).ToArray());
        Semantics = semantics ?? new(new string?[Meshes.Count], new string?[Materials.Count], new string?[Instances.Count]);
        if (Semantics.Meshes.Count != Meshes.Count || Semantics.Materials.Count != Materials.Count || Semantics.Instances.Count != Instances.Count)
            throw new InvalidDataException("Candidate semantic counts do not match CPU data.");
        if (Meshes.Count is < 1 or > 64 || Materials.Count is < 1 or > 128 || Instances.Count is < 1 or > 4096)
            throw new InvalidDataException("CPU model count limit exceeded.");
        foreach (var mesh in Meshes)
        {
            if (mesh.Geometry == null || mesh.MaterialIndex < 0 || mesh.MaterialIndex >= Materials.Count)
                throw new InvalidDataException("Invalid mesh material reference.");
            if (mesh.Geometry.Slots.Length != 1 || mesh.Geometry.Slots[0].SlotId != RenderContentProfile.DefaultSlotId)
                throw new InvalidDataException("Imported meshes require one default material slot.");
        }
        foreach (var material in Materials) ArgumentNullException.ThrowIfNull(material);
        foreach (var instance in Instances)
        {
            if (instance.MeshIndex < 0 || instance.MeshIndex >= Meshes.Count)
                throw new InvalidDataException("Invalid instance mesh reference.");
            Meshes[instance.MeshIndex].Geometry.Bounds.Transform(instance.World).Validate();
        }
        // The packet includes the recipe and exact CPU bytes. Encoding also enforces the aggregate byte quota.
        Revision = Convert.ToHexString(SHA256.HashData(ModelImportProtocol.Encode(this)));
    }

    /// <summary>Versioned interpretation, including coordinates, material conversion and native parser.</summary>
    public const string Recipe = "aether.assimp605.gltf2-static-opaque-unlit/2";
    /// <summary>Caller-supplied persistent source identity.</summary>
    public Guid SourceId { get; }
    /// <summary>Exact captured-input fingerprint.</summary>
    public string InputRevision { get; }
    /// <summary>SHA256 of canonical CPU candidate bytes and recipe; not a persistent subasset ID.</summary>
    public string Revision { get; }
    /// <summary>Validated immutable geometry and candidate-local material links.</summary>
    public IReadOnlyList<ModelImportMesh> Meshes { get; }
    /// <summary>Opaque unlit sRGB materials.</summary>
    public IReadOnlyList<OpaqueUnlitMaterialDefinition> Materials { get; }
    /// <summary>Instances with affine world transforms in meters, right-handed and Y-up.</summary>
    public IReadOnlyList<ModelImportInstance> Instances { get; }
    /// <summary>Authored identity hints; ambiguous/missing hints do not prevent CPU-only import.</summary>
    public ModelImportSemantics Semantics { get; }
}

/// <summary>One triangle mesh; MaterialIndex addresses this candidate only.</summary>
public sealed record ModelImportMesh(MeshDefinition Geometry, int MaterialIndex);

/// <summary>One occurrence of a candidate-local mesh with a row-vector world transform.</summary>
public readonly record struct ModelImportInstance(int MeshIndex, Matrix4x4 World);
