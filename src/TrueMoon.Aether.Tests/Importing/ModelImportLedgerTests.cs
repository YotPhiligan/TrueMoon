using System.Numerics;
using System.Security.Cryptography;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Importing;

public sealed class ModelImportLedgerTests
{
    private static readonly Guid SourceId = new("567b046a-dcbe-4c0f-811a-9154a81a3334");
    private static ModelSubassetKey Key(ModelSubassetKind kind, string locator) => new(kind, locator);
    private static ModelSubassetBinding Binding(PublishedModelImport record, ModelSubassetKind kind, int index)
        => record.Bindings.Single(binding => binding.Kind == kind && binding.Index == index);

    private static MeshDefinition Geometry(float width) => new(
        [Vector3.Zero, new(width, 0, 0), Vector3.UnitY], [0, 1, 2],
        [new(RenderContentProfile.DefaultSlotId, 0, 3)], new(Vector3.Zero, new(width, 1, 0)));

    private static ModelImportCandidate Candidate(bool reverse = false, float hullWidth = 1,
        ModelImportSemantics? semantics = null, Guid? sourceId = null)
    {
        ModelImportMesh[] meshes = [new(Geometry(hullWidth), 0), new(Geometry(2), 1)];
        OpaqueUnlitMaterialDefinition[] materials = [new(new(.5f, .25f, 1)), new(new(.1f, .2f, .3f))];
        ModelImportInstance[] instances = [new(0, Matrix4x4.CreateTranslation(10, 2, 0)), new(1, Matrix4x4.CreateTranslation(2, 4, 0))];
        if (reverse)
        {
            meshes = [meshes[1] with { MaterialIndex = 0 }, meshes[0] with { MaterialIndex = 1 }];
            materials = [materials[1], materials[0]];
            instances = [instances[1] with { MeshIndex = 0 }, instances[0] with { MeshIndex = 1 }];
        }
        semantics ??= reverse
            ? new(["Wheel", "Hull"], ["Rubber", "Paint"], ["/Root/Wheel", "/Root/Hull"])
            : new(["Hull", "Wheel"], ["Paint", "Rubber"], ["/Root/Hull", "/Root/Wheel"]);
        return new(sourceId ?? SourceId, new('A', 64), meshes, materials, instances, semantics);
    }

    private static void AssertCompleteProjection(PublishedModelImport record, ModelImportCandidate candidate)
    {
        Assert.Equal(candidate.Revision, record.Candidate.Revision); Assert.Equal(candidate.InputRevision, record.Candidate.InputRevision);
        Assert.Equal(SourceId, record.Ledger.SourceId); Assert.Equal(10, record.Bindings.Count);
        Assert.Equal(record.Bindings.Select(binding => binding.Key).OrderBy(key => key.Kind).ThenBy(key => key.Locator, StringComparer.Ordinal),
            record.Ledger.Entries.Where(entry => entry.Active).Select(entry => entry.Key));
        for (var index = 0; index < candidate.Meshes.Count; index++)
        {
            var mesh = candidate.Meshes[index]; var projected = record.Meshes[index];
            Assert.Equal(Binding(record, ModelSubassetKind.Mesh, index).Id, projected.AssetId);
            Assert.Equal(Binding(record, ModelSubassetKind.Slot, index).Id, projected.SlotId);
            Assert.Equal(Binding(record, ModelSubassetKind.BindingSet, index).Id, projected.BindingSetId);
            Assert.Equal(record.Materials[mesh.MaterialIndex].AssetId, projected.MaterialId);
            Assert.Equal(projected.SlotId, Assert.Single(projected.Geometry.Slots).SlotId);
            Assert.NotEqual(RenderContentProfile.DefaultSlotId, projected.SlotId);
            Assert.Equal(RenderContentProfile.DefaultSlotId, Assert.Single(mesh.Geometry.Slots).SlotId);
            Assert.Equal(mesh.Geometry.Vertices.ToArray(), projected.Geometry.Vertices);
            Assert.Equal(mesh.Geometry.Indices.ToArray(), projected.Geometry.Indices);
            Assert.Equal(mesh.Geometry.Bounds, projected.Geometry.Bounds);
            Assert.Equal(candidate.Materials[index].BaseColorSrgb, record.Materials[index].Definition.BaseColorSrgb);
            Assert.Equal(Binding(record, ModelSubassetKind.Material, index).Id, record.Materials[index].AssetId);
            Assert.Equal(Binding(record, ModelSubassetKind.Instance, index).Id, record.Instances[index].Id);
            Assert.Equal(record.Meshes[candidate.Instances[index].MeshIndex].AssetId, record.Instances[index].MeshId);
            Assert.Equal(candidate.Instances[index].World, record.Instances[index].World);
        }
    }

