using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuSurfacePipeTests
{
    private static SessionIdentity Identity() => new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64));
    [Theory]
    [InlineData("identity")] [InlineData("sequence")] [InlineData("unknown")] [InlineData("generation")]
    [InlineData("partial-zero")] [InlineData("imported")] [InlineData("produce")] [InlineData("return")] [InlineData("detach")]
    public async Task InvalidSurfaceRequest_CannotAllocateNativeResources(string invalid)
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-surface-tests-" + Guid.NewGuid().ToString("N");
        var worker = gpu.RunSurfaceProducerAsync(new(identity, endpoint, "surface-pipe-tests"), CancellationToken.None);
        using var end = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu-surface", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); await pipe.ConnectAsync(end.Token);
        var kind = invalid switch { "unknown" => "Unknown", "imported" => "Imported", "produce" => "Produce", "return" => "Return", "detach" => "Detach", _ => "Configure" };
        await Write(pipe, new { Sequence = invalid == "sequence" ? 2ul : 1ul, Kind = kind, Identity = invalid == "identity" ? identity with { Generation = 2 } : identity,
            Generation = invalid == "generation" ? 0ul : 1ul, Width = 0, Height = invalid == "partial-zero" ? 16 : 0 }, end.Token);
        var reply = await Read(pipe, end.Token); Assert.Equal("Error", reply.GetProperty("Kind").GetString());
        if (invalid == "partial-zero") await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => worker.WaitAsync(end.Token));
        else await Assert.ThrowsAsync<InvalidDataException>(() => worker.WaitAsync(end.Token));
        await app.StopAsync(); var result = gpu.LastSurfaceProducer!;
        Assert.Equal(0, result.SurfacesCreated); Assert.Equal(0, result.SlotsCreated); Assert.Equal(0, result.DevicesCreated);
        Assert.Equal(0, result.LiveSlots); Assert.Equal(0, result.OpenHandles); Assert.Equal(0, gpu.ResourceCount); Assert.Null(gpu.CleanupError);
    }
    [Theory]
    [InlineData(1ul)] [InlineData(3ul)]
    public async Task SuspendedSurface_RejectsRepeatedOrSkippedGenerationWithoutNativeWork(ulong next)
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-surface-tests-" + Guid.NewGuid().ToString("N");
        var worker = gpu.RunSurfaceProducerAsync(new(identity, endpoint, "surface-pipe-tests"), CancellationToken.None);
        using var end = new CancellationTokenSource(TimeSpan.FromSeconds(5)); using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu-surface", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(end.Token);
        await Write(pipe, new { Sequence = 1ul, Kind = "Configure", Identity = identity, Generation = 1ul }, end.Token);
        var first = await Read(pipe, end.Token); Assert.Equal("Suspended", first.GetProperty("Kind").GetString()); Assert.Equal(1ul, first.GetProperty("Generation").GetUInt64()); Assert.Equal(JsonValueKind.Null, first.GetProperty("Offer").ValueKind);
        await Write(pipe, new { Sequence = 2ul, Kind = "Configure", Identity = identity, Generation = next }, end.Token);
        var second = await Read(pipe, end.Token); Assert.Equal("Error", second.GetProperty("Kind").GetString());
        await Assert.ThrowsAsync<InvalidDataException>(() => worker.WaitAsync(end.Token)); await app.StopAsync();
        Assert.Equal(0, gpu.LastSurfaceProducer!.DevicesCreated); Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount);
    }
    [Fact]
    public async Task AppClose_DrainsSuspendedSurfaceReaderAndRetainsExactZeroAllocationDiagnostics()
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-surface-tests-" + Guid.NewGuid().ToString("N");
        var worker = gpu.RunSurfaceProducerAsync(new(identity, endpoint, "surface-pipe-tests"), CancellationToken.None);
        using var end = new CancellationTokenSource(TimeSpan.FromSeconds(5)); using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu-surface", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(end.Token);
        foreach (var generation in new[] { 1ul, 2ul })
        {
            await Write(pipe, new { Sequence = generation, Kind = "Configure", Identity = identity, Generation = generation }, end.Token);
            var reply = await Read(pipe, end.Token); Assert.Equal("Suspended", reply.GetProperty("Kind").GetString()); Assert.Equal(generation, reply.GetProperty("Generation").GetUInt64());
        }
        Assert.False(worker.IsCompleted); await app.StopAsync().WaitAsync(end.Token); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        var result = gpu.LastSurfaceProducer!; Assert.False(result.ValidationEnabled); Assert.Equal(0, result.Errors); Assert.Equal(0, result.Warnings);
        Assert.Equal(0, result.SurfacesCreated); Assert.Equal(0, result.SurfacesReleased); Assert.Equal(0, result.SlotsCreated); Assert.Equal(0, result.SlotsReleased);
        Assert.Equal(0, result.DevicesCreated); Assert.Equal(0, result.DevicesReleased); Assert.Equal(0, result.LiveSlots); Assert.Equal(0, result.OpenHandles);
        Assert.Empty(result.Messages); Assert.Equal(0, gpu.ResourceCount); Assert.Null(gpu.CleanupError);
    }
    [Fact]
    public async Task AppClose_CancelsAcceptedConfigureHookBeforeGpuAllocation()
    {
        await using var app = Create(); await app.StartAsync(); var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
        var identity = Identity(); var endpoint = "engine-surface-tests-" + Guid.NewGuid().ToString("N"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = gpu.RunSurfaceProducerAsync(new(identity, endpoint, "surface-pipe-tests"), CancellationToken.None,
            async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); });
        using var end = new CancellationTokenSource(TimeSpan.FromSeconds(5)); using var pipe = new NamedPipeClientStream(".", endpoint + "-gpu-surface", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(end.Token); await Write(pipe, new { Sequence = 1ul, Kind = "Configure", Identity = identity, Generation = 1ul, Width = 64, Height = 32 }, end.Token);
        await entered.Task.WaitAsync(end.Token); Assert.False(worker.IsCompleted); await app.StopAsync().WaitAsync(end.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker); Assert.Equal(0, gpu.LastSurfaceProducer!.SlotsCreated); Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount);
    }
    private static async Task Write(Stream stream, object message, CancellationToken token)
    { var bytes = JsonSerializer.SerializeToUtf8Bytes(message); var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length); await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); }
    private static async Task<JsonElement> Read(Stream stream, CancellationToken token)
    { var header = new byte[4]; await stream.ReadExactlyAsync(header, token); var bytes = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)]; await stream.ReadExactlyAsync(bytes, token); return JsonSerializer.Deserialize<JsonElement>(bytes); }
    private static IApp Create() => App.Build(c => c.UseAether(o => { o.HostContractId = new('A', 64); o.GameBuildId = new('B', 64); o.ReportDirectory = "surface-pipe-tests"; }));
}
