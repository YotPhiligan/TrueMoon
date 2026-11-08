namespace TrueMoon.Nom.Resources;

// Owned adapter, borrowed backing memory. Clearing the view never disposes its MemoryManager.
internal sealed class ReadOnlyMemoryResourceStream(ReadOnlyMemory<byte> memory) : Stream
{
    private readonly object gate = new();
    private ReadOnlyMemory<byte> source = memory;
    private int position;
    private bool disposed;

    public override bool CanRead { get { lock (gate) return !disposed; } }
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get { lock (gate) { Check(); return source.Length; } } }
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        lock (gate)
        {
            Check(); var count = Math.Min(Math.Min(buffer.Length, ResourceCodec.BufferBytes), source.Length - position);
            if (count == 0) return 0;
            source.Span.Slice(position, count).CopyTo(buffer); position += count; return count;
        }
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override void Flush() { lock (gate) Check(); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { lock (gate) { disposed = true; source = default; position = 0; } }
        base.Dispose(disposing);
    }
    private void Check() => ObjectDisposedException.ThrowIf(disposed, this);
}
