using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Engine.Probe.Contracts;

namespace EngineSessionProbe;

// One producer and one receiver. Dispose only after their workers have drained.
public sealed class SharedFrameRing : IDisposable
{
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private readonly Mutex _mutex;
    private readonly byte[] _header = new byte[FrameCodec.HeaderBytes];
    private readonly object _local = new();
    private Exception? _failure;
    public FrameSurface Surface { get; }
    public EventWaitHandle Ready { get; }
    public long Dropped { get; private set; }
    public long Superseded { get; private set; }
    private SharedFrameRing(FrameSurface surface, bool create)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Named frame rings require Windows.");
        surface.Validate(); if (surface.Suspended) throw new InvalidDataException("Suspended surfaces have no ring.");
        Surface = surface;
        _mutex = create ? new Mutex(false, surface.Mutex, out var fresh) : Mutex.OpenExisting(surface.Mutex);
        try
        {
            Ready = create ? new EventWaitHandle(false, EventResetMode.AutoReset, surface.ReadyEvent) : EventWaitHandle.OpenExisting(surface.ReadyEvent);
            try
            {
                _map = create ? MemoryMappedFile.CreateNew(surface.Mapping, surface.MappingBytes) : MemoryMappedFile.OpenExisting(surface.Mapping);
                try
                {
                    _view = _map.CreateViewAccessor(0, surface.MappingBytes);
                    if (create) { FrameCodec.Initialize(_header, surface); _view.WriteArray(0, _header, 0, _header.Length); }
                    else
                    {
                        try { Metadata(_ => { }); } catch { _view.Dispose(); throw; }
                    }
                }
                catch { _map.Dispose(); throw; }
            }
            catch { Ready.Dispose(); throw; }
        }
        catch { _mutex.Dispose(); throw; }
    }
    public static SharedFrameRing Create(FrameSurface surface) => new(surface, true);
    public static SharedFrameRing Open(FrameSurface surface) => new(surface, false);
    private T Metadata<T>(Func<byte[], T> action)
    {
        lock (_local)
        {
            var owned = false;
            try
            {
                if (_failure != null) throw new InvalidDataException("Frame transport is terminal.", _failure);
                try { owned = _mutex.WaitOne(100); }
                catch (AbandonedMutexException) { owned = true; throw new InvalidDataException("Abandoned frame metadata mutex."); }
                if (!owned) throw new TimeoutException("Frame metadata mutex unavailable for 100ms.");
                if (_view.ReadArray(0, _header, 0, _header.Length) != _header.Length) throw new EndOfStreamException();
                FrameCodec.ValidateHeader(_header, Surface);
                // Reject unknown states even if that slot would not be selected.
                for (var i = 0; i < FrameCodec.SlotCount; i++) _ = FrameCodec.State(_header, i);
                var result = action(_header);
                _view.WriteArray(0, _header, 0, _header.Length);
                return result;
            }
            catch (Exception e) { _failure ??= e; throw; }
            finally { if (owned) _mutex.ReleaseMutex(); }
        }
    }
    private void Metadata(Action<byte[]> action) => Metadata(h => { action(h); return 0; });
    public bool WritePattern(ulong frame, ulong tick, uint color, byte[] row)
    {
        if (frame == 0 || row.Length != Surface.Stride) throw new ArgumentException("Invalid frame/row.");
        var slot = Metadata(h =>
        {
            if (FrameCodec.Ended(h)) throw new EndOfStreamException("Frame producer detached.");
            for (var i = 0; i < FrameCodec.SlotCount; i++)
                if (FrameCodec.State(h, i) == FrameSlotState.Free) { FrameCodec.SetState(h, i, FrameSlotState.Writing); return i; }
            return -1;
        });
        if (slot < 0) { Dropped++; return false; }
        var timestamp = Stopwatch.GetTimestamp(); var crc = uint.MaxValue;
        for (var y = 0; y < Surface.Height; y++)
        {
            FramePattern.FillRow(row, Surface, y, frame, tick, color); crc = FramePattern.UpdateCrc(crc, row);
            _view.WriteArray(FrameCodec.PixelsOffset(Surface, slot) + (long)y * row.Length, row, 0, row.Length);
        }
        Metadata(h =>
        {
            if (FrameCodec.State(h, slot) != FrameSlotState.Writing) throw new InvalidDataException("Lost Writing slot.");
            FrameCodec.WriteSlot(h, Surface, new(slot, frame, tick, ~crc, timestamp), FrameSlotState.Ready);
        });
        Ready.Set(); return true;
    }
    public FrameMetadata? CopyLatest(byte[] destination, ulong after)
    {
        if (destination.Length != Surface.PixelBytes) throw new ArgumentException("Invalid frame buffer.", nameof(destination));
        try { return CopyLatestCore(destination, after); }
        catch (Exception e) { lock (_local) _failure ??= e; throw; }
    }
    private FrameMetadata? CopyLatestCore(byte[] destination, ulong after)
    {
        var selected = Metadata(h =>
        {
            if (FrameCodec.Ended(h)) throw new EndOfStreamException("Runtime frame transport ended.");
            FrameMetadata? latest = null;
            for (var i = 0; i < FrameCodec.SlotCount; i++)
                if (FrameCodec.State(h, i) == FrameSlotState.Ready)
                {
                    var m = FrameCodec.ReadSlot(h, Surface, i);
                    if (m.ProducedTimestamp > Stopwatch.GetTimestamp()) throw new InvalidDataException("Frame timestamp is in the future.");
                    if (m.FrameId > after && (latest == null || m.FrameId > latest.FrameId)) latest = m;
                }
            for (var i = 0; i < FrameCodec.SlotCount; i++)
                if (FrameCodec.State(h, i) == FrameSlotState.Ready && i != latest?.Slot)
                { FrameCodec.SetState(h, i, FrameSlotState.Free); Superseded++; }
            if (latest != null) FrameCodec.SetState(h, latest.Slot, FrameSlotState.Reading);
            return latest;
        });
        if (selected == null) return null;
        try
        {
            if (_view.ReadArray(FrameCodec.PixelsOffset(Surface, selected.Slot), destination, 0, destination.Length) != destination.Length)
                throw new EndOfStreamException("Incomplete frame copy.");
            if (FramePattern.Checksum(destination) != selected.Checksum) throw new InvalidDataException("Frame CRC mismatch.");
            return selected;
        }
        finally
        {
            Metadata(h =>
            {
                if (FrameCodec.State(h, selected.Slot) != FrameSlotState.Reading) throw new InvalidDataException("Lost Reading slot.");
                FrameCodec.SetState(h, selected.Slot, FrameSlotState.Free);
            });
        }
    }
    public void End() { Metadata(h => FrameCodec.SetEnded(h)); Ready.Set(); }
    public void Dispose() { _view.Dispose(); _map.Dispose(); Ready.Dispose(); _mutex.Dispose(); }
}
