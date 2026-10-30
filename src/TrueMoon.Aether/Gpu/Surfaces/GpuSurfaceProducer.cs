using System.IO.Pipes;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Gpu.Vulkan;

namespace TrueMoon.Aether.Gpu.Surfaces;

/// <summary>Bounded aggregate including all surface teardown validation and live native ownership.</summary>
public sealed record GpuSurfaceProducerDiagnostics(bool ValidationEnabled, int Errors, int Warnings, IReadOnlyList<string> Messages,
    int SurfacesCreated, int SurfacesReleased, int SlotsCreated, int SlotsReleased, int LiveSlots, int OpenHandles, int DevicesCreated, int DevicesReleased);

internal sealed record GpuSurfaceMessage(ulong Sequence, string Kind, SessionIdentity Identity, ulong Generation = 0,
    int Width = 0, int Height = 0, GpuAdapterId? Adapter = null, GpuSurfaceOffer? Offer = null,
    GpuFrameCompletion? Completion = null, string? Error = null, FrameMetadata? Metadata = null);

internal static class GpuSurfaceProducer
{
    internal static async Task RunAsync(GpuResourceHost host, SessionLaunch launch, CancellationToken token,
        Func<string, CancellationToken, Task>? beforeOperation, bool validation, Func<GpuFrameStamp, int, int, GpuFrameData>? frameProvider)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, host.End); var end = linked.Token;
        await using var pipe = new NamedPipeServerStream(launch.Endpoint + "-gpu-surface", PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        VulkanSurface? surface = null; GpuSlotLedger? ledger = null; ulong sequence = 0, generation = 0;
        var created = 0; var released = 0; var slotsCreated = 0; var slotsReleased = 0; var devicesCreated = 0; var devicesReleased = 0; var errors = 0; var warnings = 0; var messages = new List<string>();
        async Task DestroySurface()
        {
            if (surface == null) return;
            await surface.DisposeAsync().ConfigureAwait(false);
            var diagnostic = surface.Diagnostics; errors += diagnostic.Errors; warnings += diagnostic.Warnings;
            messages.AddRange(diagnostic.Messages.Take(Math.Max(0, 64 - messages.Count))); released++; slotsReleased += surface.SlotCount;
            devicesCreated += surface.DeviceCreations; devicesReleased += surface.DeviceReleases;
            surface = null; ledger = null;
        }
        try
        {
            launch.Identity.Validate(); await pipe.WaitForConnectionAsync(end).ConfigureAwait(false);
            while (true)
            {
                GpuSurfaceMessage? request;
                try { request = await GpuWire.ReadMessageOrEndAsync<GpuSurfaceMessage>(pipe, end).ConfigureAwait(false); }
                catch (IOException error) when (GpuWire.IsPeerClosed(error)) { break; }
                if (request == null) break;
                if (request.Identity != launch.Identity || request.Sequence != checked(++sequence)) throw new InvalidDataException("Surface request identity/sequence mismatch.");
                if (beforeOperation != null) await beforeOperation(request.Kind, end).ConfigureAwait(false);
                GpuSurfaceMessage reply;
                switch (request.Kind)
                {
                    case "Configure":
                        GpuSurfaceOffer.ValidateExtent(request.Width, request.Height);
                        if (surface != null || request.Generation != checked(generation + 1)) throw new InvalidDataException("Detach the prior GPU surface before configuring a fresh generation.");
                        generation = request.Generation;
                        if (request.Width == 0) { reply = new(sequence, "Suspended", launch.Identity, generation); break; }
                        var adapter = request.Adapter ?? throw new InvalidDataException("Missing surface adapter.");
                        surface = host.Own(new VulkanSurface(launch.Identity, generation, adapter, request.Width, request.Height, validation));
                        created++;
                        try { await surface.InitializeAsync(end).ConfigureAwait(false); host.ObserveDevice(surface.DeviceObservation); }
                        finally { slotsCreated += surface.SlotCount; }
                        ledger = new(launch.Identity, generation); reply = new(sequence, "Surface", launch.Identity, generation, Offer: surface.Offer); break;
                    case "Imported":
                        RequireGeneration(); ledger!.Imported(); reply = new(sequence, "Imported", launch.Identity, generation); break;
                    case "Produce":
                        RequireGeneration();
                        var stamp = request.Completion?.Stamp ?? throw new InvalidDataException("Missing slot stamp.");
                        GpuSlotLedger.ValidateStamp(stamp, launch.Identity);
                        // Preflight the exact next use before changing any ledger/native state.
                        var previous = ledger!.Slots[stamp.Slot];
                        if (stamp.SurfaceGeneration != generation || previous.Phase != "Idle" || stamp.FrameId != checked((previous.Stamp?.FrameId ?? 0) + 1)) throw new InvalidDataException("Stale/busy slot use.");
                        if (ledger.Begin(stamp.Slot) != stamp) throw new InvalidDataException("Slot reservation mismatch.");
                        var extent = surface!.Offer.Slots[stamp.Slot];
                        var measured = FrameTrace.Log.Begin();
                        var data = frameProvider?.Invoke(stamp, extent.Width, extent.Height);
                        if (frameProvider != null && data == null) throw new InvalidDataException("Runtime frame provider returned null.");
                        data?.Validate(stamp, extent.Width, extent.Height);
                        var ready = await surface.ProduceAsync(stamp, end, data?.Pixels, data?.Scene).ConfigureAwait(false); ledger.Produced(ready);
                        FrameTrace.Log.Complete("production", launch.Identity, generation, GpuFrameData.FrameNumber(stamp), measured);
                        reply = new(sequence, "Ready", launch.Identity, generation, Completion: ready, Metadata: data?.Metadata); break;
                    case "Return":
                        RequireGeneration(); var complete = request.Completion ?? throw new InvalidDataException("Missing slot return."); ledger!.RemoteReturn(complete);
                        var retired = await surface!.RetireAsync(complete, end).ConfigureAwait(false); ledger.Retired(retired);
                        reply = new(sequence, "Retired", launch.Identity, generation, Completion: retired); break;
                    case "Detach":
                        RequireGeneration(); if (!ledger!.IsIdle) throw new InvalidOperationException("GPU surface has outstanding slots.");
                        await DestroySurface().ConfigureAwait(false); reply = new(sequence, "Detached", launch.Identity, generation); break;
                    default: throw new InvalidDataException("Unknown surface operation.");
                }
                try { await GpuWire.WriteMessageAsync(pipe, reply, end).ConfigureAwait(false); }
                catch (IOException error) when (GpuWire.IsPeerClosed(error)) { break; }
                void RequireGeneration()
                { if (surface == null || ledger == null || request.Generation != generation) throw new InvalidDataException("Missing/stale surface generation."); }
            }
        }
        catch (Exception error)
        {
            ledger?.Fault();
            using var errorEnd = CancellationTokenSource.CreateLinkedTokenSource(end); errorEnd.CancelAfter(TimeSpan.FromSeconds(2));
            if (pipe.IsConnected && !end.IsCancellationRequested)
                try { await GpuWire.WriteMessageAsync(pipe, new GpuSurfaceMessage(sequence, "Error", launch.Identity, generation, Error: error.Message[..Math.Min(error.Message.Length, 4096)]), errorEnd.Token).ConfigureAwait(false); }
                catch (Exception writeError) when (writeError is IOException or OperationCanceledException) { }
            throw;
        }
        finally
        {
            try { await DestroySurface().ConfigureAwait(false); }
            finally
            {
                var live = surface; var diagnostic = live?.Diagnostics;
                host.LastSurfaceProducer = new(validation, errors + (diagnostic?.Errors ?? 0), warnings + (diagnostic?.Warnings ?? 0),
                    Array.AsReadOnly(messages.Concat(diagnostic?.Messages ?? []).Take(64).ToArray()), created, released, slotsCreated, slotsReleased,
                    live?.LiveSlots ?? 0, host.OpenHandleCount, devicesCreated + (live?.DeviceCreations ?? 0), devicesReleased + (live?.DeviceReleases ?? 0));
            }
        }
    }
}

