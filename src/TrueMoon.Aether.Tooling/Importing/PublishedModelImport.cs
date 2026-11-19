using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Abstractions.Rendering;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Persistent identity association for one candidate-local resource.</summary>
/// <param name="Kind">Resource category.</param><param name="Index">Candidate-local selector.</param><param name="Key">Persistent semantic key.</param><param name="Id">Persistent ID.</param>
public sealed record ModelSubassetBinding(ModelSubassetKind Kind, int Index, ModelSubassetKey Key, Guid Id);

/// <summary>CPU mesh with permanent mesh/slot/material/binding identities.</summary>
/// <param name="AssetId">Mesh ID.</param><param name="SlotId">First material slot ID.</param><param name="MaterialId">First slot's material ID.</param>
/// <param name="BindingSetId">Binding-set ID reserved for runtime artifact construction.</param><param name="Geometry">Immutable geometry with the permanent slot IDs.</param>
public sealed record PublishedModelMesh(Guid AssetId, Guid SlotId, Guid MaterialId, Guid BindingSetId, MeshDefinition Geometry)
{
    internal PublishedModelSlot? SecondSlot { get; init; }

    /// <summary>The imported material assignment for each slot in geometry order.</summary>
    public ImmutableArray<PublishedModelSlot> Slots => SecondSlot is { } second
        ? [new(SlotId, MaterialId), second] : [new(SlotId, MaterialId)];
}

/// <summary>Persistent material assignment from the imported model.</summary>
/// <param name="SlotId">Material slot identity.</param><param name="MaterialId">Imported material asset identity.</param>
public readonly record struct PublishedModelSlot(Guid SlotId, Guid MaterialId);

/// <summary>Opaque material with a permanent asset identity.</summary>
/// <param name="AssetId">Persistent ID.</param><param name="Definition">Immutable CPU material.</param>
public sealed record PublishedModelMaterial(Guid AssetId, OpaqueUnlitMaterialDefinition Definition);

/// <summary>Node occurrence referring to a permanent mesh identity.</summary>
/// <param name="Id">Occurrence ID.</param><param name="MeshId">Mesh ID.</param><param name="World">Affine row-vector transform.</param>
public readonly record struct PublishedModelInstance(Guid Id, Guid MeshId, Matrix4x4 World);

/// <summary>One complete immutable candidate+identity generation. This is not a Nom or runtime manifest.</summary>
public sealed class PublishedModelImport
{
    internal PublishedModelImport(long generation, ModelImportCandidate candidate, ModelSubassetLedger ledger, IEnumerable<ModelSubassetBinding> bindings)
    {
        if (generation <= 0 || candidate.SourceId != ledger.SourceId) throw new InvalidDataException("Invalid published model generation/identity.");
        Generation = generation; Candidate = candidate; Ledger = ledger;
        var entries = bindings.Take(1025).OrderBy(value => value.Kind).ThenBy(value => value.Index).ToArray();
        var expected = checked(candidate.Meshes.Count * 2 + Count(candidate, ModelSubassetKind.Slot) + candidate.Materials.Count + candidate.Instances.Count);
        if (entries.Length != expected || expected > 1024) throw new InvalidDataException("Published model binding count mismatch.");
        var selectors = new HashSet<(ModelSubassetKind,int)>(); var keys = new HashSet<ModelSubassetKey>();
        var active = ledger.Entries.Where(value => value.Active).ToDictionary(value => value.Key);
        foreach (var entry in entries)
        {
            var count = Count(candidate, entry.Kind);
            if (entry.Kind != entry.Key.Kind || entry.Index < 0 || entry.Index >= count || !selectors.Add((entry.Kind,entry.Index)) || !keys.Add(entry.Key) ||
                !active.TryGetValue(entry.Key, out var persistent) || persistent.Id != entry.Id) throw new InvalidDataException("Invalid published model identity association.");
        }
        if (active.Count != keys.Count) throw new InvalidDataException("Published ledger contains unrelated active identities.");
        Bindings = Array.AsReadOnly(entries);
        var ids = entries.ToDictionary(value => (value.Kind,value.Index), value => value.Id);
        var meshes = new PublishedModelMesh[candidate.Meshes.Count];
        var nextExtraSlot = meshes.Length;
        for (var i = 0; i < meshes.Length; i++)
        {
            var mesh = candidate.Meshes[i];
            var geometry = mesh.Geometry;
            var slots = geometry.Slots.ToArray();
            slots[0] = slots[0] with { SlotId = ids[(ModelSubassetKind.Slot, i)] };
            PublishedModelSlot? second = null;
            if (slots.Length == 2)
            {
                slots[1] = slots[1] with { SlotId = ids[(ModelSubassetKind.Slot, nextExtraSlot++)] };
                second = new(slots[1].SlotId, ids[(ModelSubassetKind.Material, mesh.MaterialAt(1))]);
            }
            meshes[i] = new(ids[(ModelSubassetKind.Mesh, i)], slots[0].SlotId, ids[(ModelSubassetKind.Material, mesh.MaterialIndex)],
                ids[(ModelSubassetKind.BindingSet, i)], new MeshDefinition(geometry.Vertices, geometry.Indices, slots, geometry.Bounds))
                { SecondSlot = second };
        }
        Meshes = Array.AsReadOnly(meshes);
        Materials = Array.AsReadOnly(candidate.Materials.Select((material,index) => new PublishedModelMaterial(ids[(ModelSubassetKind.Material,index)],material)).ToArray());
        Instances = Array.AsReadOnly(candidate.Instances.Select((instance,index) => new PublishedModelInstance(ids[(ModelSubassetKind.Instance,index)],meshes[instance.MeshIndex].AssetId,instance.World)).ToArray());
    }
    /// <summary>Optimistic publication generation, starting at one.</summary>
    public long Generation { get; }
    /// <summary>Original validated CPU candidate and provenance.</summary>
    public ModelImportCandidate Candidate { get; }
    /// <summary>Matching immutable ledger, including deleted-resource tombstones.</summary>
    public ModelSubassetLedger Ledger { get; }
    /// <summary>Copied exact associations in kind/index order.</summary>
    public IReadOnlyList<ModelSubassetBinding> Bindings { get; }
    /// <summary>CPU meshes with persistent slot and material references.</summary>
    public IReadOnlyList<PublishedModelMesh> Meshes { get; }
    /// <summary>CPU materials with persistent IDs.</summary>
    public IReadOnlyList<PublishedModelMaterial> Materials { get; }
    /// <summary>Persistent node occurrences and mesh references.</summary>
    public IReadOnlyList<PublishedModelInstance> Instances { get; }

