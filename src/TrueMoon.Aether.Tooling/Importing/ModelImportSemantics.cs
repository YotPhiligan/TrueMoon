namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Authored source locators, in candidate-local order. Null/duplicate values require explicit labels before publication.</summary>
public sealed class ModelImportSemantics
{
    /// <summary>Copies bounded semantic hints; no identity is inferred from numeric positions.</summary>
    /// <param name="meshes">One optional locator per mesh.</param><param name="materials">One per material.</param><param name="instances">One per instance.</param>
    public ModelImportSemantics(IEnumerable<string?> meshes, IEnumerable<string?> materials, IEnumerable<string?> instances)
    {
        Meshes = Copy(meshes, 64); Materials = Copy(materials, 128); Instances = Copy(instances, 4096);
    }
    /// <summary>Mesh semantic locators.</summary>
    public IReadOnlyList<string?> Meshes { get; }
    /// <summary>Material semantic locators.</summary>
    public IReadOnlyList<string?> Materials { get; }
    /// <summary>Node occurrence semantic locators.</summary>
    public IReadOnlyList<string?> Instances { get; }
    private static IReadOnlyList<string?> Copy(IEnumerable<string?> source, int maximum)
    {
        ArgumentNullException.ThrowIfNull(source); var values = source.Take(maximum + 1).ToArray();
        if (values.Length > maximum) throw new InvalidDataException("Semantic hint count limit.");
        foreach (var value in values) if (value != null) ModelSubassetValidation.Locator(value);
        return Array.AsReadOnly(values);
    }
}

/// <summary>Explicit semantic label for one current candidate element. Index is a transport selector, never an identity.</summary>
/// <param name="Kind">Subasset category.</param><param name="Index">Current candidate-local index.</param><param name="Locator">Authored persistent locator.</param>
public readonly record struct ModelSubassetLabel(ModelSubassetKind Kind, int Index, string Locator);

/// <summary>Copied per-publication identity decisions. Labels disambiguate discovery; renames preserve old IDs.</summary>
public sealed class ModelImportIdentityEdits
{
    /// <summary>Snapshots a bounded set of explicit decisions without IO.</summary>
    /// <param name="labels">Current candidate labels.</param><param name="renames">Persistent rename/restore mappings.</param>
    public ModelImportIdentityEdits(IEnumerable<ModelSubassetLabel>? labels = null, IEnumerable<ModelSubassetRename>? renames = null)
    {
        Labels = Array.AsReadOnly((labels ?? []).Take(1025).ToArray()); Renames = Array.AsReadOnly((renames ?? []).Take(1025).ToArray());
        if (Labels.Count > 1024 || Renames.Count > 1024) throw new InvalidDataException("Identity edit budget exceeded.");
    }
    /// <summary>Explicit labels.</summary>
    public IReadOnlyList<ModelSubassetLabel> Labels { get; }
    /// <summary>Explicit same-kind renames/restores.</summary>
    public IReadOnlyList<ModelSubassetRename> Renames { get; }
}
