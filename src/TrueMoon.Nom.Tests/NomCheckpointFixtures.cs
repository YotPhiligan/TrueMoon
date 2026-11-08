using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

internal static class NomCheckpointFixtures
{
    // Explicit RFC-Guid/LE-counter field bytes plus independently calculated SHA256 footer.
    private const string GoldenHex = "4e4f4d434b503031010080000000000000112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f0200000000000000070000000000000003000000000000000500000000000000000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f03000000000000000000000000000000010000000200000001102001000000000000007f0200000003000000800000ff018beb5bb30ddb7f694a01e3959e162c2b6e4561a4d230319d4e9b2724301f76b6";
    internal const string GoldenDigest = "826c84d1ce2ebe935ba0905d8ff2ccf441a157207362fa3a97dcc32dd042f804";
    internal const string PreviousDigest = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    internal static byte[] Golden => Convert.FromHexString(GoldenHex);
    internal static NomCheckpointHeader Header => new(NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId, 2, 7, 3, 5, PreviousDigest, 3);
    internal static NomRootDescriptor Root => new(NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId, Epoch: 2,
        CheckpointSequence: 7, CheckpointGeneration: 3, CheckpointDigest: GoldenDigest, FirstSegmentId: 6,
        ActiveSegmentId: 6, MinimumSequence: 7, PreviousSegmentHash: PreviousDigest);
    internal static NomKvPair[] Entries => [new([1], [0x10, 0x20]), new([0x7f], []), new([0x80, 0], [0, 0xff, 1])];
    internal static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static NomRootDescriptor Authenticate(byte[] bytes)
    {
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)).CopyTo(bytes, bytes.Length - 32);
        return Root with { CheckpointDigest = Digest(bytes) };
    }
    internal static void Counter(byte[] bytes, int offset, long value) => BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), value);
    internal static void Size(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}

internal sealed class NomCheckpointProbeStream : MemoryStream
{
    internal bool WasDisposed;
    internal int Reads, MaximumRequest, Writes;
    internal int ReadChunk = int.MaxValue;
    internal Action? AfterRead;
    internal NomCheckpointProbeStream() { }
    internal NomCheckpointProbeStream(byte[] bytes) : base(bytes, writable: false) { }
    public override int Read(Span<byte> buffer)
    {
        Reads++; MaximumRequest = Math.Max(MaximumRequest, buffer.Length);
        var count = base.Read(buffer[..Math.Min(buffer.Length, ReadChunk)]); AfterRead?.Invoke(); return count;
    }
    public override void Write(ReadOnlySpan<byte> bytes) { Writes++; base.Write(bytes); }
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
}

internal sealed class NomCheckpointIo : NomFileOperations
{
    private sealed class Fault(Exception error, int occurrence, bool persistent, int prefix, bool after)
    {
        internal readonly Exception Error = error;
        internal readonly int Occurrence = occurrence, Prefix = prefix;
        internal readonly bool Persistent = persistent, After = after;
        internal int Calls;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Fault> faults = [];
    internal readonly ConcurrentQueue<(string Point, string Path)> Events = new();
    internal readonly ConcurrentQueue<(string Path, SafeFileHandle Handle)> Opened = new();
    internal Action<string, string>? Before, After;
    internal void Arm(string point, Exception error, int occurrence = 1, bool persistent = false, int prefix = 0, bool after = false)
    { lock (gate) faults[point] = new(error, occurrence, persistent, prefix, after); }
    internal void Disarm() { lock (gate) faults.Clear(); Before = After = null; }
    private Fault? Hit(string point, string path)
    {
        Events.Enqueue((point, path)); Before?.Invoke(point, path);
        lock (gate)
        {
            if (!faults.TryGetValue(point, out var fault)) return null;
            fault.Calls++; return fault.Calls == fault.Occurrence || fault.Persistent && fault.Calls > fault.Occurrence ? fault : null;
        }
    }
    internal static string Kind(string path)
    {
        var name = Path.GetFileName(path);
        if (name == "owner.lock") return "lock";
        if (name == "nom.root") return "root";
        if (name == "nom.root.switch") return "switch";
        if (name == "nom.root.pending") return "create-root";
        var checkpoint = Path.GetFileName(Path.GetDirectoryName(path)) == "checkpoints";
        return checkpoint ? name.EndsWith(".pending", StringComparison.Ordinal) ? "checkpoint-pending" : "checkpoint"
            : name.EndsWith(".pending", StringComparison.Ordinal) ? "segment-pending" : "journal";
    }
    internal override FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
    {
        var point = "open-" + Kind(path); if (Hit(point, path) is { } fault) throw fault.Error;
        var file = base.OpenFile(path, mode, access, share); Opened.Enqueue((path, file.SafeFileHandle)); After?.Invoke(point, path); return file;
    }
    internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
    {
        var point = "write-" + Kind(file.Name);
        if (Hit(point, file.Name) is { } fault)
        {
            var count = fault.After ? bytes.Length : Math.Min(fault.Prefix, bytes.Length);
            if (count > 0) base.Write(file, bytes[..count]); throw fault.Error;
        }
        base.Write(file, bytes); After?.Invoke(point, file.Name);
    }
    internal override void Flush(FileStream file)
    {
        var point = "flush-" + Kind(file.Name);
        if (Hit(point, file.Name) is { } fault) { if (fault.After) base.Flush(file); throw fault.Error; }
        base.Flush(file); After?.Invoke(point, file.Name);
    }
    internal override void Close(FileStream file)
    {
        var point = "close-" + Kind(file.Name);
        if (Hit(point, file.Name) is { } fault) { if (fault.After) base.Close(file); throw fault.Error; }
        base.Close(file); After?.Invoke(point, file.Name);
    }
    internal override void PublishRoot(string source, string destination)
    {
        var point = "publish-" + Kind(destination);
        if (Hit(point, destination) is { } fault) { if (fault.After) base.PublishRoot(source, destination); throw fault.Error; }
        base.PublishRoot(source, destination); After?.Invoke(point, destination);
    }
    internal override void ReplaceRoot(string source, string destination)
    {
        if (Hit("replace-root", destination) is { } fault) { if (fault.After) base.ReplaceRoot(source, destination); throw fault.Error; }
        base.ReplaceRoot(source, destination); After?.Invoke("replace-root", destination);
    }
    internal override void DeleteFile(string path)
    {
        if (Hit("delete", path) is { } fault) { if (fault.After) base.DeleteFile(path); throw fault.Error; }
        base.DeleteFile(path); After?.Invoke("delete", path);
    }
    internal override void Truncate(FileStream file, long length)
    { if (Hit("truncate", file.Name) is { } fault) throw fault.Error; base.Truncate(file, length); }
}

internal sealed class NomCheckpointContext : IDisposable
{
    internal readonly NomKvTestDirectory Directory = new();
    internal readonly NomCheckpointIo Io = new();
    internal readonly NomKvStoreFactory Factory;
    internal NomCheckpointContext() => Factory = new(Io);
    public void Dispose() { Io.Disarm(); Factory.Dispose(); Directory.Dispose(); }
}
