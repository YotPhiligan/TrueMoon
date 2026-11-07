using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderContentCatalogTests
{
    private static readonly Guid Project = new("10203040-5060-7080-90a0-b0c0d0e0f001");
    private static readonly Guid Generation = new("10203040-5060-7080-90a0-b0c0d0e0f002");
    private static readonly Guid MeshId = new("10203040-5060-7080-90a0-b0c0d0e0f003");
    private static readonly Guid SlotId = new("10203040-5060-7080-90a0-b0c0d0e0f004");
    private static readonly RenderContentHash Build = RenderContentHash.Compute("catalog-tests"u8);

    [Fact]
    public void EmptyScope_HasIndependentCountedLeasesAndIdempotentAliasRelease()
    {
        using var catalog = new RenderContentCatalog();
        Counts(catalog, 0, 0, 0, 0);
        Assert.NotEqual(Guid.Empty, catalog.Id);
        Assert.False(catalog.IsReleased);
        var source = Manifest([]);
        using var first = catalog.Create(source);
        using var second = catalog.Acquire(source);
        var alias = first;
        Assert.NotSame(first, second);
        Assert.Same(first.Manifest, second.Manifest);
        Assert.Empty(second.Manifest.Entries);
        Assert.Equal(catalog.Id, first.CatalogId);
        Assert.False(first.IsReleased);
        Counts(catalog, 0, 0, 1, 2);

        first.Dispose();
        alias.Dispose();
        Assert.True(first.IsReleased);
        Assert.Throws<ObjectDisposedException>(() => first.Manifest);
        Counts(catalog, 0, 0, 1, 1);
        Assert.Equal(source.Digest, second.Manifest.Digest);
        second.Dispose();
        Counts(catalog, 0, 0, 0, 0);
    }

    [Fact]
    public void ExactKey_IsCanonicalizedAcrossScopesUntilTheFinalLeaseReleases()
    {
        using var catalog = new RenderContentCatalog();
        var original = Triangle();
        var equivalentCopy = Triangle();
        Assert.NotSame(original, equivalentCopy);
        Assert.Equal(original.Revision, equivalentCopy.Revision);
        var source = Manifest([Entry(original)]);
        var equivalentScope = Manifest([Entry(equivalentCopy)]);
        var nextSource = Manifest([Entry(equivalentCopy)], generation: SlotId);
        using var first = catalog.Acquire(source);
        using var repeated = catalog.Acquire(equivalentScope);
        using var next = catalog.Acquire(nextSource);
        Assert.NotSame(source, equivalentScope);
        Assert.Same(first.Manifest, repeated.Manifest);
        Assert.NotSame(first.Manifest, next.Manifest);
        Assert.Same(original, next.Manifest.Resolve(Entry(original).Key).Definition);
        Assert.Equal(SlotId, next.Manifest.Generation);
        Assert.Equal(source.Digest, next.Manifest.Digest);
        Counts(catalog, 1, 72, 2, 3);

        first.Dispose();
        Counts(catalog, 1, 72, 2, 2);
        repeated.Dispose();
        Counts(catalog, 1, 72, 1, 1);
        Assert.Equal(Vector3.UnitX, Assert.IsType<MeshDefinition>(next.Manifest.Entries[0].Definition).Vertices[1]);
        next.Dispose();
        Counts(catalog, 0, 0, 0, 0);
    }

    [Fact]
    public void DistinctLogicalKeys_ShareOnlyTheSamePhysicalDefinitionObject()
    {
        using var catalog = new RenderContentCatalog();
        var shared = Triangle();
        var equalCopy = Triangle();
        using var first = catalog.Acquire(Manifest([Entry(shared)]));
        using var aliasKey = catalog.Acquire(Manifest([Entry(shared, SlotId)]));
        Assert.NotEqual(first.Manifest.Entries[0].Key, aliasKey.Manifest.Entries[0].Key);
        Assert.Equal(first.Manifest.Entries[0].Key.Revision, aliasKey.Manifest.Entries[0].Key.Revision);
        Counts(catalog, 2, 72, 2, 2);
        using var independent = catalog.Acquire(Manifest([Entry(equalCopy, Generation)]));
        Assert.NotSame(shared, independent.Manifest.Entries[0].Definition);
        Counts(catalog, 3, 144, 3, 3);

        first.Dispose();
        Counts(catalog, 2, 144, 2, 2);
        aliasKey.Dispose();
        Counts(catalog, 1, 72, 1, 1);
        independent.Dispose();
        Counts(catalog, 0, 0, 0, 0);
    }

    [Fact]
    public void SameDefinitionWithinOneCandidate_ChargesItsPhysicalBufferOnceBeforeAdmission()
    {
        var shared = Triangle();
        var source = Manifest([Entry(shared), Entry(shared, SlotId)]);
        using var catalog = new RenderContentCatalog(new() { MaximumCpuBytes = 72 });
        using var lease = catalog.Acquire(source);
        Counts(catalog, 2, 72, 1, 1);
        Assert.Equal(2, lease.Manifest.Entries.Length);
        Assert.Same(lease.Manifest.Entries[0].Definition, lease.Manifest.Entries[1].Definition);
        lease.Dispose();
        Counts(catalog, 0, 0, 0, 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DependencyClosure_AccountsTypedBuffersRatherThanCanonicalBytesOrLeaseMultiplicity(int slotCount)
    {
        var mesh = slotCount == 1 ? Triangle() : new MeshDefinition([Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            [0u, 1u, 2u, 0u, 2u, 1u], [new(SlotId, 0, 3), new(Project, 3, 3)], new(Vector3.Zero, new(1, 1, 0)));
        var material = new OpaqueUnlitMaterialDefinition(new(.25f, .5f, 1));
        var meshEntry = Entry(mesh);
        var materialEntry = Entry(material, Generation);
        MaterialSlotBinding[] slots = slotCount == 1
            ? [new(SlotId, materialEntry.Key)] : [new(SlotId, materialEntry.Key), new(Project, materialEntry.Key)];
        var bindings = new MaterialBindingSetDefinition(meshEntry.Key, slots);
        var source = Manifest([meshEntry, materialEntry, Entry(bindings, SlotId)]);
        var meshBytes = slotCount == 1 ? 72 : 108;
        Assert.Equal(meshBytes, RenderResourceAccounting.ResidentBytes(mesh));
        Assert.Equal(0, RenderResourceAccounting.ResidentBytes(material));
        Assert.Equal(slotCount * Unsafe.SizeOf<MaterialSlotBinding>(), RenderResourceAccounting.ResidentBytes(bindings));
        Assert.Equal(slotCount == 1 ? 132 : 168, RenderContentCodec.Encode(mesh).Length);
        Assert.Equal(slotCount == 1 ? 200 : 300, RenderContentCodec.Encode(bindings).Length);
        var physicalBytes = meshBytes + (long)slotCount * Unsafe.SizeOf<MaterialSlotBinding>();
        using var catalog = new RenderContentCatalog(new() { MaximumCpuBytes = physicalBytes });
        using var first = catalog.Acquire(source);
        using var second = catalog.Acquire(source);
        Counts(catalog, 3, physicalBytes, 1, 2);
        first.Dispose();
        Counts(catalog, 3, physicalBytes, 1, 1);
        Assert.Equal(materialEntry.Key, Assert.IsType<MaterialBindingSetDefinition>(second.Manifest.Resolve(SlotId,
            RenderContentProfile.BindingSetKindId).Definition).Bindings[0].Material);
        second.Dispose();
        Counts(catalog, 0, 0, 0, 0);
        Assert.Throws<ArgumentNullException>(() => RenderResourceAccounting.ResidentBytes(null!));
    }

    [Theory]
    [InlineData("project")]
    [InlineData("build")]
    [InlineData("generation")]
    [InlineData("digest")]
    public void FrozenScopeIdentity_PinsEveryDimensionWithoutRetargetingOldContent(string changed)
    {
        var original = Triangle();
        var candidate = changed == "digest" ? Triangle(2) : original;
        var candidateProject = changed == "project" ? SlotId : Project;
        var candidateBuild = changed == "build" ? RenderContentHash.Compute("next-build"u8) : Build;
        var candidateGeneration = changed == "generation" ? SlotId : Generation;
        var source = Manifest([Entry(original)]);
        var candidateSource = new RenderContentManifest(candidateProject, candidateBuild, candidateGeneration,
            [new(candidate.CreateKey(candidateProject, MeshId), candidate)]);
        using var catalog = new RenderContentCatalog();
        using var first = catalog.Acquire(source);
        using var second = catalog.Acquire(candidateSource);
        Assert.NotSame(first.Manifest, second.Manifest);
        Assert.Equal(Project, first.Manifest.ProjectId);
        Assert.Equal(Build, first.Manifest.BuildId);
        Assert.Equal(Generation, first.Manifest.Generation);
        Assert.Equal(source.Digest, first.Manifest.Digest);
        Assert.Equal(Vector3.UnitX, Assert.IsType<MeshDefinition>(first.Manifest.Entries[0].Definition).Vertices[1]);
        Assert.Equal(candidateProject, second.Manifest.ProjectId);
        Assert.Equal(candidateBuild, second.Manifest.BuildId);
        Assert.Equal(candidateGeneration, second.Manifest.Generation);
        Assert.Equal(candidateSource.Digest, second.Manifest.Digest);
        Assert.Equal(new Vector3(changed == "digest" ? 3 : 1, 0, 0),
            Assert.IsType<MeshDefinition>(second.Manifest.Entries[0].Definition).Vertices[1]);
        Counts(catalog, changed is "project" or "digest" ? 2 : 1, changed == "digest" ? 144 : 72, 2, 2);
    }

    [Fact]
    public void CatalogOwners_KeepIndependentIdentitiesAndAccountingForTheSameSource()
    {
        using var firstOwner = new RenderContentCatalog();
        using var secondOwner = new RenderContentCatalog();
        var source = Manifest([Entry(Triangle())]);
        using var first = firstOwner.Acquire(source);
        using var second = secondOwner.Acquire(source);
        Assert.NotEqual(firstOwner.Id, secondOwner.Id);
        Assert.Equal(firstOwner.Id, first.CatalogId);
        Assert.Equal(secondOwner.Id, second.CatalogId);
        first.Dispose();
        Counts(firstOwner, 0, 0, 0, 0);
        Counts(secondOwner, 1, 72, 1, 1);
        Assert.Equal(source.Digest, second.Manifest.Digest);
    }

    [Fact]
    public void CandidatePeakFailure_PreservesOldLeaseAndAllowsRetryAfterItsRelease()
    {
        using var catalog = new RenderContentCatalog(new() { MaximumCpuBytes = 143 });
        var original = Triangle();
        var changed = Triangle(2);
        var source = Manifest([Entry(original)]);
        var candidate = Manifest([Entry(changed)], generation: SlotId);
        using var oldLease = catalog.Acquire(source);
        Error(RenderResourceError.LimitExceeded, () => catalog.Acquire(candidate));
        Counts(catalog, 1, 72, 1, 1);
        Assert.Equal(source.Digest, oldLease.Manifest.Digest);
        Assert.Equal(original.Revision, oldLease.Manifest.Entries[0].Key.Revision);
        Assert.Equal(Vector3.UnitX, Assert.IsType<MeshDefinition>(oldLease.Manifest.Entries[0].Definition).Vertices[1]);

        oldLease.Dispose();
        Counts(catalog, 0, 0, 0, 0);
        using var retry = catalog.Acquire(candidate);
        Assert.Equal(candidate.Digest, retry.Manifest.Digest);
        Assert.Equal(changed.Revision, retry.Manifest.Entries[0].Key.Revision);
        Assert.Equal(new Vector3(3, 0, 0), Assert.IsType<MeshDefinition>(retry.Manifest.Entries[0].Definition).Vertices[1]);
        Counts(catalog, 1, 72, 1, 1);
    }

    [Theory]
    [InlineData(71, false)]
    [InlineData(72, true)]
    [InlineData(73, true)]
    public void PhysicalBytes_AdmitImmediatelyBeforeAndAtLimitAndRejectOneByteBeyond(long limit, bool admitted)
    {
        using var catalog = new RenderContentCatalog(new() { MaximumCpuBytes = limit });
        var source = Manifest([Entry(Triangle())]);
        if (!admitted)
        {
            Error(RenderResourceError.LimitExceeded, () => catalog.Acquire(source));
            Counts(catalog, 0, 0, 0, 0);
            return;
        }
        using var lease = catalog.Acquire(source);
        Counts(catalog, 1, 72, 1, 1);
        Assert.Equal(source.Digest, lease.Manifest.Digest);
    }

    [Fact]
    public void RevisionCount_AdmitsBeforeAndAtLimitRejectsNextAtomicallyAndReleasesForRetry()
    {
        using var catalog = new RenderContentCatalog(new() { MaximumCpuRevisions = 3 });
        var material = new OpaqueUnlitMaterialDefinition(Vector3.One);
        using var first = catalog.Acquire(Manifest([Entry(material, MeshId), Entry(material, SlotId)]));
        Counts(catalog, 2, 0, 1, 1);
        using var atLimit = catalog.Acquire(Manifest([Entry(material, Generation)]));
        Counts(catalog, 3, 0, 2, 2);
        var candidate = Manifest([Entry(material, Project)]);
        Error(RenderResourceError.LimitExceeded, () => catalog.Acquire(candidate));
        Counts(catalog, 3, 0, 2, 2);
        Assert.Equal(Vector3.One, Assert.IsType<OpaqueUnlitMaterialDefinition>(first.Manifest.Entries[0].Definition).BaseColorSrgb);
        atLimit.Dispose();
        using var retry = catalog.Acquire(candidate);
        Counts(catalog, 3, 0, 2, 2);
        Assert.Equal(Project, Assert.Single(retry.Manifest.Entries).Key.AssetId);
    }

    [Fact]
    public void ScopeCount_AdmitsBeforeAndAtLimitRejectsNextAtomicallyAndReleasesForRetry()
    {
        using var catalog = new RenderContentCatalog(new() { MaximumCpuScopes = 2 });
        using var first = catalog.Acquire(Manifest([]));
        Counts(catalog, 0, 0, 1, 1);
        using var second = catalog.Acquire(Manifest([], generation: SlotId));
        Counts(catalog, 0, 0, 2, 2);
        var candidate = Manifest([], generation: MeshId);
        Error(RenderResourceError.LimitExceeded, () => catalog.Acquire(candidate));
        Counts(catalog, 0, 0, 2, 2);
        second.Dispose();
        using var retry = catalog.Acquire(candidate);
        Counts(catalog, 0, 0, 2, 2);
        Assert.Equal(MeshId, retry.Manifest.Generation);
    }

    [Fact]
    public void LeaseCount_BoundsRepeatedAcquisitionsEvenWhenScopeAndBuffersAreShared()
    {
        using var catalog = new RenderContentCatalog(new() { MaximumCpuLeases = 2 });
        var source = Manifest([Entry(Triangle())]);
        using var first = catalog.Acquire(source);
        Counts(catalog, 1, 72, 1, 1);
        using var second = catalog.Acquire(source);
        Counts(catalog, 1, 72, 1, 2);
        Error(RenderResourceError.LimitExceeded, () => catalog.Acquire(source));
        Counts(catalog, 1, 72, 1, 2);
        first.Dispose();
        using var retry = catalog.Acquire(source);
        Assert.Same(second.Manifest, retry.Manifest);
        Counts(catalog, 1, 72, 1, 2);
    }

    [Theory]
    [InlineData("revisions")]
    [InlineData("bytes")]
    [InlineData("scopes")]
    [InlineData("leases")]
    public void Constructor_CopiesCpuOptionsBeforeCallerMutatesThem(string field)
    {
        var options = new RenderResourceOptions();
        if (field == "revisions") options.MaximumCpuRevisions = 1;
        if (field == "bytes") options.MaximumCpuBytes = 72;
        if (field == "scopes") options.MaximumCpuScopes = 1;
        if (field == "leases") options.MaximumCpuLeases = 1;
        using var catalog = new RenderContentCatalog(options);
        options.MaximumCpuRevisions = 2;
        options.MaximumCpuBytes = 144;
        options.MaximumCpuScopes = 2;
        options.MaximumCpuLeases = 2;
        var original = Triangle();
        var source = Manifest([Entry(original)]);
        using var first = catalog.Acquire(source);
        var next = field switch
        {
            "revisions" => Manifest([Entry(original, SlotId)]),
            "bytes" => Manifest([Entry(Triangle(2))]),
            "scopes" => Manifest([], generation: SlotId),
            _ => source
        };
        Error(RenderResourceError.LimitExceeded, () => catalog.Acquire(next));
        Counts(catalog, 1, 72, 1, 1);
    }

    [Fact]
    public void Dispose_ClosesAdmissionButRetainsLiveScopeUntilReleaseAndExplicitRetry()
    {
        var catalog = new RenderContentCatalog();
        var source = Manifest([Entry(Triangle())]);
        using var lease = catalog.Acquire(source);
        Assert.Throws<InvalidOperationException>(() => catalog.Dispose());
        Assert.False(catalog.IsReleased);
        Counts(catalog, 1, 72, 1, 1, closed: true);
        Error(RenderResourceError.Closed, () => catalog.Acquire(source));
        Assert.Equal(source.Digest, lease.Manifest.Digest);
        Assert.Equal(Vector3.UnitY, Assert.IsType<MeshDefinition>(lease.Manifest.Entries[0].Definition).Vertices[2]);
        lease.Dispose();
        Counts(catalog, 0, 0, 0, 0, closed: true);
        Assert.False(catalog.IsReleased);
        catalog.Dispose();
        catalog.Dispose();
        Assert.True(catalog.IsReleased);
        Error(RenderResourceError.Closed, () => catalog.Create(source));
        Counts(catalog, 0, 0, 0, 0, closed: true);
    }

    [Fact]
    public void Acquire_RejectsNullWithoutPublishingAnyOwnership()
    {
        using var catalog = new RenderContentCatalog();
        Assert.Throws<ArgumentNullException>(() => catalog.Acquire(null!));
        Assert.Throws<ArgumentNullException>(() => catalog.Create(null!));
        Counts(catalog, 0, 0, 0, 0);
    }

    [Fact]
    public async Task ConcurrentAcquisitions_KeepOnePhysicalScopeAndReleaseEveryExclusiveLeaseOnce()
    {
        using var catalog = new RenderContentCatalog();
        var source = Manifest([Entry(Triangle())]);
        var leases = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => catalog.Acquire(source))));
        try
        {
            Assert.Equal(32, leases.Distinct().Count());
            Counts(catalog, 1, 72, 1, 32);
            Assert.All(leases, lease => Assert.Equal(source.Digest, lease.Manifest.Digest));
        }
        finally
        {
            Parallel.ForEach(leases, lease => { lease.Dispose(); lease.Dispose(); });
        }
        Counts(catalog, 0, 0, 0, 0);
    }

    private static MeshDefinition Triangle(float offset = 0) => new(
        [new(offset, 0, 0), new(offset + 1, 0, 0), new(offset, 1, 0)], [0u, 1u, 2u],
        [new(SlotId, 0, 3)], new(new(offset, 0, 0), new(offset + 1, 1, 0)));

    private static RenderAssetEntry Entry(RenderAssetDefinition definition, Guid? assetId = null)
        => new(definition.CreateKey(Project, assetId ?? MeshId), definition);

    private static RenderContentManifest Manifest(IReadOnlyList<RenderAssetEntry> entries, Guid? generation = null)
        => new(Project, Build, generation ?? Generation, entries);

    private static void Counts(RenderContentCatalog catalog, int revisions, long bytes, int scopes, int leases, bool closed = false)
        => Assert.Equal(new RenderCatalogCounts(revisions, bytes, scopes, leases, closed), catalog.Counts);

    private static void Error(RenderResourceError expected, Action action)
        => Assert.Equal(expected, Assert.Throws<RenderResourceException>(action).Error);
}
