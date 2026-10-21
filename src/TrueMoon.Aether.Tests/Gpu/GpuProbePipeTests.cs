using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using TrueMoon.Aether;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuProbePipeTests
{
    private static SessionIdentity Identity() => new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64));
    [Theory]
    [InlineData("identity")] [InlineData("sequence")] [InlineData("phase")] [InlineData("unknown-field")]
    [InlineData("zero-length")] [InlineData("over-limit")] [InlineData("negative-length")]
    public async Task InvalidProtocolInput_CannotReachNativeAllocationAndWorkerIsDrained(string invalid)
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-gpu-tests-" + Guid.NewGuid().ToString("N"); var observed = 0;
        var worker = gpu.RunProducerProbeAsync(new(identity, endpoint, "gpu-pipe-tests"), CancellationToken.None,
            (_, _) => { Interlocked.Increment(ref observed); return Task.CompletedTask; });
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(limit.Token);
        var message = JsonSerializer.Serialize(new { Sequence = invalid == "sequence" ? 2ul : 1ul, Kind = "Produce", Identity = invalid == "identity" ? identity with { Generation = 2 } : identity });
        if (invalid == "unknown-field") message = message[..^1] + ",\"Unexpected\":1}";
        var bytes = Encoding.UTF8.GetBytes(message); var header = new byte[4];
        var length = invalid switch { "zero-length" => 0, "over-limit" => 65537, "negative-length" => -1, _ => bytes.Length };
        BinaryPrimitives.WriteInt32LittleEndian(header, length); await pipe.WriteAsync(header, limit.Token);
        if (length == bytes.Length) await pipe.WriteAsync(bytes, limit.Token);
        await pipe.ReadExactlyAsync(header, limit.Token); var replyLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        var reply = new byte[replyLength]; await pipe.ReadExactlyAsync(reply, limit.Token); var json = JsonSerializer.Deserialize<JsonElement>(reply);
        Assert.Equal("Error", json.GetProperty("Kind").GetString());
        if (invalid == "unknown-field") await Assert.ThrowsAsync<JsonException>(() => worker);
        else await Assert.ThrowsAsync<InvalidDataException>(() => worker);
        Assert.Equal(invalid == "phase" ? 1 : 0, observed); Assert.Equal(0, gpu.OpenHandleCount);
        await app.StopAsync(); Assert.Equal(0, gpu.ResourceCount); Assert.Null(gpu.CleanupError);
    }

    [Fact]
    public async Task MaximumAllowedPayload_ReachesPhaseValidationWithoutNativeWork()
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-gpu-tests-" + Guid.NewGuid().ToString("N"); var observed = 0;
        var worker = gpu.RunProducerProbeAsync(new(identity, endpoint, "gpu-pipe-tests"), CancellationToken.None, (_, _) => { observed++; return Task.CompletedTask; });
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); await pipe.ConnectAsync(limit.Token);
        var json = JsonSerializer.Serialize(new { Sequence = 1ul, Kind = "Produce", Identity = identity }); var bytes = Encoding.UTF8.GetBytes(json.PadRight(65536));
        await pipe.WriteAsync(new byte[] { 0, 0, 1, 0 }, limit.Token); await pipe.WriteAsync(bytes, limit.Token);
        var header = new byte[4]; await pipe.ReadExactlyAsync(header, limit.Token);
        var reply = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)]; await pipe.ReadExactlyAsync(reply, limit.Token);
        Assert.Equal("Error", JsonSerializer.Deserialize<JsonElement>(reply).GetProperty("Kind").GetString());
        await Assert.ThrowsAsync<InvalidDataException>(() => worker);
        Assert.Equal(1, observed); await app.StopAsync(); Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount);
    }

    [Fact]
    public async Task ErrorReply_UnresponsiveReaderCannotKeepProducerWorkerAliveIndefinitely()
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-gpu-tests-" + Guid.NewGuid().ToString("N");
        var worker = gpu.RunProducerProbeAsync(new(identity, endpoint, "gpu-pipe-tests"), CancellationToken.None);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(limit.Token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Sequence = 1ul, Kind = "Produce", Identity = identity });
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await pipe.WriteAsync(header, limit.Token); await pipe.WriteAsync(bytes, limit.Token);
        // Keep the peer alive without reading the response. The original phase failure must survive the write deadline.
        await Assert.ThrowsAsync<InvalidDataException>(() => worker.WaitAsync(limit.Token));
        await app.StopAsync(); Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount); Assert.Null(gpu.CleanupError);
    }

    [Fact]
    public async Task AppClose_CancelsAndDrainsAcceptedProducerWorkBeforeNativeAllocation()
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-gpu-tests-" + Guid.NewGuid().ToString("N");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = gpu.RunProducerProbeAsync(new(identity, endpoint, "gpu-pipe-tests"), CancellationToken.None,
            async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); });
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); await pipe.ConnectAsync(limit.Token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Sequence = 1ul, Kind = "Produce", Identity = identity }); var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await pipe.WriteAsync(header, limit.Token); await pipe.WriteAsync(bytes, limit.Token); await entered.Task.WaitAsync(limit.Token);
        Assert.False(worker.IsCompleted); await app.StopAsync().WaitAsync(limit.Token); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount);
    }
    private static IApp Create() => App.Build(c => c.UseAether(o => { o.HostContractId = new('A', 64); o.GameBuildId = new('B', 64); o.ReportDirectory = "gpu-pipe-tests"; }));
}
