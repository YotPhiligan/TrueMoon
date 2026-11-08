using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Probe;

internal static class StreamFixtures
{
    internal static readonly Guid Source = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly Guid GeometryKind = Guid.Parse("44444444-4444-4444-4444-444444444444");
    internal static readonly Guid ImageKind = Guid.Parse("55555555-5555-5555-5555-555555555555");
    internal static readonly Guid ShaderKind = Guid.Parse("66666666-6666-6666-6666-666666666666");
    internal const string ShaderText = "#version 450\nvoid main() { gl_Position = vec4(0.0, 0.0, 0.0, 1.0); }\n";

    internal static AssetRecord Row(string name, Guid? kind = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Source, kind ?? GeometryKind, name, [9, 8, 7]);
    internal static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string ObjectPath(string root, string digest) => Path.Combine(root, "objects", "sha256", digest[..2], digest + ".blob");

    internal static byte[] Geometry()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write("GEO1"u8); writer.Write(3u); writer.Write(3u);
        foreach (var value in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) writer.Write(value);
        foreach (var index in new uint[] { 0, 1, 2 }) writer.Write(index);
        return stream.ToArray();
    }
    internal static byte[] Image()
    {
        var bytes = new byte[58]; bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2),58);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10),54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14),40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18),1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22),1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26),1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28),24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(34),4);
        bytes[54] = 0x11; bytes[55] = 0x22; bytes[56] = 0x33;
        return bytes;
    }
    internal static byte[] Shader() => Encoding.UTF8.GetBytes(ShaderText);

    internal static string PatternDigest(long length)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        for (var i = 0; i < buffer.Length; i++) buffer[i] = PatternByte(i);
        for (long offset = 0; offset < length; offset += buffer.Length)
            hash.AppendData(buffer, 0, (int)Math.Min(buffer.Length, length-offset));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static byte PatternByte(long offset) => (byte)((offset * 31 + 17) & 255);
    internal static async Task<(long Bytes, string Digest, int LargestRead)> Consume(Stream stream)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536]; long count = 0; var largest = 0; int read;
        while ((read = await stream.ReadAsync(buffer)) != 0)
        { hash.AppendData(buffer,0,read); count += read; largest = Math.Max(largest,read); }
        return (count,Convert.ToHexStringLower(hash.GetHashAndReset()),largest);
    }
}

internal abstract class NonseekableSource : Stream
{
    internal bool WasDisposed { get; private set; }
    internal int LargestRequest { get; private protected set; }
    internal int Reads { get; private protected set; }
    internal int LengthRequests { get; private set; }
    internal int SeekRequests { get; private set; }
    public override bool CanRead => !WasDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get { LengthRequests++; throw new NotSupportedException("Length is unavailable."); } }
    public override long Position { get { SeekRequests++; throw new NotSupportedException(); } set { SeekRequests++; throw new NotSupportedException(); } }
    public override long Seek(long offset, SeekOrigin origin) { SeekRequests++; throw new NotSupportedException(); }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
    public override void Flush() { }
    protected void Observe(int requested)
    {
        ObjectDisposedException.ThrowIf(WasDisposed,this); LargestRequest = Math.Max(LargestRequest,requested); Reads++;
        if (requested > 65536) throw new InvalidDataException("Storage requested an eager oversized input buffer.");
    }
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
}

internal sealed class ByteSource(byte[] bytes,int maximumChunk = 7) : NonseekableSource
{
    private int offset;
    public override int Read(byte[] buffer,int start,int count) => Read(buffer.AsSpan(start,count));
    public override int Read(Span<byte> buffer)
    {
        Observe(buffer.Length); var n = Math.Min(Math.Min(buffer.Length,maximumChunk),bytes.Length-offset);
        bytes.AsSpan(offset,n).CopyTo(buffer); offset += n; return n;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(Read(buffer.Span)); }
}

internal sealed class PatternSource(long length) : NonseekableSource
{
    private long offset;
    public override int Read(byte[] buffer,int start,int count) => Read(buffer.AsSpan(start,count));
    public override int Read(Span<byte> buffer)
    {
        Observe(buffer.Length); var n = (int)Math.Min(buffer.Length,length-offset);
        for (var i=0;i<n;i++) buffer[i] = StreamFixtures.PatternByte(offset+i);
        offset += n; return n;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(Read(buffer.Span)); }
}

internal sealed class ThrowingSource : NonseekableSource
{
    public override int Read(byte[] buffer,int offset,int count) { Observe(count); throw new IOException("Injected source I/O failure."); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token = default)
    { Observe(buffer.Length); throw new IOException("Injected source I/O failure."); }
}

internal sealed class MustNotReadSource : NonseekableSource
{
    public override int Read(byte[] buffer,int offset,int count) { Observe(count); throw new InvalidOperationException("Idempotent retry must not reread input."); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token = default)
    { Observe(buffer.Length); throw new InvalidOperationException("Idempotent retry must not reread input."); }
}

internal sealed class IgnoringCancellationSource : NonseekableSource
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool returned;
    public override int Read(byte[] buffer,int offset,int count) => throw new NotSupportedException("Proof uses asynchronous input.");
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token = default)
    {
        Observe(buffer.Length);
        if (returned) return 0;
        Entered.TrySetResult(); await Release.Task.ConfigureAwait(false); // Deliberately ignores token until actual I/O completion.
        returned = true; var n = Math.Min(buffer.Length,4); buffer.Span[..n].Fill(42); return n;
    }
}
