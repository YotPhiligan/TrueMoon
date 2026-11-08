using System.Security.Cryptography;
using TrueMoon.Dependencies;
using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

internal sealed class NomTestDirectory : IDisposable
{
    private readonly string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-Nom-App-" + Guid.NewGuid().ToString("N"));
    internal OpenAssetStoreArguments Arguments(string name = "store") => new(Path.Combine(Root,name), Guid.NewGuid(), Guid.NewGuid(), AssetStoreOpenMode.Create);
    internal static IFactory<IProjectAssetStore,OpenAssetStoreArguments> Factory(IApp app) => app.Services.Resolve<IFactory<IProjectAssetStore,OpenAssetStoreArguments>>()!;
    internal static NomStoreHost Host(IApp app) => app.Services.Resolve<NomStoreHost>()!;
    internal static AssetRecord Record(string locator = "mesh") => new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),locator,[7,8,9]);
    internal static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static async Task<AssetResourceVersion> Publish(IProjectAssetStore store, byte[] bytes)
    {
        var asset=Record(); using var input=new MemoryStream(bytes,false);
        var ticket=await store.ReserveResourcesAsync(store.ContentGeneration,[new AssetResourceWrite(asset,bytes.Length,Digest(bytes),Math.Max(bytes.Length,1))]);
        var receipt=await store.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{asset.Id,input}});
        return Assert.Single(receipt.Resources);
    }
    internal static void AssertRootLocked(OpenAssetStoreArguments arguments) => Assert.Throws<IOException>(() =>
    {
        using var file=new FileStream(Path.Combine(arguments.Root,"owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
    });
    internal static void AssertRootReleased(OpenAssetStoreArguments arguments)
    {
        using var file=new FileStream(Path.Combine(arguments.Root,"owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.True(file.CanWrite);
    }
    internal static async Task AssertDrainTimeout(Func<Task> action)
    {
        var error=await Xunit.Record.ExceptionAsync(action);
        Assert.NotNull(error);
        Assert.Contains(Flatten(error),item=>item is TimeoutException);
    }
    private static IEnumerable<Exception> Flatten(Exception error)
    {
        yield return error;
        if(error is AggregateException aggregate)
            foreach(var inner in aggregate.InnerExceptions)
                foreach(var value in Flatten(inner)) yield return value;
        else if(error.InnerException is { } inner)
            foreach(var value in Flatten(inner)) yield return value;
    }
    public void Dispose()
    {
        var full=Path.GetFullPath(Root);
        if(!full.StartsWith(temporary+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
           !Path.GetFileName(full).StartsWith("TrueMoon-Nom-App-",StringComparison.Ordinal))
            throw new InvalidOperationException("Test root cleanup boundary.");
        if(Directory.Exists(full)) Directory.Delete(full,true);
    }
}

internal class ShortSource(byte[] bytes) : Stream
{
    private int offset;
    internal bool WasDisposed { get; private set; }
    internal int LengthRequests, SeekRequests, Reads;
    public override bool CanRead => !WasDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get { LengthRequests++; throw new NotSupportedException(); } }
    public override long Position { get { SeekRequests++; throw new NotSupportedException(); } set { SeekRequests++; throw new NotSupportedException(); } }
    public override int Read(byte[] buffer,int index,int count)
    {
        Reads++; var read=Math.Min(Math.Min(3,count),bytes.Length-offset);
        bytes.AsSpan(offset,read).CopyTo(buffer.AsSpan(index,count)); offset+=read; return read;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested(); Reads++; var read=Math.Min(Math.Min(3,buffer.Length),bytes.Length-offset);
        bytes.AsSpan(offset,read).CopyTo(buffer.Span); offset+=read; return ValueTask.FromResult(read);
    }
    protected override void Dispose(bool disposing) { WasDisposed=true; base.Dispose(disposing); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset,SeekOrigin origin) { SeekRequests++; throw new NotSupportedException(); }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
}

internal sealed class IgnoringCancellationSource : Stream
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool WasDisposed { get; private set; }
    public override bool CanRead => !WasDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
    {
        Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(5)); buffer.Span[0]=42; return 1;
    }
    public override int Read(byte[] buffer,int offset,int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { WasDisposed=true; Release.TrySetResult(); base.Dispose(disposing); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset,SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
}
