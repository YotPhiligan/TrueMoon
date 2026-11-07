namespace TrueMoon.Aether.Rendering;

/// <summary>Exact identities and hard ceilings of the first CPU mesh/material profile.</summary>
public static class RenderContentProfile
{
    /// <summary>Logical capability name; registration does not enable a renderer.</summary>
    public const string ProfileId = "aether.opaque-unlit/1";
    /// <summary>Supported exact asset schema version.</summary>
    public const int SchemaVersion = 1;
    /// <summary>Maximum vertices in one definition.</summary>
    public const int MaximumVertices = 65536;
    /// <summary>Maximum indices in one definition.</summary>
    public const int MaximumIndices = 196608;
    /// <summary>Maximum exact material ranges in one mesh.</summary>
    public const int MaximumSlots = 8;
    /// <summary>Maximum mesh canonical bytes.</summary>
    public const int MaximumMeshBytes = 2 * 1024 * 1024;
    /// <summary>Maximum material or binding-set canonical bytes.</summary>
    public const int MaximumSmallAssetBytes = 4096;
    /// <summary>Maximum distinct assets in one frozen manifest.</summary>
    public const int MaximumManifestEntries = 1024;
    /// <summary>Maximum canonical manifest fingerprint bytes.</summary>
    public const int MaximumManifestBytes = 256 * 1024;
    /// <summary>Largest absolute local vertex coordinate.</summary>
    public const float MaximumCoordinate = 1000000;
    /// <summary>Persistent mesh asset kind.</summary>
    public static Guid MeshKindId { get; } = Id("1201");
    /// <summary>Persistent opaque unlit material kind.</summary>
    public static Guid MaterialKindId { get; } = Id("1202");
    /// <summary>Persistent exact binding-set kind.</summary>
    public static Guid BindingSetKindId { get; } = Id("1203");
    /// <summary>Built-in shader family, layout version one.</summary>
    public static Guid UnlitFamilyId { get; } = Id("1301");
    /// <summary>Built-in cube asset, scoped to the manifest project.</summary>
    public static Guid CubeAssetId { get; } = Id("1401");
    /// <summary>Built-in opaque white material asset.</summary>
    public static Guid WhiteMaterialAssetId { get; } = Id("1402");
    /// <summary>Built-in cube/white binding-set asset.</summary>
    public static Guid CubeBindingSetAssetId { get; } = Id("1403");
    /// <summary>Stable material slot of the built-in cube.</summary>
    public static Guid DefaultSlotId { get; } = Id("1404");
    internal static Guid Id(string suffix) => Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f" + suffix);
    internal static bool IsKind(Guid kind) => kind == MeshKindId || kind == MaterialKindId || kind == BindingSetKindId;
}
