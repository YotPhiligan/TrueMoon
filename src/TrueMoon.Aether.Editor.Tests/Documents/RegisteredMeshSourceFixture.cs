using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

internal sealed class RegisteredMeshSourceFixture : IAsyncDisposable,
    IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>, IFactory<ModelSourceCapture, ModelSourceCaptureArguments>
{
    private readonly ResolvedProject _project;
    private readonly ModelImportHost _host = new();
    private readonly FileModelImportLedgerStore _store = new();
    private readonly Dictionary<Guid, (ModelSourceCaptureArguments Source, ModelImportLedgerArguments Ledger)> _sources = [];

    internal RegisteredMeshSourceFixture(ResolvedProject project)
    {
        _project = project;
        Models = new(project, this, this, null, null);
    }

    internal ProjectModelCatalog Models { get; }

    internal Task<MeshSourceChoice> AddAsync(string sourcePath, bool reverseSlots = false, bool uv = true,
        string leftLabel = "left", string rightLabel = "right") =>
        AddSourceAsync(sourcePath, reverseSlots, uv, leftLabel, rightLabel);

    internal async Task<MeshSourceChoice> AddSourceAsync(string sourcePath, bool reverseSlots = false, bool uv = true,
        string leftLabel = "left", string? rightLabel = "right")
    {
        var path = Path.Combine(_project.RootDirectory, sourcePath);
        var directory = Directory.CreateDirectory(Path.GetDirectoryName(path)!).FullName;
        var id = Guid.NewGuid();
        var source = new ModelSourceCaptureArguments(id, directory, Path.GetFileName(path));
        var ledger = new ModelImportLedgerArguments(id, Path.Combine(_project.ContentDirectory, $"mesh-source-{id:N}.ledger"));
        _sources.Add(id, (source, ledger));
        var choice = await PublishAsync(id, 0, reverseSlots, uv, leftLabel, rightLabel);
        await Models.RegisterAsync(Path.GetFileNameWithoutExtension(path), source, ledger);
        return choice;
    }

    internal Task<MeshSourceChoice> RepublishAsync(MeshSourceChoice current, bool reverseSlots = true) =>
        PublishAsync(current.SourceId, current.Generation, reverseSlots, true, "left", "right");

    private async Task<MeshSourceChoice> PublishAsync(Guid sourceId, long expectedGeneration, bool reverseSlots,
        bool uv, string leftLabel, string? rightLabel)
    {
        var (source, ledger) = _sources[sourceId];
        var width = reverseSlots ? 3 : 1;
        var root = Path.Combine(source.Directory, source.RootName);
        await File.WriteAllTextAsync(root, Path.GetExtension(root) switch
        {
            ".obj" => $"v 0 0 0\nv {width} 0 0\nv 0 1 0\nf 1 2 3\n",
            ".gltf" => $$$"""{"asset":{"version":"2.0","generator":"Managed candidate {{{width}}}"}}""",
            _ => throw new ArgumentException("Use an OBJ or glTF capture fixture.")
        });
        await using var capture = ((IFactory<ModelSourceCapture, ModelSourceCaptureArguments>)this).Create(source);
        var captured = await capture.CaptureAsync();
        Vector3[] vertices = [Vector3.Zero, new(width, 0, 0), Vector3.UnitY];
        var bounds = new RenderBounds(Vector3.Zero, new(width, 1, 0));
        var firstMaterial = reverseSlots && rightLabel != null ? 1 : 0;
        uint[] indices = rightLabel == null ? [0, 1, 2] : reverseSlots ? [2, 1, 0, 0, 1, 2] : [0, 1, 2, 2, 1, 0];
        var geometry = uv
            ? new MeshDefinition(vertices, [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], indices, Slots(), bounds)
            : new MeshDefinition(vertices, indices, Slots(), bounds);
        var mesh = new ModelImportMesh(geometry, firstMaterial);
        if (rightLabel != null)
            mesh = mesh with { SlotMaterials = reverseSlots ? [new(rightLabel, 1), new(leftLabel, 0)] : [new(leftLabel, 0), new(rightLabel, 1)] };
        var candidate = new ModelImportCandidate(sourceId, captured.InputRevision, [mesh],
            rightLabel == null ? [new(Vector3.UnitZ)] :
                reverseSlots ? [new(Vector3.UnitX), new(Vector3.One)] : [new(Vector3.UnitZ), new(Vector3.UnitY)],
            [new(0, Matrix4x4.Identity)], new(["panel"], rightLabel == null ? ["blue"] : ["blue", "green"], ["first"]),
            texture: uv ? new("albedo.png", 0, new(1, 1, reverseSlots ? [64, 128, 255, 255] : [255, 128, 64, 255])) : null);
        await using var session = Create(ledger);
        var published = await session.PublishAsync(candidate, expectedGeneration);
        return new(sourceId, published.Generation, published.Candidate.Revision, Assert.Single(published.Meshes).AssetId);

        MeshSlot[] Slots() => rightLabel == null ? [new(RenderContentProfile.DefaultSlotId, 0, 3)] :
            [new(RenderContentProfile.DefaultSlotId, 0, 3), new(DocumentData.Id(551), 3, 3)];
    }

    public ModelImportLedgerSession Create(ModelImportLedgerArguments arguments) => new(_host, _store, arguments);
    ModelSourceCapture IFactory<ModelSourceCapture, ModelSourceCaptureArguments>.Create(ModelSourceCaptureArguments arguments) => new(arguments);
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
