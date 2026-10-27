using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Diagnostics;
using Xunit;
using static TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticFixture;

namespace TrueMoon.Aether.Tests.Diagnostics;

public class ChildDiagnosticForwarderTests
{
    [Theory]
    [InlineData("no-settings")]
    [InlineData("disabled")]
    [InlineData("no-reader")]
    public async Task MissingSourceOrDisabledLaunch_CreatesNoClientAndNeverReads(string mode)
    {
        var launch = Launch();
        if (mode == "no-settings") launch = launch with { DiagnosticDelivery = null };
        if (mode == "disabled") launch.DiagnosticDelivery!.Enabled = false;
        var reader = new DeliveryReader { OnRead = (_, _) => throw new InvalidOperationException("must not read") };
        var clients = new DeliveryClients();
        await using var sender = new ChildDiagnosticForwarder(launch, mode == "no-reader" ? null : reader, clients);
        await sender.StopAsync();
        var status = sender.Read();
        Assert.False(status.Enabled); Assert.True(status.Closed); Assert.True(status.Released); Assert.True(status.DrainComplete);
        Assert.Empty(reader.Reads); Assert.Equal(0, clients.Calls); Assert.Equal(0, reader.Disposals); Assert.Equal(0, clients.Disposals);
    }

    [Fact]
    public async Task Polling_BatchesInOrderCountsInitialAndInternalSourceGapsAndSuppressesImportedRows()
    {
        var launch = Launch(); var app = Guid.NewGuid(); var reader = new DeliveryReader();
        reader.Add(Record(10, app), Record(12, app) with { Origin = new(Guid.NewGuid(), 77) }, Record(13, app));
        await using var remote = new DeliveryRemote(launch);
        var clients = new DeliveryClients();
        await using var sender = new ChildDiagnosticForwarder(launch, reader, clients);
        await UntilAsync(() => sender.Read().Accepted == 2);
        await UntilAsync(() => remote.Packets.Any(x => x.RecordCount == 0 && x.Lost == 10));
        await sender.StopAsync();
        Assert.Equal(new long[] { 10, 13 }, remote.Nonempty.SelectMany(x => JsonSerializer.Deserialize<DiagnosticRecord[]>(x.Payload)!).Select(x => x.Sequence));
        Assert.All(remote.Nonempty, x => { Assert.Equal(app, x.ApplicationId); Assert.Equal(Environment.ProcessId, x.ProcessId); Assert.Equal(launch.Identity.Nonce, x.Nonce); Assert.InRange(x.RecordCount, 1, 2); Assert.InRange(x.Payload.Length, 1, launch.DiagnosticDelivery!.MaxBatchBytes); });
        Assert.Equal((0L, 2), reader.Reads.First());
        Assert.Equal(launch.Endpoint + "-diag", clients.Endpoint); Assert.Equal(1, clients.Calls);
        var status = sender.Read(); Assert.Equal(2, status.Accepted); Assert.Equal(10, status.Lost); Assert.Equal(1, status.Suppressed);
        Assert.Equal(0, status.Unconfirmed); Assert.Equal(0, status.Failures); Assert.True(status.DrainComplete); Assert.True(status.Released);
        Assert.True(clients.Product!.Completion.IsCompletedSuccessfully); Assert.Equal(0, reader.Disposals); Assert.Equal(0, clients.Disposals);
    }