    internal static int Count(ModelImportCandidate candidate, ModelSubassetKind kind) => kind switch
    {
        ModelSubassetKind.Mesh or ModelSubassetKind.BindingSet => candidate.Meshes.Count,
        ModelSubassetKind.Slot => candidate.Meshes.Sum(mesh => mesh.Geometry.Slots.Length),
        ModelSubassetKind.Material => candidate.Materials.Count, ModelSubassetKind.Instance => candidate.Instances.Count,
        _ => throw new InvalidDataException("Unknown subasset kind.")
    };
    internal static PublishedModelImport Prepare(ModelImportCandidate candidate, PublishedModelImport? previous, long expectedGeneration, ModelImportIdentityEdits? edits)
    {
        if (expectedGeneration < 0 || (previous?.Generation ?? 0) != expectedGeneration) throw new InvalidOperationException("Stale model import generation.");
        if (previous != null && previous.Ledger.SourceId != candidate.SourceId) throw new InvalidDataException("Changed model source identity.");
        var labels = new Dictionary<(ModelSubassetKind,int),string>();
        foreach (var label in edits?.Labels ?? [])
        {
            ModelSubassetValidation.Key(new(label.Kind,label.Locator));
            if (label.Index < 0 || label.Index >= Count(candidate,label.Kind) || !labels.TryAdd((label.Kind,label.Index),label.Locator))
                throw new InvalidDataException("Invalid or duplicate candidate label selector.");
        }
        var keys = new List<(ModelSubassetKind Kind,int Index,ModelSubassetKey Key)>();
        var meshLocators = new string[candidate.Meshes.Count];
        string Resolve(ModelSubassetKind kind,int index,string? hint)
        {
            var locator = (labels.TryGetValue((kind,index),out var assigned) ? assigned : hint)
                ?? throw new InvalidDataException("Missing semantic locator; provide an explicit label.");
            ModelSubassetValidation.Locator(locator); keys.Add((kind,index,new(kind,locator))); return locator;
        }
        for (var i = 0; i < meshLocators.Length; i++) meshLocators[i] = Resolve(ModelSubassetKind.Mesh,i,candidate.Semantics.Meshes[i]);
        var nextExtraSlot = meshLocators.Length;
        for (var i = 0; i < meshLocators.Length; i++)
        {
            var slots = candidate.Meshes[i].SlotMaterials;
            var prefix = "slot/" + meshLocators[i];
            Resolve(ModelSubassetKind.Slot, i, slots.IsEmpty ? prefix : prefix + "/" + slots[0].Locator);
            if (!slots.IsEmpty) Resolve(ModelSubassetKind.Slot, nextExtraSlot++, prefix + "/" + slots[1].Locator);
            Resolve(ModelSubassetKind.BindingSet, i, "binding/" + meshLocators[i]);
        }
        for (var i = 0; i < candidate.Materials.Count; i++) Resolve(ModelSubassetKind.Material,i,candidate.Semantics.Materials[i]);
        for (var i = 0; i < candidate.Instances.Count; i++) Resolve(ModelSubassetKind.Instance,i,candidate.Semantics.Instances[i]);
        var ledger = (previous?.Ledger ?? new(candidate.SourceId,[])).Reconcile(keys.Select(value => value.Key),edits?.Renames);
        var active = ledger.Entries.Where(value => value.Active).ToDictionary(value => value.Key,value => value.Id);
        return new(checked(expectedGeneration+1),candidate,ledger,keys.Select(value => new ModelSubassetBinding(value.Kind,value.Index,value.Key,active[value.Key])));
    }
}
