using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Gpu;
using TrueMoon.Diagnostics;
using Xunit;
using static TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticFixture;

namespace TrueMoon.Aether.Tests.Diagnostics;

[Collection(nameof(ChildDiagnosticForwarderCollection))]
public sealed class RuntimeDiagnosticObservationTests
{
    [Fact]
    public async Task EmptyRing_TransmitsSanitizedHealthThroughRealPipeAndRetainsLastObservationAfterStop()
    {
        var launch = Launch(); var application = Guid.NewGuid(); var reader = new DeliveryReader();
        var source = new Source(Observation(application)); var importer = new DeliveryImporter();
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(), source);
        await UntilAsync(() => receiver.Read().RuntimeObservation?.Collector?.Sinks.Count == 1);
        var snapshot = receiver.Read().RuntimeObservation!; var sink = Assert.Single(snapshot.Collector!.Sinks);
        Assert.Equal(3, sink.Failures); Assert.Equal(4, sink.Dropped); Assert.Equal("omitted", sink.LastError);
        Assert.Equal(application, snapshot.Collector.ApplicationId); Assert.Equal("GPU-device", snapshot.GraphicsDevice!.Name);
        Assert.Empty(importer.Calls); Assert.Equal(0, sender.Read().Accepted); Assert.Equal(0, sender.Read().Failures);
        Assert.Throws<NotSupportedException>(() => ((IList<DiagnosticSinkHealth>)snapshot.Collector.Sinks).Clear());
        await sender.StopAsync(); await receiver.StopAsync();
        Assert.NotNull(receiver.Read().RuntimeObservation); Assert.True(receiver.Read().Released); Assert.True(sender.Read().Released);
        Assert.Equal("PRIVATE_ERROR", source.Value.Collector!.Sinks[0].LastError); Assert.Equal(0, reader.Disposals); Assert.Equal(0, source.Disposals);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(snapshot));
    }

    [Theory]
    [InlineData("collector-identity")]
    [InlineData("negative-counter")]
    [InlineData("error-text")]
    [InlineData("sink-count")]
    [InlineData("device-name")]
    [InlineData("oversized-body")]
    public async Task InvalidObservation_RejectsWholePacketAndPreservesPreviousSnapshotAndRecordCursor(string change)
    {
        var launch = Launch(); var application = Guid.NewGuid(); var importer = new DeliveryImporter();
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        var good = Observation(application) with { Collector = Observation(application).Collector! with { Sinks = [] } };
        var first = Packet(launch, application, 1, Record(1, application)); first.Version = 2;
        first.Payload = ChildDiagnosticCodec.Envelope(first.Payload, RuntimeDiagnosticObservationCodec.Encode(good, 8192), false);
        Assert.Equal(1, (await receiver.PushAsync(first)).Accepted); var before = receiver.Read().RuntimeObservation;
        var invalid = good;
        if (change == "collector-identity") invalid = good with { Collector = good.Collector! with { ApplicationId = Guid.NewGuid() } };
        if (change == "negative-counter") invalid = good with { Collector = good.Collector! with { MemoryEvicted = -1 } };
        if (change == "error-text") invalid = good with { Collector = good.Collector! with { Sinks = [new("jsonl", 0, 0, 0, 1, 0, 0, "PRIVATE_ERROR")] } };
        if (change == "sink-count") invalid = good with { Collector = good.Collector! with { Sinks = Enumerable.Range(0, 17).Select(i => new DiagnosticSinkHealth("custom-" + i, 0, 0, 0, 0, 0, 0, null)).ToArray() } };
        if (change == "device-name") invalid = good with { GraphicsDevice = good.GraphicsDevice! with { Name = new string('n', 257) } };
        var packet = Packet(launch, application, 2, Record(2, application)); packet.Version = 2;
        packet.Payload = ChildDiagnosticCodec.Envelope(packet.Payload, JsonSerializer.SerializeToUtf8Bytes(invalid), false);
        if (change == "oversized-body") packet.Payload = new byte[launch.DiagnosticDelivery!.MaxBatchBytes + 1];
        var reply = await receiver.PushAsync(packet); Assert.Equal(1, reply.Status); Assert.Equal(1, reply.LastSequence);
        Assert.Single(importer.Calls); Assert.Same(before, receiver.Read().RuntimeObservation);
        var legacy = Packet(launch, application, 2, Record(2, application)); Assert.Equal(1, (await receiver.PushAsync(legacy)).Accepted);
        Assert.Equal(2, importer.Calls.Count); Assert.Same(before, receiver.Read().RuntimeObservation);
    }

    [Fact]
    public void Encode_BoundsSinkCountAndBytesAndCountsOmissionsWithoutChangingSource()
    {
        var source = Observation(Guid.NewGuid()); source = source with { Collector = source.Collector! with
        { Sinks = Enumerable.Range(0, 66).Select(i => new DiagnosticSinkHealth("custom-" + i, i, 1, 2, 3, 4, 5, "PRIVATE_ERROR")).ToArray() } };
        var encoded = RuntimeDiagnosticObservationCodec.Encode(source, 1400)!; Assert.InRange(encoded.Length, 1, 1400);
        using var json = JsonDocument.Parse(encoded); var snapshot = RuntimeDiagnosticObservationCodec.Decode(json.RootElement, source.Collector.ApplicationId);
        Assert.InRange(snapshot.Collector!.Sinks.Count, 0, 16); Assert.Equal(66 - snapshot.Collector.Sinks.Count, snapshot.OmittedSinks);
        Assert.Equal(66, source.Collector.Sinks.Count); Assert.Equal("PRIVATE_ERROR", source.Collector.Sinks[0].LastError);
        Assert.All(snapshot.Collector.Sinks, sink => Assert.Equal("omitted", sink.LastError)); Assert.DoesNotContain("PRIVATE", System.Text.Encoding.UTF8.GetString(encoded));
        Assert.Null(RuntimeDiagnosticObservationCodec.Encode(source, 1));
    }

    [Fact]
    public async Task ObservationFailure_DoesNotPreventRecordDeliveryAndDoesNotDisposeBorrowedSource()
    {
        var launch = Launch(); var application = Guid.NewGuid(); var reader = new DeliveryReader(); reader.Add(Record(1, application));
        var source = new Source(Observation(application)) { Failure = new IOException("PRIVATE_FAILURE") }; var importer = new DeliveryImporter();
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(), source);
        await UntilAsync(() => sender.Read().Accepted == 1); await sender.StopAsync(); await receiver.StopAsync();
        Assert.Equal(1, Assert.Single(importer.Calls).Record.Sequence); Assert.Null(receiver.Read().RuntimeObservation);
        Assert.True(sender.Read().Failures > 0); Assert.Equal(0, source.Disposals); Assert.Equal(0, reader.Disposals);
    }

    [Fact]
    public async Task Source_ReadsCachedDeviceWithoutCreatingResourcesAndKeepsUnavailableCollectorDistinct()
    {
        await using var gpu = new GpuResourceHost(); var source = new RuntimeDiagnosticObservationSource(null, gpu);
        Assert.Null(source.Read().Collector); Assert.Null(source.Read().GraphicsDevice); Assert.Equal(0, gpu.ResourceCount);
        var device = new GpuDeviceObservation("Vulkan", "selected", 1, 2, 3, 4); gpu.ObserveDevice(device);
        Assert.Same(device, source.Read().GraphicsDevice); await gpu.DisposeAsync(); Assert.Same(device, source.Read().GraphicsDevice); Assert.Equal(0, gpu.ResourceCount);
    }

    [Fact]
    public async Task ObservationBudgetOmission_CountsPacketWithoutClearingCachedEvidenceOrBlockingRecords()
    {
        var launch = Launch(); var application = Guid.NewGuid(); var importer = new DeliveryImporter();
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers());
        var first = Packet(launch, application, 1, Record(1, application)); first.Version = 2;
        first.Payload = ChildDiagnosticCodec.Envelope(first.Payload, RuntimeDiagnosticObservationCodec.Encode(Observation(application), 8192), false);
        Assert.Equal(1, (await receiver.PushAsync(first)).Accepted); var before = receiver.Read().RuntimeObservation;
        var next = Packet(launch, application, 2, Record(2, application)); next.Version = 2;
        next.Payload = ChildDiagnosticCodec.Envelope(next.Payload, null, true);
        Assert.Equal(1, (await receiver.PushAsync(next)).Accepted); Assert.Same(before, receiver.Read().RuntimeObservation);
        Assert.Equal(1, receiver.Read().ObservationOmittedPackets); Assert.Equal(new long[] { 1, 2 }, importer.Calls.Select(call => call.Record.Sequence));
    }

    private static RuntimeDiagnosticObservation Observation(Guid application) => new(Timestamp,
        new(application, false, 0, 0, 0, 0, 0, 0, [new("jsonl", 10, 2, 4, 3, 1, 100, "PRIVATE_ERROR")]),
        new("Vulkan", "GPU-device", 1, 2, 3, 4));
    private sealed class Source(RuntimeDiagnosticObservation value) : IRuntimeDiagnosticObservationSource, IDisposable
    {
        internal RuntimeDiagnosticObservation Value = value; internal Exception? Failure; internal int Disposals;
        public RuntimeDiagnosticObservation Read() => Failure == null ? Value : throw Failure;
        public void Dispose() => Disposals++;
    }
}
