using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomFaultTests
{
    [Theory]
    [InlineData("stage-create", 1, 0)]
    [InlineData("stage-create", 2, 0)]
    [InlineData("stage-write", 1, 0)]
    [InlineData("stage-write", 1, 1)]
    [InlineData("stage-write", 1, 3)]
    [InlineData("stage-write", 2, 1)]
    [InlineData("stage-flush", 1, 0)]
    [InlineData("stage-flush", 2, 0)]
    [InlineData("stage-durable", 1, 0)]
    [InlineData("stage-durable", 2, 0)]
    [InlineData("finalize", 1, 0)]
    [InlineData("finalize", 2, 0)]
    public async Task SinkFault_BeforePublication_AbortsWholeBatchAndKeepsOldContent(string point, int occurrence, int prefix)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments();
        await using var owner = context.Factory.Create(args);
        var oldBytes = new byte[] { 0x71, 0x72 }; var old = await NomTestDirectory.Publish(owner, oldBytes);
        var first = NomFaultContext.Record(Guid.Parse("00000000-0000-0000-0000-000000000001"), "first");
        var second = NomFaultContext.Record(Guid.Parse("00000000-0000-0000-0000-000000000002"), "second");
        var a = new byte[] { 1, 2, 3 }; var b = new byte[] { 4, 5, 6 };
        using var inputA = new ShortSource(a); using var inputB = new ShortSource(b);
        var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(first, a), NomFaultContext.Spec(second, b)]);
        // This is a deterministic sink failure, not an exhausted physical test volume.
        var failure = new IOException("Injected target disk-full boundary.", unchecked((int)0x80070070));
        context.Io.Arm(point, failure, occurrence, prefix: prefix);
        var thrown = await Assert.ThrowsAsync<IOException>(() => owner.PublishResourcesAsync(ticket,
            new Dictionary<Guid, Stream> { [first.Id] = inputA, [second.Id] = inputB }));
        Assert.Same(failure, thrown); Assert.Equal(unchecked((int)0x80070070), thrown.HResult);
        if (point == "stage-write") Assert.Equal(prefix, context.Io.PartialBytesWritten);
        Assert.False(inputA.WasDisposed); Assert.False(inputB.WasDisposed);
        Assert.Equal(0, inputA.LengthRequests + inputA.SeekRequests + inputB.LengthRequests + inputB.SeekRequests);
        Assert.Equal(1, owner.ContentGeneration); Assert.Equal(old.AssetId, Assert.Single(await owner.ReadAsync()).Id);
        Assert.Equal(old, await owner.FindResourceAsync(old.AssetId));
        Assert.Null(await owner.FindResourceAsync(first.Id)); Assert.Null(await owner.FindResourceAsync(second.Id));
        Assert.Equal("aborted", (await owner.LookupResourceOperationAsync(ticket)).Status);
        await NomFaultContext.AssertBytes(owner, old, oldBytes);
        context.Io.Disarm();
        var next = await NomTestDirectory.Publish(owner, [7, 8, 9]); Assert.Equal(2, owner.ContentGeneration);
        await owner.DisposeAsync(); NomTestDirectory.AssertRootReleased(args);
        await using var reopened = context.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(2, reopened.ContentGeneration); Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status);
        await NomFaultContext.AssertBytes(reopened, old, oldBytes); await NomFaultContext.AssertBytes(reopened, next, [7, 8, 9]);
    }

    [Fact]
    public async Task AbortCleanupFailure_PreservesBothErrorsAndRetainsReservationUntilRetry()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments();
        var owner = context.Factory.Create(args); var old = await NomTestDirectory.Publish(owner, [0x51, 0x52]);
        var asset = NomTestDirectory.Record("failed"); var ticket = await owner.ReserveResourcesAsync(1, [new AssetResourceWrite(asset)]);
        var sourceError = new IOException("Input failed."); var cleanupError = new IOException("Owned staging delete failed.");
        using var input = new NomThrowingSource(sourceError); context.Io.Arm("delete-file", cleanupError, persistent: true);
        var failure = await Assert.ThrowsAsync<AggregateException>(() => owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = input }));
        Assert.Contains(NomFaultContext.Flatten(failure), error => ReferenceEquals(error, sourceError));
        Assert.Contains(NomFaultContext.Flatten(failure), error => ReferenceEquals(error, cleanupError));
        Assert.False(input.WasDisposed); Assert.Equal(1, input.Reads);
        Assert.Equal("aborted", (await owner.LookupResourceOperationAsync(ticket)).Status); Assert.Equal(1, owner.ContentGeneration);
        Assert.Null(await owner.FindResourceAsync(asset.Id)); await NomFaultContext.AssertBytes(owner, old, [0x51, 0x52]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReserveResourcesAsync(1, [new AssetResourceWrite(NomTestDirectory.Record("cannot-reuse"))]));
        await NomFaultContext.AssertError(cleanupError, () => context.App.StopAsync());
        Assert.Equal(1, context.Host.ActiveStoreCount); Assert.False(context.Host.IsReleased); Assert.False(owner.IsClosed); NomTestDirectory.AssertRootLocked(args);
        context.Io.Disarm(); await context.Host.StopAsync();
        Assert.True(owner.IsClosed); Assert.True(context.Host.IsReleased); Assert.Equal(0, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootReleased(args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(1, reopened.ContentGeneration); Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status);
        await NomFaultContext.AssertBytes(reopened, old, [0x51, 0x52]);
    }

    [Fact]
    public async Task CommittedCleanupFailure_ReplayRetriesCleanupWithoutReadingReplacementInput()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); await using var owner = context.Factory.Create(args);
        var asset = NomTestDirectory.Record(); var bytes = new byte[] { 1, 3, 5 }; using var input = new ShortSource(bytes);
        var ticket = await owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(asset, bytes)]);
        var cleanupError = new IOException("Committed stage directory still retained."); context.Io.Arm("delete-directory", cleanupError, persistent: true);
        var receipt = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = input });
        Assert.True(receipt.CleanupPending); Assert.Equal(1, receipt.Generation); Assert.False(input.WasDisposed);
        var version = Assert.Single(receipt.Resources); Assert.Equal(version, await owner.FindResourceAsync(asset.Id));
        Assert.Equal("committed", (await owner.LookupResourceOperationAsync(ticket)).Status);
        using var replacement = new NomThrowingSource(new IOException("Committed replay must not read replacement bytes."));
        var replay = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = replacement });
        Assert.True(replay.CleanupPending); Assert.Equal(receipt.Generation, replay.Generation); Assert.Equal(receipt.Resources, replay.Resources); Assert.Equal(0, replacement.Reads);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReserveResourcesAsync(1, [new AssetResourceWrite(NomTestDirectory.Record("blocked"))]));
        context.Io.Disarm(); replay = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = replacement });
        Assert.False(replay.CleanupPending); Assert.Equal(0, replacement.Reads); Assert.False(replacement.WasDisposed); Assert.Equal(1, owner.ContentGeneration);
        var unused = await owner.ReserveResourcesAsync(1, [new AssetResourceWrite(NomTestDirectory.Record("slot-released"))]); await owner.AbortResourceOperationAsync(unused);
        await NomFaultContext.AssertBytes(owner, version, bytes);
    }

    [Fact]
    public async Task CommittedCleanupFailure_StopRetainsRootAndReopenKeepsCommittedOutcome()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var asset = NomTestDirectory.Record(); using var input = new ShortSource([2, 4, 6]);
        var ticket = await owner.ReserveResourcesAsync(0, [new AssetResourceWrite(asset)]);
        var cleanupError = new IOException("Committed cleanup blocked."); context.Io.Arm("delete-directory", cleanupError, persistent: true);
        var committed = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = input }); Assert.True(committed.CleanupPending);
        await NomFaultContext.AssertError(cleanupError, () => context.App.StopAsync());
        Assert.False(owner.IsClosed); Assert.False(context.Host.IsReleased); Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        context.Io.Disarm(); await context.Host.StopAsync(); Assert.True(owner.IsClosed); NomTestDirectory.AssertRootReleased(args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(1, reopened.ContentGeneration); Assert.Equal("committed", (await reopened.LookupResourceOperationAsync(ticket)).Status);
        await NomFaultContext.AssertBytes(reopened, Assert.Single(committed.Resources), [2, 4, 6]);
    }

    [Fact]
    public async Task ConcurrentCommittedReplay_DoesNotReleaseOriginalPublisherDuringCleanup()
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); var io = new NomFaultIo();
        await using var factory = new CatalogFactory(1, io); var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true));
        var asset = NomTestDirectory.Record(); var bytes = new byte[] { 4, 6, 8 }; using var input = new ShortSource(bytes);
        var ticket = await owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(asset, bytes)]);
        var gate = new NomFaultGate(); io.DeleteDirectoryGate = gate;
        var publishing = Task.Run(() => owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = input }));
        Task? stopping = null; AssetWriteReceipt? receipt = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("committed", (await owner.LookupResourceOperationAsync(ticket)).Status);
            var replay = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream>());
            Assert.Equal(1, replay.Generation); Assert.True(replay.CleanupPending); Assert.Single(replay.Resources);
            Assert.False(publishing.IsCompleted); Assert.Equal(1, owner.ActiveResourceUsers);
            Assert.Single(io.Events, e => e.Point == "delete-directory");
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReserveResourcesAsync(1, [new AssetResourceWrite(NomTestDirectory.Record("must-stay-busy"))]));
            stopping = factory.DisposeAsync().AsTask();
            Assert.False(stopping.IsCompleted); Assert.False(owner.IsClosed); Assert.Equal(1, owner.ActiveResourceUsers); NomTestDirectory.AssertRootLocked(args);
        }
        finally
        {
            gate.Release.Set(); receipt = await publishing.WaitAsync(TimeSpan.FromSeconds(5));
            if (stopping != null) await stopping.WaitAsync(TimeSpan.FromSeconds(5)); else await factory.DisposeAsync();
            io.DeleteDirectoryGate = null;
        }
        Assert.NotNull(receipt); Assert.Equal(1, receipt.Generation); Assert.False(receipt.CleanupPending); Assert.False(input.WasDisposed);
        Assert.True(owner.IsClosed); Assert.Equal(0, factory.Count); Assert.Equal(0, owner.ActiveResourceUsers); NomTestDirectory.AssertRootReleased(args);
        await using var other = new CatalogFactory(); await using var reopened = other.Create(new(args.Root, args.ProjectId, args.StoreId, false));
        Assert.Equal("committed", (await reopened.LookupResourceOperationAsync(ticket)).Status);
        await using var stream = await reopened.OpenResourceAsync(Assert.Single(receipt.Resources)); using var readback = new MemoryStream(); await stream.CopyToAsync(readback);
        Assert.Equal(bytes, readback.ToArray()); Assert.Equal(1, reopened.Header.Generation);
    }

    [Fact]
    public async Task UnknownOutcomeAndCleanupFailure_KeepTicketAndOwnershipUntilCloseReopen()
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); var io = new NomFaultIo();
        await using var factory = new CatalogFactory(1, io); var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true));
        var asset = NomTestDirectory.Record(); var bytes = new byte[] { 8, 6, 4 }; using var input = new ShortSource(bytes);
        var ticket = await owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(asset, bytes)]);
        var cleanupError = new IOException("Uncertain operation cleanup blocked."); io.Arm("delete-directory", cleanupError, persistent: true);
        try
        {
            var unknown = await Assert.ThrowsAsync<UnknownOutcomeException>(() => owner.PublishResourcesAsync(ticket,
                new Dictionary<Guid, Stream> { [asset.Id] = input }, proof: new ResourceProofOptions(ThrowAfterCommit: true)));
            Assert.Equal(ticket, unknown.Ticket); Assert.Contains(NomFaultContext.Flatten(unknown), error => ReferenceEquals(error, cleanupError));
            var combined = Assert.IsType<AggregateException>(unknown.InnerException); Assert.Equal(2, combined.InnerExceptions.Count);
            var original = Assert.IsType<UnknownOutcomeException>(Assert.Single(combined.InnerExceptions, error => error is UnknownOutcomeException));
            Assert.Equal(ticket, original.Ticket); Assert.Equal("Injected lost commit acknowledgement.", Assert.IsType<IOException>(original.InnerException).Message);
            Assert.Equal(1, owner.ActiveResourceUsers); Assert.False(input.WasDisposed);
            await NomFaultContext.AssertError(cleanupError, () => factory.DisposeAsync().AsTask());
            Assert.Equal(1, factory.Count); Assert.False(owner.IsClosed); NomTestDirectory.AssertRootLocked(args);
        }
        finally { io.Disarm(); await factory.DisposeAsync(); }
        Assert.True(owner.IsClosed); Assert.Equal(0, factory.Count); NomTestDirectory.AssertRootReleased(args);
        await using var other = new CatalogFactory(); await using var reopened = other.Create(new(args.Root, args.ProjectId, args.StoreId, false));
        var outcome = await reopened.LookupResourceOperationAsync(ticket); Assert.Equal("committed", outcome.Status); Assert.Equal(1, outcome.Generation);
        await using var stream = await reopened.OpenResourceAsync(Assert.Single(outcome.Resources)); using var readback = new MemoryStream(); await stream.CopyToAsync(readback);
        Assert.Equal(bytes, readback.ToArray()); Assert.Equal(1, reopened.Header.Generation);
    }

    [Theory]
    [InlineData("close-journal-before")]
    [InlineData("close-journal-after")]
    [InlineData("close-root-before")]
    [InlineData("close-root-after")]
    [InlineData("close-lock-before")]
    public async Task CloseFault_RetainsRootAndRegistryUntilSameThreadRetry(string point)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var version = await NomTestDirectory.Publish(owner, [0x41, 0x42, 0x43]); var failure = new IOException("Catalog cleanup stage failed: " + point);
        context.Io.Arm(point, failure, persistent: true);
        await NomFaultContext.AssertError(failure, () => context.App.StopAsync());
        Assert.False(owner.IsClosed); Assert.False(context.Host.IsReleased); Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.ReadAsync());
        context.Io.Disarm(); await context.Host.StopAsync();
        Assert.True(owner.IsClosed); Assert.True(context.Host.IsReleased); Assert.Equal(0, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootReleased(args);
        var closeThreads = context.Io.Events.Where(e => e.Point == point).Select(e => e.Thread).Distinct().ToArray(); Assert.Single(closeThreads);
        Assert.Equal(point.EndsWith("-after", StringComparison.Ordinal) ? 1 : 2, context.Io.Events.Count(e => e.Point == point)); Assert.All(context.Io.OpenedCatalogFiles, item => Assert.True(item.Handle.IsClosed));
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(1, reopened.ContentGeneration); Assert.Equal(version, await reopened.FindResourceAsync(version.AssetId)); await NomFaultContext.AssertBytes(reopened, version, [0x41, 0x42, 0x43]);
    }

    [Theory]
    [InlineData("write-journal", 0, false)]
    [InlineData("write-journal", 1, false)]
    [InlineData("write-journal", 100, false)]
    [InlineData("write-journal", int.MaxValue, true)]
    [InlineData("flush-journal", 0, true)]
    public async Task CatalogCommitIoFailure_ReportsUnknownAndReopenResolvesWholeResourceBatch(string point, int prefix, bool committed)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        var oldBytes = new byte[] { 0x22, 0x33, 0x44 }; var old = await NomTestDirectory.Publish(owner, oldBytes);
        var first = NomFaultContext.Record(Guid.Parse("7cf7248a-644f-45c8-b3c5-c90876cbb24f"), "commit-a");
        var second = NomFaultContext.Record(Guid.Parse("32bdbfde-31c7-41ba-bdfc-0fa58f7e6cd0"), "commit-b");
        var a = new byte[] { 1, 2, 3 }; var b = new byte[] { 4, 5, 6 }; using var inputA = new ShortSource(a); using var inputB = new ShortSource(b);
        var ticket = await owner.ReserveResourcesAsync(1, [NomFaultContext.Spec(first, a), NomFaultContext.Spec(second, b)]);
        var failure = new IOException("Catalog target disk-full boundary.", unchecked((int)0x80070070)); context.Io.Arm(point, failure, prefix: prefix);
        var unknown = await Assert.ThrowsAsync<UnknownOutcomeException>(() => owner.PublishResourcesAsync(ticket,
            new Dictionary<Guid, Stream> { [first.Id] = inputA, [second.Id] = inputB }));
        Assert.Equal(ticket, unknown.Ticket); Assert.Contains(NomFaultContext.Flatten(unknown), error => ReferenceEquals(error, failure));
        Assert.False(inputA.WasDisposed); Assert.False(inputB.WasDisposed); Assert.False(owner.IsClosed);
        Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        context.Io.Disarm(); await owner.DisposeAsync(); NomTestDirectory.AssertRootReleased(args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        var outcome = await reopened.LookupResourceOperationAsync(ticket); Assert.Equal(committed ? "committed" : "aborted", outcome.Status);
        Assert.Equal(committed ? 2 : 1, reopened.ContentGeneration); Assert.Equal(committed ? 3 : 1, (await reopened.ReadAsync()).Length);
        Assert.Equal(old, await reopened.FindResourceAsync(old.AssetId)); await NomFaultContext.AssertBytes(reopened, old, oldBytes);
        if (committed)
        {
            Assert.Equal(2, outcome.Generation); Assert.Equal(2, outcome.Resources.Length);
            await NomFaultContext.AssertBytes(reopened, (await reopened.FindResourceAsync(first.Id))!, a);
            await NomFaultContext.AssertBytes(reopened, (await reopened.FindResourceAsync(second.Id))!, b);
        }
        else
        {
            Assert.Null(outcome.Generation); Assert.Empty(outcome.Resources);
            Assert.Null(await reopened.FindResourceAsync(first.Id)); Assert.Null(await reopened.FindResourceAsync(second.Id));
        }
    }
    [Theory]
    [InlineData("open-journal")]
    [InlineData("open-pending")]
    public async Task CatalogOpenFailure_ReleasesAlreadyOpenedFilesAndDoesNotConsumeCapacity(string point)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var failure = new IOException("Catalog stream open failed.");
        context.Io.Arm(point, failure);
        Assert.Same(failure, Assert.Throws<IOException>(() => context.Factory.Create(args)));
        Assert.Equal(0, context.Host.ActiveStoreCount); Assert.NotEmpty(context.Io.OpenedCatalogFiles);
        Assert.All(context.Io.OpenedCatalogFiles, item => Assert.True(item.Handle.IsClosed)); NomTestDirectory.AssertRootReleased(args);
        context.Io.Disarm(); await using var fresh = context.Factory.Create(context.Directory.Arguments("next")); Assert.Empty(await fresh.ReadAsync());
    }

    [Fact]
    public async Task ConcurrentCloseRetry_AfterFault_SharesOneOwnerAttemptUntilActualClose()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var owner = context.Factory.Create(args);
        await NomTestDirectory.Publish(owner, [1, 7]); var failure = new IOException("First close refused."); context.Io.Arm("close-journal-before", failure);
        await NomFaultContext.AssertError(failure, () => context.Host.StopAsync());
        Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        context.Io.Disarm(); var gate = new NomFaultGate(); context.Io.CloseGate = gate;
        Task? hostRetry = null; Task? productRetry = null;
        try
        {
            hostRetry = context.Host.StopAsync(); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            productRetry = owner.DisposeAsync().AsTask();
            Assert.False(hostRetry.IsCompleted); Assert.False(productRetry.IsCompleted); Assert.False(owner.IsClosed);
            Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        }
        finally
        {
            gate.Release.Set();
            if (hostRetry != null) await hostRetry.WaitAsync(TimeSpan.FromSeconds(5));
            if (productRetry != null) await productRetry.WaitAsync(TimeSpan.FromSeconds(5));
            context.Io.CloseGate = null;
        }
        Assert.Equal(2, context.Io.Events.Count(e => e.Point == "close-journal-before"));
        Assert.True(owner.IsClosed); Assert.True(context.Host.IsReleased); Assert.Equal(0, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootReleased(args);
    }

    [Fact]
    public async Task FailedStartupAndCloseFailure_PreserveBothCausesAndRetainedOwnerForLaterDrain()
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); var io = new NomFaultIo();
        await using var factory = new CatalogFactory(1, io); var closeError = new IOException("Startup catalog close failed."); io.Arm("close-journal-before", closeError, persistent: true);
        try
        {
            var error = Assert.Throws<AggregateException>(() => factory.Create(new(args.Root, args.ProjectId, args.StoreId, true,
                FailAfterOpen: true, DrainTimeout: TimeSpan.FromSeconds(2))));
            Assert.Contains(NomFaultContext.Flatten(error), failure => failure is InvalidDataException && failure.Message.Contains("Injected failed startup", StringComparison.Ordinal));
            Assert.Contains(NomFaultContext.Flatten(error), failure => ReferenceEquals(failure, closeError));
            Assert.Equal(1, factory.Count); NomTestDirectory.AssertRootLocked(args);
            var over = directory.Arguments("failed-owner-still-consumes-slot");
            Assert.Throws<InvalidOperationException>(() => factory.Create(new(over.Root, over.ProjectId, over.StoreId, true)));
            Assert.False(Directory.Exists(over.Root));
        }
        finally { io.Disarm(); await factory.DisposeAsync(); }
        Assert.Equal(0, factory.Count); NomTestDirectory.AssertRootReleased(args);
        Assert.Single(io.Events.Where(e => e.Point == "close-journal-before").Select(e => e.Thread).Distinct());
        await using var other = new CatalogFactory(); await using var reopened = other.Create(new(args.Root, args.ProjectId, args.StoreId, false));
        Assert.Equal(0, reopened.Header.Generation); Assert.Empty(await reopened.ReadAsync());
    }
}
