using Xunit;
using static TrueMoon.Nom.Tests.Nom3Fixtures;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomIntegratedFaultTests
{
    [Theory]
    [InlineData("zero")][InlineData("partial")][InlineData("full")][InlineData("flush-before")][InlineData("flush-after")]
    public async Task AllocationFailure_IssuesNoTicketAndReopenNeverReusesDurablyBurnedSequence(string mode)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var candidate = Candidate;
        var failure = new IOException("Allocation acknowledgement failed."); Arm(context.Io, mode, failure);
        var error = await Record.ExceptionAsync(() => owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(candidate, NewBytes)]));
        Assert.NotNull(error); Assert.IsAssignableFrom<IOException>(error); Assert.False(error is UnknownOutcomeException);
        Assert.Contains(NomFaultContext.Flatten(error), item => ReferenceEquals(item, failure)); var writes = Writes(context.Io);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReadAsync()); context.Io.Disarm(); await owner.DisposeAsync();
        Assert.Equal(writes, Writes(context.Io)); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        await AssertOld(reopened, old, candidate);
        var next = await reopened.ReserveResourcesAsync(1, [NomFaultContext.Spec(candidate, NewBytes)]);
        Assert.Equal(Whole(mode) ? 3 : 2, next.Sequence); await reopened.AbortResourceOperationAsync(next);
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(next)).Status); await AssertOld(reopened, old, candidate);
    }

    [Theory]
    [InlineData("zero")][InlineData("partial")][InlineData("full")][InlineData("flush-before")][InlineData("flush-after")]
    public async Task PendingFailure_ExposesExactTicketAndReopenResolvesAbortedWithoutTicketReuse(string mode)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var candidate = Candidate;
        var failure = new IOException("Pending operation acknowledgement failed."); Arm(context.Io, mode, failure, occurrence: 2);
        var error = await Assert.ThrowsAsync<UnknownOutcomeException>(() => owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(candidate, NewBytes)]));
        Assert.Equal(args.StoreId, error.Ticket.StoreId); Assert.Equal(2, error.Ticket.Sequence);
        Assert.Contains(NomFaultContext.Flatten(error), item => ReferenceEquals(item, failure)); var writes = Writes(context.Io);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.LookupResourceOperationAsync(error.Ticket));
        context.Io.Disarm(); await owner.DisposeAsync(); Assert.Equal(writes, Writes(context.Io)); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        var outcome = await reopened.LookupResourceOperationAsync(error.Ticket); Assert.Equal("aborted", outcome.Status); Assert.Null(outcome.Generation); Assert.Empty(outcome.Resources);
        await AssertOld(reopened, old, candidate); var next = await reopened.ReserveResourcesAsync(1, [NomFaultContext.Spec(candidate, NewBytes)]);
        Assert.Equal(3, next.Sequence); Assert.NotEqual(error.Ticket, next); await reopened.AbortResourceOperationAsync(next);
    }

    [Theory]
    [InlineData("zero")][InlineData("partial")][InlineData("full")][InlineData("flush-before")][InlineData("flush-after")]
    public async Task AbortFailure_CleansOwnedStageWithoutFurtherAppendAndReopenResolvesAborted(string mode)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var candidate = Candidate;
        var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(candidate, NewBytes)]); Stage(args, ticket, NewBytes[..1]);
        var failure = new IOException("Abort acknowledgement failed."); Arm(context.Io, mode, failure);
        var error = await Assert.ThrowsAsync<UnknownOutcomeException>(() => owner.AbortResourceOperationAsync(ticket)); Assert.Equal(ticket, error.Ticket);
        Assert.Contains(NomFaultContext.Flatten(error), item => ReferenceEquals(item, failure)); Assert.False(Directory.Exists(StagePath(args, ticket)));
        var writes = Writes(context.Io); context.Io.Disarm(); await owner.DisposeAsync(); Assert.Equal(writes, Writes(context.Io)); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status); await AssertOld(reopened, old, candidate);
        var next = await reopened.ReserveResourcesAsync(1, [NomFaultContext.Spec(candidate, NewBytes)]); Assert.Equal(3, next.Sequence); await reopened.AbortResourceOperationAsync(next);
    }

    [Theory]
    [InlineData("zero")][InlineData("partial")][InlineData("full")][InlineData("flush-before")][InlineData("flush-after")]
    public async Task PruneFailure_ReopenSelectsWholeRetentionFloorAndPreservesEveryResource(string mode)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (first, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes);
        var pending = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(Candidate, NewBytes)]); await owner.AbortResourceOperationAsync(pending);
        var keptAsset = NomTestDirectory.Record("kept"); var (kept, keptVersion) = await Publish(owner, keptAsset, NewBytes);
        var failure = new IOException("Prune acknowledgement failed."); Arm(context.Io, mode, failure);
        var error = await Record.ExceptionAsync(() => owner.PruneResourceOperationsAsync(pending.Sequence)); Assert.NotNull(error);
        Assert.Contains(NomFaultContext.Flatten(error), item => ReferenceEquals(item, failure)); var writes = Writes(context.Io);
        context.Io.Disarm(); await owner.DisposeAsync(); Assert.Equal(writes, Writes(context.Io)); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        if (Whole(mode))
        { await Assert.ThrowsAsync<ExpiredOperationException>(() => reopened.LookupResourceOperationAsync(first)); await Assert.ThrowsAsync<ExpiredOperationException>(() => reopened.LookupResourceOperationAsync(pending)); }
        else
        { Assert.Equal("committed", (await reopened.LookupResourceOperationAsync(first)).Status); Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(pending)).Status); }
        Assert.Equal("committed", (await reopened.LookupResourceOperationAsync(kept)).Status); Assert.Equal(2, reopened.ContentGeneration);
        Assert.Equal(2, (await reopened.ReadAsync()).Length); await NomFaultContext.AssertBytes(reopened, old, OldBytes); await NomFaultContext.AssertBytes(reopened, keptVersion, NewBytes);
        var next = await reopened.ReserveResourcesAsync(2, [NomFaultContext.Spec(Candidate, NewBytes)]); Assert.Equal(4, next.Sequence); await reopened.AbortResourceOperationAsync(next);
    }

    [Theory]
    [InlineData("zero")][InlineData("partial")][InlineData("full")][InlineData("flush-before")][InlineData("flush-after")]
    public async Task RecoveryAbortFailure_ReleasesStageAndHandlesThenRetryPreservesOldContent(string mode)
    {
        using var directory = new NomTestDirectory(); var seed = await SeedPendingRoot(directory); var io = new NomFaultIo();
        await using var factory = new CatalogFactory(1, io); var failure = new IOException("Startup recovery abort failed."); Arm(io, mode, failure);
        var error = Record.Exception(() => factory.Create(new(seed.Arguments.Root, seed.Arguments.ProjectId, seed.Arguments.StoreId, false)));
        Assert.NotNull(error); Assert.Contains(NomFaultContext.Flatten(error), item => ReferenceEquals(item, failure));
        Assert.False(Directory.Exists(StagePath(seed.Arguments, seed.Pending))); Assert.Equal(0, factory.Count); AssertClosed(io, seed.Arguments);
        io.Disarm(); await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(seed.Arguments with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(seed.Pending)).Status); await AssertOld(reopened, seed.Original, seed.Candidate);
        var next = await reopened.ReserveResourcesAsync(1, [NomFaultContext.Spec(seed.Candidate, NewBytes)]); Assert.Equal(3, next.Sequence); await reopened.AbortResourceOperationAsync(next);
    }

    [Fact]
    public async Task PendingReadPoison_StopCleansOnlyOwnedStageAndReopenAbortsWithoutLeakingRoot()
    {
        await using var context = new NomFaultContext(); context.Io.UseReadFaultStreams = true; var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var (_, old) = await Publish(owner, NomTestDirectory.Record("old"), OldBytes); var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(Candidate, NewBytes)]);
        Stage(args, ticket, NewBytes[..1]); var failure = new IOException("Catalog read poisoned pending owner."); context.Io.Arm("read-journal", failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => owner.FindResourceAsync(old.AssetId))); var writes = Writes(context.Io);
        await context.Host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(writes, Writes(context.Io));
        Assert.True(owner.IsClosed); Assert.True(context.Host.IsReleased); Assert.Equal(0, context.Host.ActiveStoreCount);
        Assert.False(Directory.Exists(StagePath(args, ticket))); AssertClosed(context.Io, args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status); await AssertOld(reopened, old, Candidate);
    }
}