    [Fact]
    public async Task ReadAndSessionOpen_MissingNestedPathAreReadOnlyAndPrecancelCreatesNothing()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        await using var host = new ModelImportHost(); await using var session = new ModelImportLedgerSession(host, store, new(SourceId, fixture.Path));
        Assert.Null(await store.ReadAsync(fixture.Path, SourceId)); Assert.Null(await session.OpenAsync()); Assert.Null(session.Current);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAsync(fixture.Path, SourceId, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PublishAsync(fixture.Path, SourceId, 0, Candidate(), cancellationToken: cancelled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = session.OpenAsync(cancelled.Token); });
        Assert.False(Directory.Exists(fixture.Root)); Assert.Equal(0, host.ActiveOperationCount);
    }

    [Fact]
    public async Task PublishAndReopen_PreserveCompleteCpuCandidateLedgerAndPersistentAssociations()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore(); var candidate = Candidate();
        var published = await store.PublishAsync(fixture.Path, SourceId, 0, candidate);
        Assert.Equal(1, published.Generation); AssertCompleteProjection(published, candidate);
        Assert.Equal(Key(ModelSubassetKind.Mesh, "Hull"), Binding(published, ModelSubassetKind.Mesh, 0).Key);
        Assert.Equal(Key(ModelSubassetKind.Slot, "slot/Hull"), Binding(published, ModelSubassetKind.Slot, 0).Key);
        Assert.Equal(Key(ModelSubassetKind.BindingSet, "binding/Hull"), Binding(published, ModelSubassetKind.BindingSet, 0).Key);
        Assert.Equal(Key(ModelSubassetKind.Material, "Paint"), Binding(published, ModelSubassetKind.Material, 0).Key);
        Assert.Equal(Key(ModelSubassetKind.Instance, "/Root/Hull"), Binding(published, ModelSubassetKind.Instance, 0).Key);
        var bytes = File.ReadAllBytes(fixture.Path); Assert.Equal(ModelImportLedgerCodec.Encode(published), bytes);
        var reopened = Assert.IsType<PublishedModelImport>(await new FileModelImportLedgerStore().ReadAsync(fixture.Path, SourceId));
        Assert.Equal(1, reopened.Generation); AssertCompleteProjection(reopened, candidate);
        Assert.Equal(published.Ledger.Entries, reopened.Ledger.Entries); Assert.Equal(published.Bindings, reopened.Bindings);
        Assert.Equal(candidate.Semantics.Meshes, reopened.Candidate.Semantics.Meshes);
        Assert.Equal(candidate.Semantics.Materials, reopened.Candidate.Semantics.Materials);
        Assert.Equal(candidate.Semantics.Instances, reopened.Candidate.Semantics.Instances);
        Assert.Throws<NotSupportedException>(() => ((IList<ModelSubassetBinding>)reopened.Bindings).Clear());
        fixture.AssertOnlyRecordAndPermanentLock(); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public async Task Publish_ReaderHoldingOldHeadKeepsItsSnapshotWhileNewReaderSeesCommit()
    {
        using var fixture = new Fixture();
        var directory = Path.Combine(fixture.Root, new string('а', 160), new string('b', 160));
        var path = Path.Combine(directory, "модель.tml");
        var store = new FileModelImportLedgerStore();
        var original = await store.PublishAsync(path, SourceId, 0, Candidate());
        using var retained = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var changed = Candidate(hullWidth: 3);
        await store.PublishAsync(path, SourceId, 1, changed);

        var current = Assert.IsType<PublishedModelImport>(await store.ReadAsync(path, SourceId));
        Assert.Equal(2, current.Generation);
        AssertCompleteProjection(current, changed);
        Assert.Equal(original.Ledger.Entries, current.Ledger.Entries);
        var retainedBytes = new byte[checked((int)retained.Length)];
        await retained.ReadExactlyAsync(retainedBytes);
        Assert.Equal(ModelImportLedgerCodec.Encode(original), retainedBytes);
        Assert.Equal(new[] { path, path + ".write-lock" }, Directory.GetFiles(directory).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Publish_ConcurrentReadersNeverSeeAnAbsentOrMixedHead()
    {
        const int replacements = 80;
        using var fixture = new Fixture();
        var store = new FileModelImportLedgerStore();
        var candidates = new[] { Candidate(), Candidate(hullWidth: 3) };
        var initial = await store.PublishAsync(fixture.Path, SourceId, 0, candidates[0]);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(async () =>
        {
            long last = 1;
            do
            {
                var snapshot = await store.ReadAsync(fixture.Path, SourceId);
                started.TrySetResult();
                Assert.NotNull(snapshot);
                Assert.InRange(snapshot.Generation, last, replacements + 1L);
                Assert.Equal(initial.Ledger.Entries, snapshot.Ledger.Entries);
                Assert.Equal(candidates[(snapshot.Generation - 1) % 2].Revision, snapshot.Candidate.Revision);
                last = snapshot.Generation;
            } while (!stop.IsCancellationRequested);
        });

        try
        {
            await Task.WhenAny(started.Task, reader);
            for (var generation = 1; generation <= replacements && !reader.IsCompleted; generation++)
                await store.PublishAsync(fixture.Path, SourceId, generation, candidates[generation % 2]);
        }
        finally { stop.Cancel(); await reader; }

        var final = Assert.IsType<PublishedModelImport>(await store.ReadAsync(fixture.Path, SourceId));
        Assert.Equal(replacements + 1, final.Generation);
        Assert.Equal(candidates[0].Revision, final.Candidate.Revision);
        fixture.AssertOnlyRecordAndPermanentLock();
    }

    [Fact]
    public async Task Reimport_ReorderAndGeometryChangePreserveSemanticIdsAndCorrectReferences()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        var original = await store.PublishAsync(fixture.Path, SourceId, 0, Candidate());
        var changed = Candidate(reverse: true, hullWidth: 3); var result = await store.PublishAsync(fixture.Path, SourceId, 1, changed);
        Assert.Equal(2, result.Generation); Assert.NotEqual(original.Candidate.Revision, result.Candidate.Revision);
        Assert.Equal(original.Ledger.Entries, result.Ledger.Entries); AssertCompleteProjection(result, changed);
        Assert.Equal(original.Meshes[0].AssetId, result.Meshes[1].AssetId);
        Assert.Equal(original.Materials[0].AssetId, result.Materials[1].AssetId);
        Assert.Equal(original.Instances[0].Id, result.Instances[1].Id);
        Assert.Equal(new Vector3(3, 0, 0), result.Meshes[1].Geometry.Vertices[1]);
        Assert.Equal(Vector3.UnitX, original.Meshes[0].Geometry.Vertices[1]);
    }

    [Fact]
    public async Task Reimport_DoesNotInferRenameFromSameContentAndIndexButExplicitMappingPreservesIds()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        var original = await store.PublishAsync(fixture.Path, SourceId, 0, Candidate());
        var renamed = Candidate(semantics: new(["Body", "Wheel"], ["Paint", "Rubber"], ["/Root/Hull", "/Root/Wheel"]));
        var edits = new ModelImportIdentityEdits(renames:
        [
            new(Key(ModelSubassetKind.Mesh, "Hull"), Key(ModelSubassetKind.Mesh, "Body")),
            new(Key(ModelSubassetKind.Slot, "slot/Hull"), Key(ModelSubassetKind.Slot, "slot/Body")),
            new(Key(ModelSubassetKind.BindingSet, "binding/Hull"), Key(ModelSubassetKind.BindingSet, "binding/Body"))
        ]);
        using var explicitFixture = new Fixture(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(explicitFixture.Path)!);
        File.WriteAllBytes(explicitFixture.Path, File.ReadAllBytes(fixture.Path));
        var explicitRename = await store.PublishAsync(explicitFixture.Path, SourceId, 1, renamed, edits);
        var inferred = await store.PublishAsync(fixture.Path, SourceId, 1, renamed);
        Assert.Equal(original.Meshes[0].AssetId, explicitRename.Meshes[0].AssetId);
        Assert.Equal(original.Meshes[0].SlotId, explicitRename.Meshes[0].SlotId);
        Assert.Equal(original.Meshes[0].BindingSetId, explicitRename.Meshes[0].BindingSetId);
        Assert.Equal(explicitRename.Ledger.Entries, (await store.ReadAsync(explicitFixture.Path, SourceId))!.Ledger.Entries);
        Assert.NotEqual(original.Meshes[0].AssetId, inferred.Meshes[0].AssetId);
        Assert.NotEqual(original.Meshes[0].SlotId, inferred.Meshes[0].SlotId);
        Assert.NotEqual(original.Meshes[0].BindingSetId, inferred.Meshes[0].BindingSetId);
        Assert.Equal(original.Materials[0].AssetId, inferred.Materials[0].AssetId);
        Assert.Equal(original.Instances[0].Id, inferred.Instances[0].Id);
        Assert.Equal(inferred.Meshes[0].AssetId, inferred.Instances[0].MeshId);
        Assert.Equal(original.Ledger.Entries.Where(entry => (entry.Key.Kind is ModelSubassetKind.Mesh or ModelSubassetKind.Slot or ModelSubassetKind.BindingSet) && entry.Key.Locator.EndsWith("Hull", StringComparison.Ordinal))
            .Select(entry => entry with { Active = false }), inferred.Ledger.Entries.Where(entry => !entry.Active));
        Assert.Equal(original.Candidate.Meshes[0].Geometry.Vertices.ToArray(), inferred.Candidate.Meshes[0].Geometry.Vertices);
    }

    [Fact]
    public async Task Reimport_DeleteRequiresExplicitRestoreAndPreservesBytesOnImplicitRevival()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore(); var candidate = Candidate();
        var original = await store.PublishAsync(fixture.Path, SourceId, 0, candidate);
        var wheelOnly = new ModelImportCandidate(SourceId, candidate.InputRevision, [new(candidate.Meshes[1].Geometry, 0)],
            [candidate.Materials[1]], [new(0, candidate.Instances[1].World)], new(["Wheel"], ["Rubber"], ["/Root/Wheel"]));
        var deleted = await store.PublishAsync(fixture.Path, SourceId, 1, wheelOnly);
        Assert.Equal(5, deleted.Ledger.Entries.Count(entry => !entry.Active)); Assert.Equal(5, deleted.Bindings.Count);
        var bytes = File.ReadAllBytes(fixture.Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, SourceId, 2, candidate));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
        var restore = new ModelImportIdentityEdits(renames: deleted.Ledger.Entries.Where(entry => !entry.Active).Select(entry => new ModelSubassetRename(entry.Key, entry.Key)));
        var restored = await store.PublishAsync(fixture.Path, SourceId, 2, candidate, restore);
        Assert.Equal(3, restored.Generation); Assert.Equal(original.Ledger.Entries, restored.Ledger.Entries);
        Assert.Equal(original.Bindings, restored.Bindings); AssertCompleteProjection(restored, candidate);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Publish_MissingOrDuplicateHintsRequireExplicitLabels(bool duplicate)
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        string?[] hints = duplicate ? ["same", "same"] : [null, null];
        var candidate = Candidate(semantics: new(hints, hints, hints));
        var decoded = ModelImportProtocol.Decode(ModelImportProtocol.Encode(candidate), SourceId, candidate.InputRevision);
        Assert.Equal(hints, decoded.Semantics.Meshes); Assert.Equal(hints, decoded.Semantics.Materials); Assert.Equal(hints, decoded.Semantics.Instances);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, SourceId, 0, candidate));
        Assert.False(File.Exists(fixture.Path));
        var labels = new ModelImportIdentityEdits(
        [
            new(ModelSubassetKind.Mesh, 0, "Hull"), new(ModelSubassetKind.Mesh, 1, "Wheel"),
            new(ModelSubassetKind.Material, 0, "Paint"), new(ModelSubassetKind.Material, 1, "Rubber"),
            new(ModelSubassetKind.Instance, 0, "/Root/Hull"), new(ModelSubassetKind.Instance, 1, "/Root/Wheel"),
            new(ModelSubassetKind.Slot, 0, "authored-slot"), new(ModelSubassetKind.BindingSet, 0, "authored-binding")
        ]);
        var published = await store.PublishAsync(fixture.Path, SourceId, 0, candidate, labels);
        AssertCompleteProjection(published, candidate);
        Assert.Equal("Hull", Binding(published, ModelSubassetKind.Mesh, 0).Key.Locator);
        Assert.Equal("authored-slot", Binding(published, ModelSubassetKind.Slot, 0).Key.Locator);
        Assert.Equal("authored-binding", Binding(published, ModelSubassetKind.BindingSet, 0).Key.Locator);
        Assert.Equal(hints, candidate.Semantics.Meshes); Assert.Equal(0, candidate.Instances[0].MeshIndex);
    }

    [Fact]
    public async Task Publish_LongMeshLocatorRequiresBoundedSlotAndBindingLabels()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        var candidate = Candidate(semantics: new([new string('a', 256), "Wheel"], ["Paint", "Rubber"], ["/Hull", "/Wheel"]));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, SourceId, 0, candidate));
        Assert.False(File.Exists(fixture.Path));
        var record = await store.PublishAsync(fixture.Path, SourceId, 0, candidate, new(
            [new(ModelSubassetKind.Slot, 0, "slot"), new(ModelSubassetKind.BindingSet, 0, "binding")]));
        Assert.Equal(new string('a', 256), Binding(record, ModelSubassetKind.Mesh, 0).Key.Locator);
        Assert.Equal("slot", Binding(record, ModelSubassetKind.Slot, 0).Key.Locator);
        Assert.Equal("binding", Binding(record, ModelSubassetKind.BindingSet, 0).Key.Locator);
    }

    [Theory]
    [InlineData("negative-index")] [InlineData("past-end-index")] [InlineData("duplicate-selector")] [InlineData("unknown-kind")]
    public async Task Publish_InvalidLabelSelectorsLeaveExactPreviousBytes(string invalid)
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        var original = await store.PublishAsync(fixture.Path, SourceId, 0, Candidate()); var bytes = File.ReadAllBytes(fixture.Path);
        ModelSubassetLabel[] labels = invalid switch
        {
            "negative-index" => [new(ModelSubassetKind.Mesh, -1, "Hull")],
            "past-end-index" => [new(ModelSubassetKind.Mesh, 2, "Hull")],
            "duplicate-selector" => [new(ModelSubassetKind.Mesh, 0, "Hull"), new(ModelSubassetKind.Mesh, 0, "Body")],
            _ => [new((ModelSubassetKind)0, 0, "Hull")]
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, SourceId, 1, Candidate(), new(labels)));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
        Assert.Equal(original.Ledger.Entries, (await store.ReadAsync(fixture.Path, SourceId))!.Ledger.Entries);
        fixture.AssertOnlyRecordAndPermanentLock();
    }

    [Fact]
    public async Task Session_StaleForeignInvalidAndCancelledPublishesKeepCurrentAndExactOldBytes()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        await using var host = new ModelImportHost(); await using var session = new ModelImportLedgerSession(host, store, new(SourceId, fixture.Path));
        var original = await session.PublishAsync(Candidate(), 0); var bytes = File.ReadAllBytes(fixture.Path);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PublishAsync(Candidate(hullWidth: 3), 0));
        Assert.Same(original, session.Current); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
        await Assert.ThrowsAsync<InvalidDataException>(() => session.PublishAsync(Candidate(sourceId: Guid.NewGuid()), 1));
        Assert.Same(original, session.Current); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
        await Assert.ThrowsAsync<InvalidDataException>(() => session.PublishAsync(Candidate(semantics: new([null, null], [null, null], [null, null])), 1));
        Assert.Same(original, session.Current); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.PublishAsync(Candidate(), 1, cancellationToken: cancelled.Token));
        Assert.Same(original, session.Current); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path)); Assert.Equal(0, host.ActiveOperationCount);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(fixture.Path, Guid.NewGuid()));
        var foreign = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, foreign, 1, Candidate(sourceId: foreign)));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path)); fixture.AssertOnlyRecordAndPermanentLock();
        Assert.Equal(2, (await session.PublishAsync(Candidate(hullWidth: 3), 1)).Generation);
    }

    [Fact]
    public async Task Publish_PermanentWriterLockRejectsAnotherStoreAndStaleGenerationThenAllowsRetry()
    {
        using var fixture = new Fixture(); var first = new FileModelImportLedgerStore(); var second = new FileModelImportLedgerStore();
        await first.PublishAsync(fixture.Path, SourceId, 0, Candidate()); var bytes = File.ReadAllBytes(fixture.Path);
        using (var lease = new FileStream(fixture.Path + ".write-lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => second.PublishAsync(fixture.Path, SourceId, 1, Candidate(hullWidth: 3)));
            Assert.Equal(1, (await second.ReadAsync(fixture.Path, SourceId))!.Generation);
            Assert.Equal(bytes, File.ReadAllBytes(fixture.Path)); fixture.AssertOnlyRecordAndPermanentLock();
        }
        var winner = await second.PublishAsync(fixture.Path, SourceId, 1, Candidate(hullWidth: 3)); var committed = File.ReadAllBytes(fixture.Path);
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.PublishAsync(fixture.Path, SourceId, 1, Candidate(hullWidth: 4)));
        Assert.Equal(committed, File.ReadAllBytes(fixture.Path));
        Assert.Equal(winner.Candidate.Revision, (await first.ReadAsync(fixture.Path, SourceId))!.Candidate.Revision);
        Assert.Equal(2, winner.Generation); fixture.AssertOnlyRecordAndPermanentLock();
    }

    [Fact]
    public async Task Session_LateReplacementFailureKeepsCurrentAndExactBytesAndRemovesPendingFile()
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        await using var host = new ModelImportHost(); await using var session = new ModelImportLedgerSession(host, store, new(SourceId, fixture.Path));
        var original = await session.PublishAsync(Candidate(), 0); var bytes = File.ReadAllBytes(fixture.Path);
        using (var reader = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAsync<IOException>(() => session.PublishAsync(Candidate(hullWidth: 3), 1));
            Assert.Same(original, session.Current); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
            Assert.Equal(0, host.ActiveOperationCount); fixture.AssertOnlyRecordAndPermanentLock();
        }
        var committed = await session.PublishAsync(Candidate(hullWidth: 3), 1);
        Assert.Same(committed, session.Current); Assert.Equal(2, committed.Generation);
        Assert.NotEqual(bytes, File.ReadAllBytes(fixture.Path)); fixture.AssertOnlyRecordAndPermanentLock();
    }

    [Theory]
    [InlineData("magic")] [InlineData("length")] [InlineData("checksum")] [InlineData("truncated")]
    [InlineData("trailing")] [InlineData("generation")] [InlineData("association")]
    public async Task Read_RejectsCorruptEnvelopeAndChecksummedInvalidPayloadWithoutChangingBytes(string mutation)
    {
        using var fixture = new Fixture(); var store = new FileModelImportLedgerStore();
        await store.PublishAsync(fixture.Path, SourceId, 0, Candidate()); var bytes = File.ReadAllBytes(fixture.Path);
        switch (mutation)
        {
            case "magic": bytes[0] ^= 1; break;
            case "length": BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 4); break;
            case "checksum": bytes[^1] ^= 1; break;
            case "truncated": bytes = bytes[..^1]; break;
            case "trailing": bytes = bytes.Concat(new byte[] { 0 }).ToArray(); break;
            case "generation": BitConverter.GetBytes(0L).CopyTo(bytes, 40); Rehash(bytes); break;
            case "association": SourceId.ToByteArray().CopyTo(bytes, bytes.Length - 16); Rehash(bytes); break;
        }
        File.WriteAllBytes(fixture.Path, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(fixture.Path, SourceId));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, SourceId, 1, Candidate()));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path)); fixture.AssertOnlyRecordAndPermanentLock();
    }

    private static void Rehash(byte[] bytes) => SHA256.HashData(bytes.AsSpan(40)).CopyTo(bytes, 8);

    [Fact]
    public async Task Read_RejectsOversizedFileBeforeDecode()
    {
        using var fixture = new Fixture(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fixture.Path)!);
        using (var file = new FileStream(fixture.Path, FileMode.CreateNew, FileAccess.Write)) file.SetLength(ModelImportLedgerCodec.MaximumBytes + 1L);
        await Assert.ThrowsAsync<InvalidDataException>(() => new FileModelImportLedgerStore().ReadAsync(fixture.Path, SourceId));
        Assert.Equal(ModelImportLedgerCodec.MaximumBytes + 1L, new FileInfo(fixture.Path).Length);
        Assert.False(File.Exists(fixture.Path + ".write-lock"));
    }

    [Theory]
    [InlineData(1015, true)] [InlineData(1016, true)] [InlineData(1017, false)]
    public async Task Publish_ActiveBudgetIncludesMeshesSlotsBindingsMaterialsAndInstances(int instances, bool accepted)
    {
        using var fixture = new Fixture(); var original = Candidate();
        var candidate = new ModelImportCandidate(SourceId, original.InputRevision, original.Meshes, original.Materials,
            Enumerable.Range(0, instances).Select(index => new ModelImportInstance(index % 2, Matrix4x4.Identity)),
            new(original.Semantics.Meshes, original.Semantics.Materials, Enumerable.Range(0, instances).Select(index => $"node/{index}")));
        var store = new FileModelImportLedgerStore();
        if (accepted)
        {
            var result = await store.PublishAsync(fixture.Path, SourceId, 0, candidate);
            Assert.Equal(instances + 8, result.Bindings.Count); Assert.Equal(instances + 8, result.Ledger.Entries.Count);
            Assert.All(result.Instances, instance => Assert.Contains(instance.MeshId, result.Meshes.Select(mesh => mesh.AssetId)));
        }
        else { await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishAsync(fixture.Path, SourceId, 0, candidate)); Assert.False(File.Exists(fixture.Path)); }
    }

    [Fact]
    public void SemanticsAndIdentityEditsDetachInputsAndProtocolV2PreservesNullableUnicodeHints()
    {
        string?[] meshes = ["\u00e9\u96ea", null]; string?[] materials = ["same", "same"]; string?[] instances = [null, "/Root/Wheel"];
        var semantics = new ModelImportSemantics(meshes, materials, instances); meshes[0] = "changed"; materials[0] = "changed"; instances[0] = "changed";
        var candidate = Candidate(semantics: semantics); var bytes = ModelImportProtocol.Encode(candidate);
        var decoded = ModelImportProtocol.Decode(bytes, SourceId, candidate.InputRevision);
        Assert.Equal("aether.assimp605.gltf2-static-opaque-unlit/2", ModelImportCandidate.Recipe);
        Assert.Equal(new string?[] { "\u00e9\u96ea", null }, decoded.Semantics.Meshes);
        Assert.Equal(new string?[] { "same", "same" }, decoded.Semantics.Materials);
        Assert.Equal(new string?[] { null, "/Root/Wheel" }, decoded.Semantics.Instances);
        Assert.Equal(candidate.Revision, decoded.Revision); Assert.NotEqual(Candidate().Revision, decoded.Revision);
        Assert.Throws<NotSupportedException>(() => ((IList<string?>)semantics.Meshes).Clear());
        BitConverter.GetBytes(0x31434d54).CopyTo(bytes, 0);
        Assert.Throws<InvalidDataException>(() => ModelImportProtocol.Decode(bytes, SourceId, candidate.InputRevision));

        var label = new ModelSubassetLabel(ModelSubassetKind.Mesh, 0, "Hull"); var rename = new ModelSubassetRename(Key(ModelSubassetKind.Mesh, "A"), Key(ModelSubassetKind.Mesh, "B"));
        var labels = new[] { label }; var renames = new[] { rename }; var edits = new ModelImportIdentityEdits(labels, renames);
        labels[0] = label with { Locator = "changed" }; renames[0] = rename with { To = Key(ModelSubassetKind.Mesh, "changed") };
        Assert.Equal(label, Assert.Single(edits.Labels)); Assert.Equal(rename, Assert.Single(edits.Renames));
        Assert.Throws<NotSupportedException>(() => ((IList<ModelSubassetLabel>)edits.Labels).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ModelSubassetRename>)edits.Renames).Clear());
    }

    [Theory]
    [InlineData(ModelSubassetKind.Mesh, 64)] [InlineData(ModelSubassetKind.Material, 128)] [InlineData(ModelSubassetKind.Instance, 4096)]
    public void SemanticHintCountBudgetsAcceptBeforeAndAtLimitAndRejectAfter(ModelSubassetKind kind, int maximum)
    {
        ModelImportSemantics Create(int count) => new(kind == ModelSubassetKind.Mesh ? new string?[count] : [],
            kind == ModelSubassetKind.Material ? new string?[count] : [], kind == ModelSubassetKind.Instance ? new string?[count] : []);
        Assert.Equal(maximum - 1, Count(Create(maximum - 1))); Assert.Equal(maximum, Count(Create(maximum)));
        Assert.Throws<InvalidDataException>(() => Create(maximum + 1));
        int Count(ModelImportSemantics value) => kind switch { ModelSubassetKind.Mesh => value.Meshes.Count, ModelSubassetKind.Material => value.Materials.Count, _ => value.Instances.Count };
    }

    [Theory]
    [InlineData("mesh")] [InlineData("material")] [InlineData("instance")]
    public void CandidateRejectsMismatchedSemanticCounts(string kind)
        => Assert.Throws<InvalidDataException>(() => Candidate(semantics: new(kind == "mesh" ? ["Hull"] : ["Hull", "Wheel"],
            kind == "material" ? ["Paint"] : ["Paint", "Rubber"], kind == "instance" ? ["/Hull"] : ["/Hull", "/Wheel"])));

    [Fact]
    public void IdentityEditBudgetsAccept1024AndReject1025ForEachCollection()
    {
        var label = new ModelSubassetLabel(ModelSubassetKind.Mesh, 0, "Hull");
        var rename = new ModelSubassetRename(Key(ModelSubassetKind.Mesh, "A"), Key(ModelSubassetKind.Mesh, "B"));
        Assert.Equal(1024, new ModelImportIdentityEdits(Enumerable.Repeat(label, 1024)).Labels.Count);
        Assert.Equal(1024, new ModelImportIdentityEdits(renames: Enumerable.Repeat(rename, 1024)).Renames.Count);
        Assert.Throws<InvalidDataException>(() => new ModelImportIdentityEdits(Enumerable.Repeat(label, 1025)));
        Assert.Throws<InvalidDataException>(() => new ModelImportIdentityEdits(renames: Enumerable.Repeat(rename, 1025)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppFactory_IsLazyCallerOwnedAndSessionsShareAdmissionUntilStopDrainsCancellation(bool microsoft)
    {
        using var fixture = new Fixture(); var original = PublishedModelImport.Prepare(Candidate(), null, 0, null); var store = new BlockingStore(original);
        await using var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<IModelImportLedgerStore>(_ => store)); context.UseAetherModelImport();
        }).Build();
        await app.StartAsync(); var host = app.Services.Resolve<ModelImportHost>()!;
        var factory = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
        await using var active = factory.Create(new(SourceId, fixture.Path))!;
        await using var idle = factory.Create(new(SourceId, fixture.Path))!;
        await using var contender = factory.Create(new(SourceId, fixture.Path))!;
        Assert.NotSame(active, contender); Assert.Equal(0, store.ReadCalls); Assert.Equal(0, store.PublishCalls);
        Assert.Null(active.Current); Assert.False(Directory.Exists(fixture.Root));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = active.OpenAsync(cancelled.Token); });
        Assert.Equal(0, store.ReadCalls); Assert.Equal(0, host.ActiveOperationCount);
        Assert.Same(original, await active.OpenAsync()); Assert.Same(original, active.Current);
        var publication = active.PublishAsync(Candidate(hullWidth: 3), 1);
        try
        {
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(1, host.ActiveOperationCount);
            Assert.Throws<InvalidOperationException>(() => { _ = active.OpenAsync(); });
            Assert.Throws<InvalidOperationException>(() => { _ = contender.OpenAsync(); });
            await idle.DisposeAsync(); Assert.Equal(1, host.ActiveOperationCount);
            var stop = app.StopAsync(); await store.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(stop.IsCompleted); Assert.Equal(1, host.ActiveOperationCount); Assert.Same(original, active.Current);
            store.Release(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publication); await stop.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(store.Exited); Assert.Equal(0, host.ActiveOperationCount); Assert.Same(original, active.Current);
            Assert.Throws<ObjectDisposedException>(() => { _ = active.OpenAsync(); });
            Assert.Throws<ObjectDisposedException>(() => { _ = contender.OpenAsync(); });
            Assert.Equal(1, store.ReadCalls); Assert.Equal(1, store.PublishCalls); Assert.False(Directory.Exists(fixture.Root));
        }
        finally { store.Release(); }
    }

    private sealed class BlockingStore(PublishedModelImport initial) : IModelImportLedgerStore
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int ReadCalls { get; private set; }
        internal int PublishCalls { get; private set; }
        internal bool Exited { get; private set; }
        internal void Release() => _release.TrySetResult();
        public Task<PublishedModelImport?> ReadAsync(string path, Guid sourceId, CancellationToken cancellationToken = default)
        { ReadCalls++; return Task.FromResult<PublishedModelImport?>(initial); }
        public async Task<PublishedModelImport> PublishAsync(string path, Guid sourceId, long expectedGeneration, ModelImportCandidate candidate,
            ModelImportIdentityEdits? edits = null, CancellationToken cancellationToken = default)
        {
            PublishCalls++; using var registration = cancellationToken.Register(() => Cancelled.TrySetResult()); Entered.TrySetResult();
            try { await _release.Task; cancellationToken.ThrowIfCancellationRequested(); return PublishedModelImport.Prepare(candidate, initial, expectedGeneration, edits); }
            finally { Exited = true; }
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrueMoon-G1c-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Root, "nested", "model.tml");
        internal void AssertOnlyRecordAndPermanentLock()
            => Assert.Equal(new[] { Path, Path + ".write-lock" }, Directory.GetFiles(System.IO.Path.GetDirectoryName(Path)!).Order(StringComparer.Ordinal));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
