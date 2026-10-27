using System.Buffers;
using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Sessions;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using Xunit;
using static TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticFixture;

namespace TrueMoon.Aether.Tests.Diagnostics;

public class ChildDiagnosticLimitsTests
{
    [Fact]
    public void DedicatedTransportLimits_DisableFeedbackAndBoundBothDirections()
    {
        var options = Options(); var limits = ChildDiagnosticFactories.Limits(options);
        Assert.Equal(1, limits.MaxPendingRequests); Assert.Equal(0, limits.MaxQueuedRequests);
        Assert.Equal(1, limits.MaxConcurrentRequests); Assert.Equal(1, limits.MaxConnections);
        Assert.Equal(72 * 1024, limits.MaxBufferedRequestBytes); Assert.Equal(options.RequestTimeout, limits.RequestTimeout);
        Assert.False(limits.MetricsEnabled); Assert.False(limits.CaptureRequestEvents); Assert.False(limits.PropagateTraceContext);
    }

    [Theory]
    [InlineData(73728, 1024, true)]
    [InlineData(73729, 1024, false)]
    [InlineData(73728, 1025, false)]
    public async Task ReceiverWireBounds_AllowExactSizesAndNeverPublishOversizedResponse(int requestSize, int replySize, bool valid)
    {
        var inner = new ByteHandler(replySize); var bounded = new BoundedDiagnosticHandler(inner); var writer = new ArrayBufferWriter<byte>();
        if (valid)
        {
            Assert.Equal((true, (Exception?)null), await bounded.HandleAsync(7, new byte[requestSize], writer));
            Assert.Equal(1, inner.Calls); Assert.Equal(replySize, writer.WrittenCount);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => bounded.HandleAsync(7, new byte[requestSize], writer));
            Assert.Equal(requestSize > 73728 ? 0 : 1, inner.Calls); Assert.Equal(0, writer.WrittenCount);
        }
    }

    [Theory]
    [InlineData(73728, 1024, true)]
    [InlineData(73729, 1024, false)]
    [InlineData(73728, 1025, false)]
    public async Task SenderWireBounds_RejectBeforeTransportOrUserDecoder(int requestSize, int replySize, bool valid)
    {
        var inner = new ByteClient(replySize); var bounded = new BoundedDiagnosticClient(inner); var decoded = 0;
        Task<int> Invoke() => bounded.InvokeAsync(9, writer => writer.Write(new byte[requestSize]), memory => { decoded++; return memory.Length; });
        if (valid) { Assert.Equal(replySize, await Invoke()); Assert.Equal(1, inner.Calls); Assert.Equal(1, decoded); Assert.Equal(requestSize, inner.RequestBytes); }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await Invoke());
            Assert.Equal(requestSize > 73728 ? 0 : 1, inner.Calls); Assert.Equal(0, decoded);
        }
    }

    [Fact]
    public void CodecPropertyBudget_ExaminesOnlySixteenCopiesAndKeepsFirstClippedKeyCollision()
    {
        var key = new string('k', 64); var properties = new Dictionary<string, string?> { [key + "1"] = "first", [key + "2"] = "second", ["Nonce"] = "secret" };
        for (var i = 3; i < 17; i++) properties.Add("key" + i, "value" + i);
        var input = Record() with { Properties = properties }; var result = ChildDiagnosticCodec.Bound(input);
        properties[key + "1"] = "changed";
        Assert.Equal("first", result.Properties[key]); Assert.Equal(14, result.Properties.Count); Assert.False(result.Properties.ContainsKey("key16"));
        Assert.False(result.Properties.ContainsKey("Nonce")); Assert.True(result.IsTruncated);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string?>)result.Properties).Add("later", "mutable"));
        var launch = Launch(); var packet = Packet(launch, input.ApplicationId, 1, result);
        Assert.Equal(result.Properties.OrderBy(x => x.Key), Assert.Single(ChildDiagnosticCodec.Decode(packet, Options())).Properties.OrderBy(x => x.Key));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RateBudget_IncludesEnvelopeAcceptsExactBoundaryAndRecoversInANewWindow(bool bytes)
    {
        var options = Options(); options.RecordsPerSecond = 2;
        if (bytes) options.BytesPerSecond = options.MaxBatchBytes + 1024;
        var rate = new ChildDiagnosticRate(options);
        Assert.True(rate.Enter(bytes ? 1 : 2, bytes ? options.MaxBatchBytes : 0));
        Assert.False(rate.Enter(1, 0));
        await UntilAsync(() => rate.Enter(1, bytes ? options.MaxBatchBytes : 0));
        Assert.False(rate.Enter(bytes ? 1 : 2, 0));
    }

    [Fact]
    public async Task ReceiverRateRejection_DoesNotImportOrAdvanceUnvalidatedPacketAndRecovers()
    {
        var options = Options(); options.RecordsPerSecond = 2;
        var launch = Launch(options); var importer = new DeliveryImporter(); var servers = new DeliveryServers(options);
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, servers);
        var app = Guid.NewGuid();
        Assert.Equal(2, (await receiver.PushAsync(Packet(launch, app, 1, Record(1, app), Record(2, app)))).Accepted);
        var next = Packet(launch, app, 2, Record(3, app));
        var refused = await receiver.PushAsync(next);
        Assert.Equal(4, refused.Status); Assert.Equal(2, refused.LastSequence); Assert.Equal(2, importer.Calls.Count); Assert.Equal(1, receiver.Read().RateLimited);
        DiagnosticDeliveryReply? admitted = null;
        using (var timeout = new CancellationTokenSource(Deadline))
            do { await Task.Delay(10, timeout.Token); admitted = await receiver.PushAsync(next); } while (admitted.Status != 0);
        Assert.Equal(1, admitted!.Accepted); Assert.Equal(3, admitted.LastSequence); Assert.Equal(3, importer.Calls.Count);
        Assert.Equal(0, receiver.Read().Duplicates); Assert.Equal(0, receiver.Read().Failures);
    }

    [Theory]
    [InlineData("receiver")]
    [InlineData("sender")]
    public void FailedFactory_PreservesOriginalCauseAndBorrowedDependencies(string kind)
    {
        var launch = Launch(); var cause = new IOException("factory failure");
        var importer = new DeliveryImporter(); var reader = new DeliveryReader();
        var servers = new DeliveryServers { Failure = cause }; var clients = new DeliveryClients { Failure = cause };
        var actual = kind == "receiver"
            ? Assert.Throws<IOException>(() => new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, servers))
            : Assert.Throws<IOException>(() => new ChildDiagnosticForwarder(launch, reader, clients));
        Assert.Same(cause, actual); Assert.Equal(kind == "receiver" ? 1 : 0, servers.Calls); Assert.Equal(kind == "sender" ? 1 : 0, clients.Calls);
        Assert.Equal(0, importer.Disposals); Assert.Equal(0, reader.Disposals); Assert.Equal(0, servers.Disposals); Assert.Equal(0, clients.Disposals);
    }

    [Fact]
    public void InvalidRole_IsRejectedBeforeEitherFactory()
    {
        var launch = Launch() with { Role = (RuntimeRole)99 }; var servers = new DeliveryServers(); var clients = new DeliveryClients();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChildDiagnosticReceiver(launch, Environment.ProcessId, new DeliveryImporter(), servers));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChildDiagnosticForwarder(launch, new DeliveryReader(), clients));
        Assert.Equal(0, servers.Calls); Assert.Equal(0, clients.Calls);
    }

    private sealed class ByteHandler(int bytes) : IInvocationServerHandler<IChildDiagnosticDelivery>
    {
        internal int Calls;
        public Task<(bool, Exception?)> HandleAsync(byte methodCode, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        { Calls++; writer.Write(new byte[bytes]); return Task.FromResult((true, (Exception?)null)); }
    }
    private sealed class ByteClient(int replyBytes) : IInvocationClient<IChildDiagnosticDelivery>
    {
        internal int Calls, RequestBytes;
        public Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> result, CancellationToken token = default)
        { Calls++; var bytes = new ArrayBufferWriter<byte>(); action?.Invoke(bytes); RequestBytes = bytes.WrittenCount; return Task.FromResult(result(new byte[replyBytes])); }
        public Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken token = default) => InvokeAsync(methodCode, action, _ => true, token);
        public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> result) => InvokeAsync(methodCode, action, result).GetAwaiter().GetResult();
        public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action) => InvokeAsync(methodCode, action).GetAwaiter().GetResult();
    }
}
