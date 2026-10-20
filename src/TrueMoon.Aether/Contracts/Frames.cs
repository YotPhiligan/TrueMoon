using System.Buffers.Binary;
using System.Text;

namespace TrueMoon.Aether.Contracts;

public sealed class SurfaceConfiguration
{
    public long Generation { get; set; }
    public string Mapping { get; set; } = "";
    public string Mutex { get; set; } = "";
    public string ReadyEvent { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public FrameSurface Describe(SessionIdentity identity) => new(identity, unchecked((ulong)Generation), Mapping, Mutex, ReadyEvent, Width, Height);
}

public sealed record FrameSurface(SessionIdentity Identity, ulong Generation, string Mapping, string Mutex, string ReadyEvent, int Width, int Height)
{
    public bool Suspended => Width == 0 && Height == 0;
    public int Stride => checked(Width * 4);
    public int PixelBytes => checked(Stride * Height);
    public long MappingBytes => Suspended ? 0 : checked(FrameCodec.HeaderBytes + 3L * PixelBytes);
    public void Validate()
    {
        Identity.Validate();
        if (Generation == 0) throw new InvalidDataException("Surface generation must be positive.");
        if (Suspended)
        {
            if (Mapping != "" || Mutex != "" || ReadyEvent != "") throw new InvalidDataException("Suspended surface cannot reference mappings.");
            return;
        }
        if (Width is < 16 or > 1920 || Height is < 16 or > 1080) throw new InvalidDataException("Invalid frame dimensions.");
        foreach (var name in new[] { Mapping, Mutex, ReadyEvent })
            if (name is not { Length: >= 8 and <= 100 } || !name.StartsWith("tm-frame-", StringComparison.Ordinal) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                throw new InvalidDataException("Invalid frame resource name.");
        if (new[] { Mapping, Mutex, ReadyEvent }.Distinct(StringComparer.Ordinal).Count() != 3 || MappingBytes > 25L * 1024 * 1024)
            throw new InvalidDataException("Frame capacity/names invalid.");
    }
    public SurfaceConfiguration ToWire() => new() { Generation = unchecked((long)Generation), Mapping = Mapping, Mutex = Mutex,
        ReadyEvent = ReadyEvent, Width = Width, Height = Height };
    public static FrameSurface Create(SessionIdentity identity, ulong generation, int width, int height)
    {
        var prefix = "tm-frame-" + Guid.NewGuid().ToString("N");
        var suspended = width == 0 && height == 0;
        var value = new FrameSurface(identity, generation, suspended ? "" : prefix + "-map", suspended ? "" : prefix + "-mutex",
            suspended ? "" : prefix + "-event", width, height); value.Validate(); return value;
    }
}

public enum FrameSlotState { Free, Writing, Ready, Reading }
public sealed record FrameMetadata(int Slot, ulong FrameId, ulong Tick, uint Checksum, long ProducedTimestamp);

// Explicit little-endian v1. No native struct layout, borrowed pointers or strings
// with variable length are mapped. Identity hashes are raw SHA256; Guid is ASCII N.
public static class FrameCodec
{
    public const int HeaderBytes = 1024, SlotBytes = 192, SlotsOffset = 256, SlotCount = 3;
    public static int SlotOffset(int slot) => slot is >= 0 and < SlotCount ? SlotsOffset + slot * SlotBytes : throw new ArgumentOutOfRangeException(nameof(slot));
    public static long PixelsOffset(FrameSurface surface, int slot) => checked(HeaderBytes + (long)(SlotOffset(slot) - SlotsOffset) / SlotBytes * surface.PixelBytes);
    public static void Initialize(Span<byte> bytes, FrameSurface surface)
    {
        surface.Validate(); if (surface.Suspended || bytes.Length != HeaderBytes) throw new InvalidDataException("Invalid header buffer.");
        bytes.Clear(); "TMFRAME1"u8.CopyTo(bytes);
        U32(bytes, 8, 1); U32(bytes, 12, HeaderBytes); U32(bytes, 16, SlotCount);
        Dimensions(bytes, 20, surface); U32(bytes, 36, surface.PixelBytes);
        U64(bytes, 40, surface.Identity.Generation); U64(bytes, 48, surface.Generation); U64(bytes, 56, checked((ulong)surface.MappingBytes));
        Identity(bytes[64..], surface.Identity);
        for (var i = 0; i < SlotCount; i++) WriteSlot(bytes, surface, new(i, 0, 0, 0, 0), FrameSlotState.Free);
    }
    public static void ValidateHeader(ReadOnlySpan<byte> bytes, FrameSurface surface)
    {
        surface.Validate();
        if (bytes.Length != HeaderBytes || !bytes[..8].SequenceEqual("TMFRAME1"u8) || R32(bytes, 8) != 1 || R32(bytes, 12) != HeaderBytes || R32(bytes, 16) != SlotCount ||
            !SameDimensions(bytes, 20, surface) || R32(bytes, 36) != surface.PixelBytes || R64(bytes, 40) != surface.Identity.Generation ||
            R64(bytes, 48) != surface.Generation || R64(bytes, 56) != (ulong)surface.MappingBytes || !SameIdentity(bytes[64..], surface.Identity) || R32(bytes, 160) > 1)
            throw new InvalidDataException("Corrupt/stale frame header.");
    }
    public static bool Ended(ReadOnlySpan<byte> bytes) => R32(bytes, 160) == 1;
    public static void SetEnded(Span<byte> bytes) => U32(bytes, 160, 1);
    public static FrameSlotState State(ReadOnlySpan<byte> bytes, int slot)
    {
        var state = R32(bytes, SlotOffset(slot));
        return state <= 3 ? (FrameSlotState)state : throw new InvalidDataException("Unknown frame slot state.");
    }
    public static void SetState(Span<byte> bytes, int slot, FrameSlotState state) => U32(bytes, SlotOffset(slot), (int)state);
    public static void WriteSlot(Span<byte> header, FrameSurface surface, FrameMetadata metadata, FrameSlotState state)
    {
        var b = header.Slice(SlotOffset(metadata.Slot), SlotBytes); b.Clear();
        U32(b, 0, (int)state); U32(b, 4, metadata.Slot); U64(b, 8, metadata.FrameId); U64(b, 16, metadata.Tick);
        Dimensions(b, 24, surface); BinaryPrimitives.WriteUInt32LittleEndian(b[40..], metadata.Checksum);
        BinaryPrimitives.WriteInt64LittleEndian(b[48..], metadata.ProducedTimestamp);
        U64(b, 56, surface.Identity.Generation); U64(b, 64, surface.Generation); Identity(b[72..], surface.Identity);
    }
    public static FrameMetadata ReadSlot(ReadOnlySpan<byte> header, FrameSurface surface, int slot)
    {
        var b = header.Slice(SlotOffset(slot), SlotBytes);
        if (R32(b, 4) != slot || !SameDimensions(b, 24, surface) || R64(b, 56) != surface.Identity.Generation ||
            R64(b, 64) != surface.Generation || !SameIdentity(b[72..], surface.Identity) || R64(b, 8) == 0 || BinaryPrimitives.ReadInt64LittleEndian(b[48..]) <= 0)
            throw new InvalidDataException("Corrupt/stale frame metadata.");
        return new(slot, R64(b, 8), R64(b, 16), BinaryPrimitives.ReadUInt32LittleEndian(b[40..]), BinaryPrimitives.ReadInt64LittleEndian(b[48..]));
    }
    private static void Identity(Span<byte> b, SessionIdentity identity)
    {
        Encoding.ASCII.GetBytes(identity.SessionId.ToString("N"), b[..32]);
        Convert.FromHexString(identity.GameBuildId).CopyTo(b[32..64]); Convert.FromHexString(identity.HostContractId).CopyTo(b[64..96]);
    }
    private static bool SameIdentity(ReadOnlySpan<byte> b, SessionIdentity identity)
        => Encoding.ASCII.GetString(b[..32]) == identity.SessionId.ToString("N") && b[32..64].SequenceEqual(Convert.FromHexString(identity.GameBuildId)) && b[64..96].SequenceEqual(Convert.FromHexString(identity.HostContractId));
    private static void Dimensions(Span<byte> b, int offset, FrameSurface surface)
    { U32(b, offset, surface.Width); U32(b, offset + 4, surface.Height); U32(b, offset + 8, surface.Stride); U32(b, offset + 12, surface.PixelBytes); }
    private static bool SameDimensions(ReadOnlySpan<byte> b, int offset, FrameSurface surface)
        => R32(b, offset) == surface.Width && R32(b, offset + 4) == surface.Height && R32(b, offset + 8) == surface.Stride && R32(b, offset + 12) == surface.PixelBytes;
    private static void U32(Span<byte> b, int offset, int value) => BinaryPrimitives.WriteUInt32LittleEndian(b[offset..], checked((uint)value));
    private static uint R32(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private static void U64(Span<byte> b, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(b[offset..], value);
    private static ulong R64(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(b[offset..]);
}

public static class FramePattern
{
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(i =>
    { var c = (uint)i; for (var j = 0; j < 8; j++) c = (c >> 1) ^ ((c & 1) != 0 ? 0xedb88320u : 0); return c; }).ToArray();
    public static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    { foreach (var b in bytes) crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 255]; return crc; }
    public static uint Checksum(ReadOnlySpan<byte> bytes) => ~UpdateCrc(uint.MaxValue, bytes);
    public static uint Pixel(int x, int y, int width, int height, ulong frame, ulong tick, uint color)
    {
        if (x < 6 && y < 6) return 0xffff0000;
        if (x >= width - 6 && y < 6) return 0xff00ff00;
        if (x < 6 && y >= height - 6) return 0xff0000ff;
        if (x >= width - 6 && y >= height - 6) return 0xffffffff;
        if (y == 7 && x < 64) return ((frame >> x) & 1) != 0 ? 0xffffffff : 0xff000000;
        if (y == height - 8 && x < 64) return ((tick >> x) & 1) != 0 ? 0xffffffff : 0xff000000;
        return 0xff000000 | ((color + (uint)((x / 16 + y / 16 + (int)(frame % 32)) * 0x030507)) & 0x00ffffff);
    }
    public static void FillRow(Span<byte> row, FrameSurface surface, int y, ulong frame, ulong tick, uint color)
    {
        if (row.Length != surface.Stride) throw new ArgumentException("Wrong row size.", nameof(row));
        for (var x = 0; x < surface.Width; x++) BinaryPrimitives.WriteUInt32LittleEndian(row[(x * 4)..], Pixel(x, y, surface.Width, surface.Height, frame, tick, color));
    }
}