internal sealed class VulkanSurface(SessionIdentity identity, ulong generation, GpuAdapterId adapter, int width, int height, bool validation) : IGpuResource
{
    private readonly List<VulkanSharedFrame> _slots = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool IsReleased { get; private set; }
    internal int SlotCount => _slots.Count;
    internal GpuDeviceObservation? DeviceObservation => _slots.Count == 0 ? null : _slots[0].DeviceObservation;
    internal int DeviceCreations => _slots.Count == 0 ? 0 : _slots[0].DeviceCreations;
    internal int DeviceReleases => _slots.Count == 0 ? 0 : _slots[0].DeviceReleases;
    internal int LiveSlots => _slots.Count(s => !s.IsReleased);
    internal GpuProducerDiagnostics Diagnostics => _slots.Count == 0 ? new(validation, 0, 0, [], true, 0) : _slots[0].Diagnostics;
    internal GpuSurfaceOffer Offer => new(generation, _slots.Select(s => s.Offer).ToArray());
    internal async Task InitializeAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsReleased, this);
            for (var slot = 0; slot < 3; slot++)
            {
                var frame = new VulkanSharedFrame(new(identity, generation, 1, slot, 2), adapter, width, height, validation, slot == 0 ? null : _slots[0]);
                _slots.Add(frame); await frame.InitializeAsync(token).ConfigureAwait(false);
            }
            Offer.Validate(identity, adapter, generation, width, height);
        }
        finally { _gate.Release(); }
    }
    internal Task<GpuFrameCompletion> ProduceAsync(GpuFrameStamp stamp, CancellationToken token, byte[]? pixels = null, Rendering.PrimitiveScene? scene = null) => ExecuteAsync(() => _slots[stamp.Slot].ProduceSlotAsync(stamp, token, pixels, scene), token);
    internal Task<GpuFrameCompletion> RetireAsync(GpuFrameCompletion completion, CancellationToken token) => ExecuteAsync(() => _slots[completion.Stamp.Slot].RetireAsync(completion, token), token);
    private async Task<GpuFrameCompletion> ExecuteAsync(Func<Task<GpuFrameCompletion>> body, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(IsReleased, this); return await body().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return; List<Exception> errors = [];
            // A failed child cleanup retains the shared device owner for an explicit retry.
            foreach (var frame in _slots.Skip(1).Reverse()) try { await frame.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0 && _slots.Count != 0) try { await _slots[0].DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("GPU surface cleanup retained ownership.", errors);
            IsReleased = true;
        }
        finally { _gate.Release(); }
    }
}
