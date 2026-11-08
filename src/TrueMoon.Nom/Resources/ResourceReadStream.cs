using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Resources;

// A borrowed caller buffer is used directly; every actual read is capped at64KiB.
internal sealed class ResourceReadStream(CatalogOwner owner, AssetResourceVersion version, CancellationToken shutdown, ResourceGate? readGate=null) : Stream
{
    private FileStream? file;
    private readonly SemaphoreSlim io = new(1,1);
    private readonly CancellationTokenSource closingToken = new();
    private readonly object lifetime = new();
    private Task? closeTask;
    private int closing;
    internal async Task InitializeAsync(string path, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, shutdown);
        linked.Token.ThrowIfCancellationRequested();
        file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length != version.Length || await ResourceCodec.HashStream(file,linked.Token).ConfigureAwait(false) != version.Digest)
            throw new InvalidDataException("Stored object length/hash mismatch.");
        linked.Token.ThrowIfCancellationRequested(); file.Position = 0;
    }
    private void Check() => ObjectDisposedException.ThrowIf(Volatile.Read(ref closing) != 0, this);
    private FileStream File => file ?? throw new InvalidOperationException("Read lease not initialized.");
    private void HoldReadForProof()
    {
        if(readGate==null) return;
        readGate.Entered.TrySetResult();
        if(!readGate.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Read proof gate.");
    }
    public override bool CanRead => Volatile.Read(ref closing) == 0;
    public override bool CanSeek => CanRead;
    public override bool CanWrite => false;
    public override long Length { get { Check(); return version.Length; } }
    public override long Position { get { Check(); io.Wait(); try { Check(); return File.Position; } finally { io.Release(); } } set => Seek(value,SeekOrigin.Begin); }
    public override int Read(byte[] buffer,int offset,int count) => Read(buffer.AsSpan(offset,count));
    public override int Read(Span<byte> buffer)
    {
        Check(); shutdown.ThrowIfCancellationRequested(); io.Wait(closingToken.Token);
        try { Check(); HoldReadForProof(); shutdown.ThrowIfCancellationRequested(); closingToken.Token.ThrowIfCancellationRequested(); return File.Read(buffer[..Math.Min(buffer.Length,ResourceCodec.BufferBytes)]); }
        finally { io.Release(); }
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Check(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,shutdown,closingToken.Token);
        await io.WaitAsync(linked.Token).ConfigureAwait(false);
        try { Check(); HoldReadForProof(); return await File.ReadAsync(buffer[..Math.Min(buffer.Length,ResourceCodec.BufferBytes)],linked.Token).ConfigureAwait(false); }
        finally { io.Release(); }
    }
    public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset,count),cancellationToken).AsTask();
    public override long Seek(long offset, SeekOrigin origin)
    {
        Check(); io.Wait(closingToken.Token);
        try
        {
            Check(); var position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => checked(File.Position+offset), SeekOrigin.End => checked(version.Length+offset), _ => -1 };
            if (position < 0 || position > version.Length) throw new IOException("Seek outside resource.");
            File.Position = position; return position;
        }
        finally { io.Release(); }
    }
    public override void Flush() { Check(); }
    public override void SetLength(long value) => throw new NotSupportedException("Read-only resource.");
    public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException("Read-only resource.");
    public override ValueTask DisposeAsync()
    {
        lock(lifetime) { Interlocked.Exchange(ref closing,1); return new(closeTask ??= CloseAsync()); }
    }
    private async Task CloseAsync()
    {
        closingToken.Cancel(); await io.WaitAsync().ConfigureAwait(false);
        try { if(file != null) await file.DisposeAsync().ConfigureAwait(false); }
        finally { io.Release(); }
        owner.ReleaseResourceReader(this); // Only after actual close; failures retain the registered lease.
    }
    protected override void Dispose(bool disposing)
    { if(disposing) DisposeAsync().AsTask().GetAwaiter().GetResult(); base.Dispose(disposing); }
}
