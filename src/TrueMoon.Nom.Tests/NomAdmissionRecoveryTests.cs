using Xunit;
using static TrueMoon.Nom.Tests.Nom3Fixtures;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomAdmissionRecoveryTests
{
    private static NomFormatLimits ExactLimits => new()
    {
        MaximumKeyBytes = 40, MaximumValueBytes = 195, MaximumPayloadBytes = 987, MaximumMutations = 8,
        MaximumSegmentBytes = 2234, MaximumReplayBatches = 4, MaximumIndexKeys = 8, MaximumIndexBytes = 968
    };

    [Theory]
    [InlineData("segment")][InlineData("replay")][InlineData("keys")][InlineData("index")]
    [InlineData("payload")][InlineData("mutations")][InlineData("value")][InlineData("key-length")]
    public async Task ReservationCapacity_OneBelowRequiredBound_RejectsBeforeSequenceAllocationAndStillCloses(string quota)
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); var io = new NomFaultIo(); await using var factory = new CatalogFactory(1, io);
        var limits = quota switch
        {
            "segment" => ExactLimits with { MaximumSegmentBytes = 2233 }, "replay" => ExactLimits with { MaximumReplayBatches = 3 },
            "keys" => ExactLimits with { MaximumIndexKeys = 7 }, "index" => ExactLimits with { MaximumIndexBytes = 967 },
            "payload" => ExactLimits with { MaximumPayloadBytes = 986 }, "mutations" => ExactLimits with { MaximumMutations = 7 },
            "value" => ExactLimits with { MaximumValueBytes = 194 }, _ => ExactLimits with { MaximumKeyBytes = 39 }
        };
        var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true, StorageLimits: limits));
        Assert.Equal(79, CatalogCodec.Encode(Candidate).Length); Assert.Equal(432, new FileInfo(NomKvTestDirectory.Journal(args.Root)).Length); io.Events.Clear();
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(Candidate, NewBytes)]));
        Assert.Empty(io.Events); Assert.Equal(0, owner.ActiveResourceUsers); Assert.Equal(0, owner.Header.Generation); Assert.Empty(await owner.ReadAsync());
        await owner.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); Assert.True(owner.IsClosed); Assert.Equal(0, factory.Count); AssertClosed(io, args);
        using var rawFactory = new NomKvStoreFactory(); using var raw = rawFactory.Open(new(args.Root, args.ProjectId, args.StoreId));
        var catalog = new NomCatalog(raw); Assert.Equal(1, catalog.Control.Next); Assert.Equal(0, catalog.Control.Floor); Assert.Empty(catalog.Operations());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ReservationCapacity_ExactHeadroom_AlwaysPermitsAbortOrPublicationAndSingleDrain(bool publish)
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); var io = new NomFaultIo(); await using var factory = new CatalogFactory(1, io);
        var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true, StorageLimits: ExactLimits));
        var ticket = await owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(Candidate, NewBytes)]); Assert.Equal(1, ticket.Sequence); Assert.Equal(1, owner.ActiveResourceUsers);
        var writes = Writes(io);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.AddAsync(0, [NomTestDirectory.Record("must-not-steal-headroom")]));
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.PruneResourceOperationsAsync(0)); Assert.Equal(writes, Writes(io));
        AssetResourceVersion? version = null;
        if (publish)
        {
            using var input = new ShortSource(NewBytes); var receipt = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [Candidate.Id] = input });
            version = Assert.Single(receipt.Resources); Assert.Equal(1, receipt.Generation); Assert.False(input.WasDisposed);
        }
        else await owner.AbortResourceOperationAsync(ticket);
        Assert.Equal(0, owner.ActiveResourceUsers); await owner.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); AssertClosed(io, args);
        await using var other = new CatalogFactory(); await using var reopened = other.Create(new(args.Root, args.ProjectId, args.StoreId, false));
        var outcome = await reopened.LookupResourceOperationAsync(ticket); Assert.Equal(publish ? "committed" : "aborted", outcome.Status); Assert.Equal(publish ? 1 : 0, reopened.Header.Generation);
        Assert.Equal(publish ? 1 : 0, (await reopened.ReadAsync()).Length);
        if (publish)
        {
            Assert.Equal(version, await reopened.FindResourceAsync(Candidate.Id)); await using var stream = await reopened.OpenResourceAsync(version!);
            using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes); Assert.Equal(NewBytes, bytes.ToArray());
        }
        var next = await reopened.ReserveResourcesAsync(reopened.Header.Generation, [NomFaultContext.Spec(NomTestDirectory.Record("next"), NewBytes)]);
        Assert.Equal(2, next.Sequence); await reopened.AbortResourceOperationAsync(next);
    }

    [Theory]
    [InlineData(1)][InlineData(2)]
    public async Task StopDuringReserving_OneStopAttemptAbortsTicketAfterEnteredIoAndReleasesRoot(int writeOrdinal)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var gate = new NomFaultGate(); var calls = 0;
        context.Io.BeforeJournalWrite = () =>
        {
            if (Interlocked.Increment(ref calls) != writeOrdinal) return;
            gate.Entered.TrySetResult(); if (!gate.Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Reservation IO gate.");
        };
        var reservation = Task.Run(() => owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(Candidate, NewBytes)])); Task? stop = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); stop = context.Host.StopAsync();
            Assert.False(stop.IsCompleted); Assert.False(owner.IsClosed); Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        }
        finally { gate.Release.Set(); }
        var ticket = await reservation.WaitAsync(TimeSpan.FromSeconds(5)); await stop!.WaitAsync(TimeSpan.FromSeconds(5)); context.Io.Disarm();
        Assert.True(owner.IsClosed); Assert.True(context.Host.IsReleased); Assert.Equal(0, context.Host.ActiveStoreCount); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status); await AssertOld(reopened, old, Candidate);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CancelAtCommitFence_ResolvesAbortedBeforeAndCommittedAfterWithoutClosingBorrowedInput(bool afterCommit)
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); await using var factory = new CatalogFactory();
        var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true)); var oldAsset = NomTestDirectory.Record("old");
        var oldTicket = await owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(oldAsset, OldBytes)]); using var oldInput = new ShortSource(OldBytes);
        var old = Assert.Single((await owner.PublishResourcesAsync(oldTicket, new Dictionary<Guid, Stream> { [oldAsset.Id] = oldInput })).Resources);
        var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(Candidate, NewBytes)]); using var input = new ShortSource(NewBytes); using var cancel = new CancellationTokenSource();
        var gate = new ResourceGate(); var proof = afterCommit ? new ResourceProofOptions(AfterCommit: gate) : new ResourceProofOptions(BeforeCommit: gate);
        var publishing = Task.Run(() => owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [Candidate.Id] = input }, cancel.Token, proof));
        try { await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancel.Cancel(); } finally { gate.Release.Set(); }
        if (afterCommit) { var receipt = await publishing; Assert.Equal(2, receipt.Generation); Assert.Equal(Candidate.Id, Assert.Single(receipt.Resources).AssetId); }
        else Assert.True((await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing)).CancellationToken.IsCancellationRequested);
        Assert.False(input.WasDisposed); Assert.Equal(afterCommit ? 2 : 1, owner.Header.Generation); await owner.DisposeAsync();
        await using var reopened = factory.Create(new(args.Root, args.ProjectId, args.StoreId, false));
        Assert.Equal(afterCommit ? "committed" : "aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status);
        await using var oldStream = await reopened.OpenResourceAsync(old); using var oldBytes = new MemoryStream(); await oldStream.CopyToAsync(oldBytes); Assert.Equal(OldBytes, oldBytes.ToArray());
        Assert.Equal(afterCommit, await reopened.FindResourceAsync(Candidate.Id) != null);
    }

    [Fact]
    public async Task LostFlushAcknowledgement_StopDoesNotAppendAndReopenKeepsWholeCommittedContent()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(Candidate, NewBytes)]);
        var failure = new IOException("Flush completed but acknowledgement was lost."); context.Io.Arm("flush-journal", failure, after: true); using var input = new ShortSource(NewBytes);
        var error = await Assert.ThrowsAsync<UnknownOutcomeException>(() => owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [Candidate.Id] = input }));
        Assert.Equal(ticket, error.Ticket); Assert.Contains(NomFaultContext.Flatten(error), cause => ReferenceEquals(cause, failure)); Assert.False(input.WasDisposed);
        var writes = Writes(context.Io); await context.Host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(writes, Writes(context.Io)); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(2, reopened.ContentGeneration); var outcome = await reopened.LookupResourceOperationAsync(ticket); Assert.Equal("committed", outcome.Status);
        await NomFaultContext.AssertBytes(reopened, old, OldBytes); await NomFaultContext.AssertBytes(reopened, Assert.Single(outcome.Resources), NewBytes);
    }

    [Fact]
    public async Task AbortMetadataAndCleanupFailure_PreserveBothCausesAndRetainRootUntilSingleRetry()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(Candidate, NewBytes)]); Stage(args, ticket, NewBytes[..1]);
        var metadata = new IOException("Abort metadata failed."); var cleanup = new IOException("Owned staging cleanup failed."); Arm(context.Io, "zero", metadata);
        context.Io.Arm("delete-file", cleanup, persistent: true); var error = await Assert.ThrowsAsync<AggregateException>(() => owner.AbortResourceOperationAsync(ticket));
        Assert.Contains(NomFaultContext.Flatten(error), cause => ReferenceEquals(cause, metadata)); Assert.Contains(NomFaultContext.Flatten(error), cause => ReferenceEquals(cause, cleanup));
        var writes = Writes(context.Io); await NomFaultContext.AssertError(cleanup, () => context.Host.StopAsync());
        Assert.Equal(writes, Writes(context.Io)); Assert.False(owner.IsClosed); Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        context.Io.Disarm(); await context.Host.StopAsync(); AssertClosed(context.Io, args); Assert.True(owner.IsClosed); Assert.True(context.Host.IsReleased);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status); await AssertOld(reopened, old, Candidate);
    }

    [Theory]
    [InlineData("zero")][InlineData("full")]
    public async Task RecoveryMetadataAndCloseFailure_PreserveBothCausesAndOwnedHandlesUntilDrainRetry(string mode)
    {
        using var directory = new NomTestDirectory(); var seed = await SeedPendingRoot(directory); var io = new NomFaultIo(); await using var factory = new CatalogFactory(1, io);
        var metadata = new IOException("Recovery metadata failed."); var close = new IOException("Recovery close failed."); Arm(io, mode, metadata); io.Arm("close-journal-before", close, persistent: true);
        try
        {
            var error = Assert.Throws<AggregateException>(() => factory.Create(new(seed.Arguments.Root, seed.Arguments.ProjectId, seed.Arguments.StoreId, false)));
            Assert.Contains(NomFaultContext.Flatten(error), cause => ReferenceEquals(cause, metadata)); Assert.Contains(NomFaultContext.Flatten(error), cause => ReferenceEquals(cause, close));
            Assert.False(Directory.Exists(StagePath(seed.Arguments, seed.Pending))); Assert.Equal(1, factory.Count); NomTestDirectory.AssertRootLocked(seed.Arguments);
            var writes = Writes(io); await NomFaultContext.AssertError(close, () => factory.DisposeAsync().AsTask()); Assert.Equal(writes, Writes(io));
        }
        finally { io.Disarm(); await factory.DisposeAsync(); }
        Assert.Equal(0, factory.Count); AssertClosed(io, seed.Arguments);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(seed.Arguments with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(seed.Pending)).Status); await AssertOld(reopened, seed.Original, seed.Candidate);
    }
}