    [Fact]
    public async Task OptionsAreCopied_AndAckUsesLastPayloadSequenceRatherThanSuppressedTailCursor()
    {
        var options = Options(); var launch = Launch(options); var app = Guid.NewGuid(); var reader = new DeliveryReader();
        reader.Add(Record(1, app), Record(2, app) with { Origin = new(Guid.NewGuid(), 1) });
        await using var remote = new DeliveryRemote(launch);
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients());
        options.Enabled = false; options.MaxBatchRecords = 0; options.MaxBatchBytes = 0; options.RequestTimeout = TimeSpan.Zero;
        await UntilAsync(() => sender.Read().Accepted == 1);
        await sender.StopAsync();
        var packet = Assert.Single(remote.Nonempty);
        Assert.Equal(1, packet.RecordCount); Assert.Equal(1, Assert.Single(JsonSerializer.Deserialize<DiagnosticRecord[]>(packet.Payload)!).Sequence);
        var status = sender.Read(); Assert.True(status.Enabled); Assert.Equal(1, status.Accepted); Assert.Equal(1, status.Suppressed); Assert.Equal(0, status.Unconfirmed); Assert.Equal(0, status.Failures);
    }

    [Theory]
    [InlineData("status-negative")]
    [InlineData("status-unknown")]
    [InlineData("accepted-negative")]
    [InlineData("accepted-too-large")]
    [InlineData("rejected-negative")]
    [InlineData("sum")]
    [InlineData("rejection-with-accepted")]
    [InlineData("negative-sequence")]
    [InlineData("old-sequence")]
    [InlineData("future-sequence")]
    public async Task MalformedAck_MakesBatchUnconfirmedAndNeverReplaysIt(string change)
    {
        var launch = Launch(); var reader = new DeliveryReader(); reader.Add(Record());
        await using var remote = new DeliveryRemote(launch, (packet, _) =>
        {
            var reply = new DiagnosticDeliveryReply { Accepted = packet.RecordCount, LastSequence = packet.RecordCount == 0 ? 0 : 1 };
            if (packet.RecordCount != 0) switch (change)
            {
                case "status-negative": reply.Status = -1; break;
                case "status-unknown": reply.Status = 6; break;
                case "accepted-negative": reply.Accepted = -1; reply.Rejected = 2; break;
                case "accepted-too-large": reply.Accepted = 2; reply.Rejected = -1; break;
                case "rejected-negative": reply.Rejected = -1; break;
                case "sum": reply.Accepted = 0; break;
                case "rejection-with-accepted": reply.Status = 2; break;
                case "negative-sequence": reply.LastSequence = -1; break;
                case "old-sequence": reply.LastSequence = 0; break;
                case "future-sequence": reply.LastSequence = 2; break;
            }
            return Task.FromResult(reply);
        });
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients());
        await UntilAsync(() => sender.Read().Unconfirmed == 1);
        await sender.StopAsync();
        Assert.Single(remote.Nonempty); Assert.Equal(0, sender.Read().Accepted); Assert.Equal(0, sender.Read().Rejected);
        Assert.Equal(1, sender.Read().Unconfirmed); Assert.Equal(1, sender.Read().Failures); Assert.Equal(typeof(InvalidDataException).FullName, sender.Read().LastFailureType);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task ValidRejectionAck_IsKnownRejectedWithoutReplayOrUnconfirmedLoss(int status)
    {
        var launch = Launch(); var reader = new DeliveryReader(); reader.Add(Record(7));
        await using var remote = new DeliveryRemote(launch, (packet, _) => Task.FromResult(new DiagnosticDeliveryReply { Status = status, Rejected = packet.RecordCount, LastSequence = 2 }));
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients());
        await UntilAsync(() => sender.Read().Rejected == 1); await sender.StopAsync();
        Assert.Single(remote.Nonempty); Assert.Equal(1, sender.Read().Rejected); Assert.Equal(6, sender.Read().Lost);
        Assert.Equal(0, sender.Read().Unconfirmed); Assert.Equal(0, sender.Read().Failures); Assert.True(sender.Read().DrainComplete);
    }

    [Fact]
    public async Task OwnRequestDeadlineAndPendingGuard_BoundAnInjectedClientWithLargeCapacityAndTimeout()
    {
        // Allow the initial physical connection to reach the barrier; the injected client still waits 50s.
        var launch = Launch(); launch.DiagnosticDelivery!.RequestTimeout = TimeSpan.FromMilliseconds(500);
        var app = Guid.NewGuid(); var reader = new DeliveryReader(); reader.Add(Record(1, app));
        var entered = Signal(); var release = Signal();
        await using var remote = new DeliveryRemote(launch, async (packet, _) =>
        {
            if (packet.RecordCount != 0) { entered.TrySetResult(); await release.Task; }
            return new() { Accepted = packet.RecordCount, LastSequence = packet.RecordCount == 0 ? 0 : 1 };
        });
        var clients = new DeliveryClients(unboundedFactoryLimits: true);
        await using var sender = new ChildDiagnosticForwarder(launch, reader, clients);
        try
        {
            await entered.Task.WaitAsync(Deadline);
            await UntilAsync(() => sender.Read().Unconfirmed == 1);
            Assert.Equal(1, clients.Product!.PendingRequestCount);
            reader.Add(Record(2, app), Record(3, app));
            await UntilAsync(() => sender.Read().Lost == 2);
            Assert.Single(remote.Nonempty); Assert.Equal(1, clients.Product.PendingRequestCount);
            Assert.Equal(0, sender.Read().Accepted); Assert.Equal(1, sender.Read().Unconfirmed);
        }
        finally { release.TrySetResult(); }
        await UntilAsync(() => clients.Product!.PendingRequestCount == 0);
        await sender.StopAsync();
        Assert.Single(remote.Nonempty); Assert.Equal(2, sender.Read().Lost); Assert.Equal(1, sender.Read().Unconfirmed); Assert.True(sender.Read().Released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedAck_KeepsBoundedPayloadUnconfirmedAndDrainsWithoutReplay(bool replyAfterStop)
    {
        var options = Options(); options.MaxBatchBytes = 65536; var launch = Launch(options); var reader = new DeliveryReader();
        var app = Guid.NewGuid(); var entered = Signal(); var release = Signal(); var replied = Signal();
        await using var remote = new DeliveryRemote(launch, async (packet, _) =>
        {
            if (packet.RecordCount != 0) { entered.TrySetResult(); await release.Task; replied.TrySetResult(); }
            return new() { Accepted = packet.RecordCount, LastSequence = packet.RecordCount == 0 ? 0 : 1 };
        });
        var clients = new DeliveryClients(options);
        await using var sender = new ChildDiagnosticForwarder(launch, reader, clients);
        try
        {
            // Connect before publishing data; this test delays the acknowledgement, not startup.
            await UntilAsync(() => clients.Product!.IsConnected);
            reader.Add(SerializationRecord(launch) with { ApplicationId = app });
            await entered.Task.WaitAsync(Deadline);
            var row = Assert.Single(JsonSerializer.Deserialize<DiagnosticRecord[]>(Assert.Single(remote.Nonempty).Payload)!);
            Assert.Equal(new string('S', 128), row.Source); Assert.Equal(new string('x', 2047), row.MessageTemplate);
            Assert.Equal(4096, row.ExceptionStackTrace!.Length); Assert.Equal(new string('v', 127), row.Properties["unicode"]);
            Assert.Equal(new Dictionary<string, string?> { ["safe"] = launch.Endpoint, ["unicode"] = new string('v', 127) }, row.Properties);
            Assert.True(row.IsTruncated);

            // Keep the reply behind a barrier until the real 500ms request budget has expired.
            await UntilAsync(() => sender.Read().Unconfirmed == 1);
            Assert.Equal(1, clients.Product!.PendingRequestCount);
            reader.Add(Record(2, app), Record(3, app));
            await UntilAsync(() => sender.Read().Lost == 2);
            Assert.Single(remote.Nonempty);
            var pending = sender.Read();
            Assert.Equal(0, pending.Accepted); Assert.Equal(0, pending.Rejected); Assert.Equal(1, pending.Failures);
            Assert.Equal(typeof(TaskCanceledException).FullName, pending.LastFailureType);

            if (!replyAfterStop)
            {
                release.TrySetResult();
                await replied.Task.WaitAsync(Deadline);
                await UntilAsync(() => clients.Product.PendingRequestCount == 0);
            }
            await sender.StopAsync();
            release.TrySetResult(); await replied.Task.WaitAsync(Deadline);
            var status = sender.Read();
            Assert.Equal(0, status.Accepted); Assert.Equal(0, status.Rejected); Assert.Equal(1, status.Unconfirmed);
            Assert.Equal(2, status.Lost); Assert.Equal(1, status.Failures);
            Assert.True(status.Closed); Assert.True(status.DrainComplete); Assert.True(status.Released);
            Assert.Equal(0, clients.Product.PendingRequestCount); Assert.True(clients.Product.Completion.IsCompletedSuccessfully);
            Assert.Single(remote.Nonempty); Assert.Equal(0, reader.Disposals); Assert.Equal(0, clients.Disposals);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task OversizedRecord_IsKnownLocalLossAndEmptyHeartbeatReportsIt()
    {
        var options = Options(); options.MaxBatchBytes = 1024; var launch = Launch(options); var reader = new DeliveryReader();
        reader.Add(Record() with { MessageTemplate = new('x', 2048) });
        await using var remote = new DeliveryRemote(launch); await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(options));
        await UntilAsync(() => remote.Packets.Any(x => x.Lost == 1)); await sender.StopAsync();
        Assert.Empty(remote.Nonempty); Assert.All(remote.Packets, x => { Assert.Equal(0, x.RecordCount); Assert.Equal("[]", System.Text.Encoding.UTF8.GetString(x.Payload)); });
        Assert.Equal(1, sender.Read().Lost); Assert.Equal(0, sender.Read().Unconfirmed); Assert.Equal(0, sender.Read().Failures);
    }

    [Fact]
    public async Task BlockedBorrowedReader_TimesOutOnlyTheStopWaitThenReleasesOnRetry()
    {
        var launch = Launch(); launch.DiagnosticDelivery!.ShutdownTimeout = TimeSpan.FromMilliseconds(50);
        var entered = Signal(); using var release = new ManualResetEventSlim();
        var reader = new DeliveryReader { OnRead = (_, _) => { entered.TrySetResult(); Assert.True(release.Wait(Deadline)); return []; } };
        var clients = new DeliveryClients(); await using var sender = new ChildDiagnosticForwarder(launch, reader, clients);
        try
        {
            await entered.Task.WaitAsync(Deadline);
            await Assert.ThrowsAsync<TimeoutException>(() => sender.StopAsync());
            var status = sender.Read(); Assert.True(status.Closed); Assert.False(status.Released); Assert.False(status.DrainComplete);
            Assert.False(clients.Product!.Completion.IsCompleted); Assert.Equal(0, reader.Disposals);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.StopAsync(cancel.Token));
        }
        finally { release.Set(); }
        await sender.StopAsync(); Assert.True(sender.Read().Released); Assert.False(sender.Read().DrainComplete);
        Assert.True(clients.Product!.Completion.IsCompletedSuccessfully); Assert.Equal(0, reader.Disposals); Assert.Equal(0, clients.Disposals);
    }

    [Fact]
    public async Task ReaderFailure_IsIsolatedAndStatusStoresOnlyTypeThenWorkerCanRecover()
    {
        var launch = Launch(); var reader = new DeliveryReader(); var failures = 1;
        reader.OnRead = (_, _) => Interlocked.Exchange(ref failures, 0) == 1 ? throw new IOException(launch.Identity.Nonce + launch.Endpoint) : [];
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients());
        await UntilAsync(() => sender.Read().Failures == 1);
        reader.OnRead = null; reader.Add(Record());
        await using var remote = new DeliveryRemote(launch);
        await UntilAsync(() => sender.Read().Accepted == 1); await sender.StopAsync();
        Assert.Equal(typeof(IOException).FullName, sender.Read().LastFailureType); Assert.Equal(1, sender.Read().Failures); Assert.True(sender.Read().Released);
        Assert.DoesNotContain(launch.Identity.Nonce, JsonSerializer.Serialize(sender.Read())); Assert.DoesNotContain(launch.Endpoint, JsonSerializer.Serialize(sender.Read()));
    }

    [Fact]
    public async Task ByteBoundedBatch_LeavesTheNextRecordForTheNextPacketWithoutLosingIt()
    {
        var app = Guid.NewGuid(); var first = Record(1, app) with { MessageTemplate = new('x', 900) };
        var singleBytes = JsonSerializer.SerializeToUtf8Bytes(ChildDiagnosticCodec.Bound(first), ChildDiagnosticCodec.Json).Length + 2;
        var options = Options(); options.MaxBatchBytes = singleBytes; var launch = Launch(options); var reader = new DeliveryReader();
        reader.Add(first, first with { Sequence = 2 });
        await using var remote = new DeliveryRemote(launch); await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(options));
        await UntilAsync(() => sender.Read().Accepted == 2); await sender.StopAsync();
        Assert.Equal(2, remote.Nonempty.Length); Assert.All(remote.Nonempty, x => { Assert.Equal(1, x.RecordCount); Assert.Equal(singleBytes, x.Payload.Length); });
        Assert.Equal(new long[] { 1, 2 }, remote.Nonempty.SelectMany(x => JsonSerializer.Deserialize<DiagnosticRecord[]>(x.Payload)!).Select(x => x.Sequence));
        Assert.Equal(0, sender.Read().Lost); Assert.Equal(0, sender.Read().Unconfirmed);
    }

    [Fact]
    public async Task RecordRateOverflow_IsKnownLocalLossAndIsNotReplayedAfterWindowReset()
    {
        var options = Options(); options.RecordsPerSecond = 2; var launch = Launch(options); var app = Guid.NewGuid(); var reader = new DeliveryReader();
        reader.Add(Record(1, app), Record(2, app), Record(3, app), Record(4, app));
        await using var remote = new DeliveryRemote(launch); await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(options));
        await UntilAsync(() => sender.Read().Accepted == 2 && sender.Read().RateLimited == 2);
        await UntilAsync(() => remote.Packets.Any(x => x.RecordCount == 0 && x.Lost == 2));
        await sender.StopAsync();
        Assert.Equal(new long[] { 1, 2 }, remote.Nonempty.SelectMany(x => JsonSerializer.Deserialize<DiagnosticRecord[]>(x.Payload)!).Select(x => x.Sequence));
        Assert.Equal(2, sender.Read().Lost); Assert.Equal(2, sender.Read().RateLimited); Assert.Equal(0, sender.Read().Unconfirmed);
    }

    [Fact]
    public async Task StopCapturesOneFiniteTargetAndDoesNotChaseNewerReaderRecords()
    {
        var options = Options(); options.MaxBatchRecords = 1; options.ShutdownTimeout = TimeSpan.FromSeconds(2); options.RequestTimeout = TimeSpan.FromSeconds(2);
        var launch = Launch(options); var app = Guid.NewGuid(); var reader = new DeliveryReader(); reader.Add(Record(1, app), Record(2, app), Record(3, app));
        var latestCalls = 0; reader.OnLatest = () => { Interlocked.Increment(ref latestCalls); return 2; };
        var entered = Signal(); var release = Signal();
        await using var remote = new DeliveryRemote(launch, async (packet, _) =>
        {
            if (packet.RecordCount != 0 && JsonSerializer.Deserialize<DiagnosticRecord[]>(packet.Payload)![0].Sequence == 1)
            { entered.TrySetResult(); await release.Task; }
            return new() { Accepted = packet.RecordCount, LastSequence = packet.RecordCount == 0 ? 0 : JsonSerializer.Deserialize<DiagnosticRecord[]>(packet.Payload)![^1].Sequence };
        });
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(options));
        try
        {
            await entered.Task.WaitAsync(Deadline); var stop = sender.StopAsync(); release.TrySetResult(); await stop;
            Assert.Equal(1, latestCalls); Assert.Equal(new long[] { 1, 2 }, remote.Nonempty.SelectMany(x => JsonSerializer.Deserialize<DiagnosticRecord[]>(x.Payload)!).Select(x => x.Sequence));
            Assert.Equal(2, sender.Read().Accepted); Assert.True(sender.Read().DrainComplete); Assert.True(sender.Read().Released);
        }
        finally { release.TrySetResult(); }
    }
}
