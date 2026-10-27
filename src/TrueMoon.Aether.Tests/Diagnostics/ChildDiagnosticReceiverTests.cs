using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Sessions;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Tests.Diagnostics;

public sealed class ChildDiagnosticReceiverTests
{
    [Theory]
    [InlineData("records-low")] [InlineData("records-high")] [InlineData("bytes-low")] [InlineData("bytes-high")]
    [InlineData("rps-low")] [InlineData("rps-high")] [InlineData("bps-low")] [InlineData("bps-high")]
    [InlineData("poll-low")] [InlineData("poll-high")] [InlineData("request-low")] [InlineData("request-high")]
    [InlineData("shutdown-low")] [InlineData("shutdown-high")] [InlineData("disabled-invalid")]
    public void InvalidOptions_RejectBeforeAnyTransportFactoryOrImporterIsUsed(string kind)
    {
        var options = ChildDiagnosticFixture.Options();
        switch (kind)
        {
            case "records-low": options.MaxBatchRecords = 0; break; case "records-high": options.MaxBatchRecords = 33; break;
            case "bytes-low": options.MaxBatchBytes = 1023; break; case "bytes-high": options.MaxBatchBytes = 65537; break;
            case "rps-low": options.RecordsPerSecond = options.MaxBatchRecords - 1; break; case "rps-high": options.RecordsPerSecond = 4097; break;
            case "bps-low": options.BytesPerSecond = options.MaxBatchBytes + 1023; break; case "bps-high": options.BytesPerSecond = 4 * 1024 * 1024 + 1; break;
            case "poll-low": options.PollInterval = TimeSpan.FromMilliseconds(9); break; case "poll-high": options.PollInterval = TimeSpan.FromMilliseconds(1001); break;
            case "request-low": options.RequestTimeout = TimeSpan.FromMilliseconds(19); break; case "request-high": options.RequestTimeout = TimeSpan.FromMilliseconds(2001); break;
            case "shutdown-low": options.ShutdownTimeout = TimeSpan.FromMilliseconds(49); break; case "shutdown-high": options.ShutdownTimeout = TimeSpan.FromMilliseconds(5001); break;
            default: options.Enabled = false; options.MaxBatchRecords = 0; break;
        }
        var servers = new DeliveryServers(); var importer = new DeliveryImporter();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChildDiagnosticReceiver(ChildDiagnosticFixture.Launch(options), Environment.ProcessId, importer, servers));
        Assert.Equal(0, servers.Calls); Assert.Empty(importer.Calls); Assert.Equal(0, importer.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExactOptionBounds_AreCopiedBeforeUseAndAcceptOneFullBatch(bool maximum)
    {
        var options = new ChildDiagnosticDeliveryOptions { MaxBatchRecords = maximum ? 32 : 1, MaxBatchBytes = maximum ? 65536 : 1024,
            RecordsPerSecond = maximum ? 4096 : 1, BytesPerSecond = maximum ? 4 * 1024 * 1024 : 2048,
            PollInterval = TimeSpan.FromMilliseconds(maximum ? 1000 : 10), RequestTimeout = TimeSpan.FromMilliseconds(maximum ? 2000 : 20), ShutdownTimeout = TimeSpan.FromMilliseconds(maximum ? 5000 : 50) };
        var launch = ChildDiagnosticFixture.Launch(options);
        if (maximum) launch = launch with { Identity = launch.Identity with { Generation = ulong.MaxValue } };
        var importer = new DeliveryImporter(); var servers = new DeliveryServers(options);
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, servers);
        var count = options.MaxBatchRecords; options.Enabled = false; options.MaxBatchRecords = 0; options.MaxBatchBytes = 0; options.RecordsPerSecond = 0; options.BytesPerSecond = 0;
        var application = Guid.NewGuid(); var records = Enumerable.Range(1, count).Select(index => ChildDiagnosticFixture.Record(index, application)).ToArray();
        var reply = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, application, 1, records));
        Assert.Equal(0, reply.Status); Assert.Equal(count, reply.Accepted); Assert.Equal(count, reply.LastSequence); Assert.Equal(count, importer.Calls.Count);
        Assert.Equal(launch.Endpoint + "-diag", servers.Arguments!.Endpoint); Assert.True(receiver.Read().Enabled);
        await receiver.StopAsync(); Assert.True(receiver.Read().Released); Assert.Equal(0, servers.Disposals); Assert.Equal(0, importer.Disposals);
    }

    [Theory]
    [InlineData("no-settings")] [InlineData("disabled")] [InlineData("no-importer")]
    public async Task MissingDependencyOrDisabledLaunch_IsLocalOnlyWithNoTransportIo(string kind)
    {
        var launch = ChildDiagnosticFixture.Launch(); if (kind == "no-settings") launch = launch with { DiagnosticDelivery = null }; if (kind == "disabled") launch.DiagnosticDelivery!.Enabled = false;
        var importer = new DeliveryImporter(); var servers = new DeliveryServers(); await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, kind == "no-importer" ? null : importer, servers);
        var application = Guid.NewGuid(); var reply = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, application, 1, ChildDiagnosticFixture.Record(1, application)));
        Assert.Equal(5, reply.Status); Assert.Equal(0, reply.Accepted); Assert.False(receiver.Read().Enabled); Assert.True(receiver.Read().Released); Assert.Equal(0, servers.Calls); Assert.Empty(importer.Calls);
    }

    [Fact]
    public async Task ReceiverImport_PreservesChildProvenanceAndAuthoritativeHostContextWithoutSecretEnvelopeProperties()
    {
        using var host = new DiagnosticPipeline(new() { Emergency = new() { Enabled = false } });
        var importer = new DeliveryImporter((record, context) => host.TryImport(record, context)); var launch = ChildDiagnosticFixture.Launch(); var servers = new DeliveryServers();
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, servers); var application = Guid.NewGuid();
        var record = ChildDiagnosticFixture.Record(17, application) with { Properties = new Dictionary<string, string?> { ["SessionId"] = "forged", ["Generation"] = "forged", ["RuntimeRole"] = "forged", ["GameBuildId"] = "forged", ["user"] = "retained" } };
        var reply = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, application, 1, record)); Assert.Equal((0, 1, 0, 17L), (reply.Status, reply.Accepted, reply.Rejected, reply.LastSequence));
        var imported = Assert.Single(host.Read()); Assert.Equal(host.ApplicationId, imported.ApplicationId); Assert.Equal(1, imported.Sequence); Assert.Equal(new DiagnosticOrigin(application, 17), imported.Origin);
        Assert.Equal(record.TimestampUtc, imported.TimestampUtc); Assert.Equal(record.ProcessId, imported.ProcessId); Assert.Equal(record.TraceId, imported.TraceId); Assert.Equal(record.SpanId, imported.SpanId); Assert.Equal(record.ExceptionMessage, imported.ExceptionMessage);
        Assert.Equal(launch.Identity.SessionId.ToString("D"), imported.Properties["SessionId"]); Assert.Equal("4", imported.Properties["Generation"]); Assert.Equal(launch.Role.ToString(), imported.Properties["RuntimeRole"]); Assert.Equal(launch.Identity.GameBuildId, imported.Properties["GameBuildId"]); Assert.Equal("retained", imported.Properties["user"]);
        Assert.DoesNotContain(imported.Properties.Keys, key => key.Equals("Nonce", StringComparison.OrdinalIgnoreCase) || key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(launch.Identity.Nonce, imported.Properties.Values); Assert.DoesNotContain(launch.Endpoint, imported.Properties.Values);
    }

    [Theory]
    [InlineData("session")] [InlineData("generation")] [InlineData("host")] [InlineData("game")] [InlineData("nonce")] [InlineData("role")] [InlineData("pid")]
    public async Task ForeignGenerationIdentity_IsRejectedBeforeImportAndDoesNotBindTheApplication(string field)
    {
        var launch = ChildDiagnosticFixture.Launch(); var importer = new DeliveryImporter(); await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        var app = Guid.NewGuid(); var packet = ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(1, app));
        switch (field) { case "session": packet.SessionId = Guid.NewGuid(); break; case "generation": packet.Generation--; break; case "host": packet.HostContractId = new('D', 64); break; case "game": packet.GameBuildId = new('E', 64); break; case "nonce": packet.Nonce = new('F', 64); break; case "role": packet.Role = (int)RuntimeRole.EditPreview; break; default: packet.ProcessId++; break; }
        var rejected = await receiver.PushAsync(packet); Assert.Equal(2, rejected.Status); Assert.Equal(0, rejected.Accepted); Assert.Empty(importer.Calls);
        var nextApp = Guid.NewGuid(); var accepted = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, nextApp, 1, ChildDiagnosticFixture.Record(1, nextApp))); Assert.Equal(1, accepted.Accepted); Assert.Single(importer.Calls);
    }

    [Theory]
    [InlineData("version")] [InlineData("application")] [InlineData("packet-zero")] [InlineData("packet-negative")] [InlineData("lost")] [InlineData("unconfirmed")]
    [InlineData("count-negative")] [InlineData("count-high")] [InlineData("payload-null")] [InlineData("payload-high")]
    public async Task InvalidEnvelopeBounds_RejectWithoutImport(string field)
    {
        var launch = ChildDiagnosticFixture.Launch(); var importer = new DeliveryImporter(); await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        var app = Guid.NewGuid(); var packet = ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(1, app));
        switch (field) { case "version": packet.Version = 2; break; case "application": packet.ApplicationId = Guid.Empty; break; case "packet-zero": packet.PacketSequence = 0; break; case "packet-negative": packet.PacketSequence = -1; break; case "lost": packet.Lost = -1; break; case "unconfirmed": packet.Unconfirmed = -1; break; case "count-negative": packet.RecordCount = -1; break; case "count-high": packet.RecordCount = 3; break; case "payload-null": packet.Payload = null!; break; default: packet.Payload = new byte[4097]; break; }
        var reply = await receiver.PushAsync(packet); Assert.Equal(1, reply.Status); Assert.Equal(0, reply.Accepted); Assert.Empty(importer.Calls); Assert.Equal(0, receiver.Read().Accepted);
    }

    [Theory]
    [InlineData("null")] [InlineData("origin")] [InlineData("application")] [InlineData("pid")] [InlineData("sequence")] [InlineData("level")]
    [InlineData("source")] [InlineData("source-size")] [InlineData("event-null")] [InlineData("event-size")] [InlineData("message-null")] [InlineData("message-size")]
    [InlineData("properties-null")] [InlineData("properties-count")] [InlineData("key-blank")] [InlineData("key-size")] [InlineData("value-size")] [InlineData("nonce-key")] [InlineData("endpoint-key")]
    [InlineData("exception-type")] [InlineData("exception-message")] [InlineData("exception-stack")] [InlineData("caller")]
    public async Task InvalidRow_RejectsTheWholePacketBeforeEvenTheEarlierValidRowIsImported(string field)
    {
        var launch = ChildDiagnosticFixture.Launch(); launch.DiagnosticDelivery!.MaxBatchBytes = 65536;
        var importer = new DeliveryImporter(); await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        var app = Guid.NewGuid(); var row = ChildDiagnosticFixture.Record(2, app);
        row = field switch
        {
            "null" => null!, "origin" => row with { Origin = new(Guid.NewGuid(), 1) }, "application" => row with { ApplicationId = Guid.NewGuid() }, "pid" => row with { ProcessId = row.ProcessId + 1 }, "sequence" => row with { Sequence = 1 }, "level" => row with { Level = (DiagnosticLevel)99 },
            "source" => row with { Source = " " }, "source-size" => row with { Source = new('x', 129) }, "event-null" => row with { EventName = null! }, "event-size" => row with { EventName = new('x', 129) }, "message-null" => row with { MessageTemplate = null! }, "message-size" => row with { MessageTemplate = new('x', 2049) },
            "properties-null" => row with { Properties = null! }, "properties-count" => row with { Properties = Enumerable.Range(0, 17).ToDictionary(index => index.ToString(), _ => (string?)"value") },
            "key-blank" => row with { Properties = new Dictionary<string, string?> { [" "] = "value" } }, "key-size" => row with { Properties = new Dictionary<string, string?> { [new('x', 65)] = "value" } }, "value-size" => row with { Properties = new Dictionary<string, string?> { ["key"] = new('x', 129) } },
            "nonce-key" => row with { Properties = new Dictionary<string, string?> { ["nOnCe"] = "private" } }, "endpoint-key" => row with { Properties = new Dictionary<string, string?> { ["ENDPOINT"] = "private" } },
            "exception-type" => row with { ExceptionType = new('x', 129) }, "exception-message" => row with { ExceptionMessage = new('x', 2049) }, "exception-stack" => row with { ExceptionStackTrace = new('x', 4097) }, _ => row with { Caller = new('x', 129) }
        };
        var reply = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(1, app), row));
        Assert.Equal(1, reply.Status); Assert.Equal(0, reply.Accepted); Assert.Empty(importer.Calls); Assert.Equal(0, receiver.Read().Accepted); Assert.Equal(1, receiver.Read().Failures);
    }

    [Theory]
    [InlineData("malformed")] [InlineData("object")] [InlineData("count")] [InlineData("depth")]
    public async Task MalformedJsonShape_IsRejectedBeforeImport(string kind)
    {
        var launch = ChildDiagnosticFixture.Launch(); var importer = new DeliveryImporter(); await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        var app = Guid.NewGuid(); var packet = ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(1, app));
        packet.Payload = System.Text.Encoding.UTF8.GetBytes(kind == "malformed" ? "[" : kind == "object" ? "{}" : kind == "count" ? "[]" : new string('[', 17) + new string(']', 17));
        var reply = await receiver.PushAsync(packet); Assert.Equal(1, reply.Status); Assert.Empty(importer.Calls); Assert.Equal(1, receiver.Read().Failures);
    }

    [Fact]
    public async Task ApplicationPacketAndRecordFences_RejectStaleDuplicatesWhileLossHeartbeatsRemainMonotonic()
    {
        var launch = ChildDiagnosticFixture.Launch(); var importer = new DeliveryImporter(); await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers()); var app = Guid.NewGuid();
        var first = ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(3, app)); first.Lost = 5; first.Unconfirmed = 7;
        Assert.Equal(1, (await receiver.PushAsync(first)).Accepted); Assert.Equal(3, (await receiver.PushAsync(first)).Status);
        Assert.Equal(3, (await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 2, ChildDiagnosticFixture.Record(3, app)))).Status);
        var foreign = Guid.NewGuid(); Assert.Equal(2, (await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, foreign, 2, ChildDiagnosticFixture.Record(4, foreign)))).Status);
        var heartbeat = ChildDiagnosticFixture.Packet(launch, app, 2); heartbeat.Lost = 2; heartbeat.Unconfirmed = 9; var ack = await receiver.PushAsync(heartbeat);
        Assert.Equal((0, 0, 0, 3L), (ack.Status, ack.Accepted, ack.Rejected, ack.LastSequence)); Assert.Single(importer.Calls);
        var status = receiver.Read(); Assert.Equal(2, status.Duplicates); Assert.Equal(5, status.ChildReportedLost); Assert.Equal(9, status.ChildReportedUnconfirmed);
        Assert.Equal(1, (await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 3, ChildDiagnosticFixture.Record(4, app)))).Accepted);
    }

    [Fact]
    public async Task FalseAndThrowingImporter_RejectIndividualRowsContinueAndAcknowledgeWithoutLeakingErrorText()
    {
        var launch = ChildDiagnosticFixture.Launch(); launch.DiagnosticDelivery!.MaxBatchRecords = 3;
        var importer = new DeliveryImporter((record, _) => record.Sequence == 1 ? true : record.Sequence == 2 ? false : throw new IOException(launch.Identity.Nonce + launch.Endpoint + "payload"));
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers()); var app = Guid.NewGuid();
        var reply = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(1, app), ChildDiagnosticFixture.Record(2, app), ChildDiagnosticFixture.Record(3, app)));
        Assert.Equal((0, 1, 2, 3L), (reply.Status, reply.Accepted, reply.Rejected, reply.LastSequence)); Assert.Equal(new long[] { 1, 2, 3 }, importer.Calls.Select(call => call.Record.Sequence));
        var status = receiver.Read(); Assert.Equal(1, status.Accepted); Assert.Equal(2, status.Rejected); Assert.Equal(1, status.Failures); Assert.Equal(typeof(IOException).FullName, status.LastFailureType);
        Assert.DoesNotContain(launch.Identity.Nonce, status.ToString()); Assert.DoesNotContain(launch.Endpoint, status.ToString()); Assert.Equal(0, importer.Disposals);
    }

    [Fact]
    public async Task BlockedImporter_RejectsConcurrentPacketsAndPreservesOwnedTransportAcrossTimeoutAndCancelledStopWait()
    {
        var options = ChildDiagnosticFixture.Options(); options.ShutdownTimeout = TimeSpan.FromMilliseconds(50); var launch = ChildDiagnosticFixture.Launch(options);
        using var release = new ManualResetEventSlim(); var entered = ChildDiagnosticFixture.Signal(); var importer = new DeliveryImporter((_, _) => { entered.TrySetResult(); Assert.True(release.Wait(ChildDiagnosticFixture.Deadline)); return true; }); var servers = new DeliveryServers();
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, servers); var app = Guid.NewGuid(); var pending = receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 1, ChildDiagnosticFixture.Record(1, app)));
        try
        {
            await entered.Task.WaitAsync(ChildDiagnosticFixture.Deadline); var busy = await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 2, ChildDiagnosticFixture.Record(2, app))); Assert.Equal(4, busy.Status); Assert.Single(importer.Calls);
            await Assert.ThrowsAsync<TimeoutException>(() => receiver.StopAsync()); Assert.True(receiver.Read().Closed); Assert.False(receiver.Read().Released); Assert.False(receiver.Read().DrainComplete);
            using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receiver.StopAsync(cancel.Token)); Assert.False(receiver.Read().Released);
            Assert.Equal(5, (await receiver.PushAsync(ChildDiagnosticFixture.Packet(launch, app, 3, ChildDiagnosticFixture.Record(3, app)))).Status);
        }
        finally { release.Set(); }
        Assert.Equal(1, (await pending).Accepted); await receiver.StopAsync(); Assert.True(receiver.Read().Released); Assert.True(servers.Product!.Completion.IsCompletedSuccessfully); Assert.Equal(0, importer.Disposals); Assert.Equal(0, servers.Disposals);
    }
}
