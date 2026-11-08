using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Dependencies;
using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

internal sealed class NomFaultIo : NomFileOperations
{
    private sealed class Fault(Exception error, int occurrence, bool persistent, int prefix, bool after)
    {
        internal readonly Exception Error = error;
        internal readonly int Occurrence = occurrence;
        internal readonly bool Persistent = persistent;
        internal readonly int Prefix = prefix;
        internal readonly bool After = after;
        internal int Calls;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Fault> faults = [];
    internal readonly ConcurrentQueue<(string Point, int Thread, string? Path)> Events = new();
    internal readonly ConcurrentQueue<(string Path, SafeFileHandle Handle)> OpenedCatalogFiles = new();
    internal int PartialBytesWritten { get; private set; }
    internal NomFaultGate? CloseGate;
    internal NomFaultGate? DeleteDirectoryGate;
    internal bool UseReadFaultStreams;
    internal Action? BeforeJournalWrite;
    internal void Arm(string point, Exception error, int occurrence = 1, bool persistent = false, int prefix = 0, bool after = false)
    { lock (gate) faults[point] = new(error, occurrence, persistent, prefix, after); }
    internal void Disarm() { lock (gate) faults.Clear(); BeforeJournalWrite = null; }
    private Fault? Hit(string point, string? path = null)
    {
        Events.Enqueue((point, Environment.CurrentManagedThreadId, path));
        lock (gate)
        {
            if (!faults.TryGetValue(point, out var fault)) return null;
            fault.Calls++;
            return fault.Calls == fault.Occurrence || fault.Persistent && fault.Calls > fault.Occurrence ? fault : null;
        }
    }
    internal override FileStream CreateStaging(string path)
    { if (Hit("stage-create", path) is { } f) throw f.Error; return base.CreateStaging(path); }
    private static string FileKind(string path) => Path.GetFileName(path) switch
    { "owner.lock" => "lock", "nom.root" => "root", "nom.root.pending" => "pending", _ => "journal" };
    internal override FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
    {
        var point = "open-" + FileKind(path);
        if (Hit(point, path) is { } fault) throw fault.Error;
        var file = UseReadFaultStreams ? new NomKvReadFaultStream(path, mode, access, share, BeforeRead) : base.OpenFile(path, mode, access, share);
        OpenedCatalogFiles.Enqueue((path, file.SafeFileHandle)); return file;
    }
    private void BeforeRead(string path) { if (Hit("read-" + FileKind(path), path) is { } fault) throw fault.Error; }
    internal override async ValueTask WriteStagingAsync(FileStream target, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        if (Hit("stage-write", target.Name) is { } f)
        {
            var count = Math.Min(f.Prefix, bytes.Length);
            if (count > 0) await base.WriteStagingAsync(target, bytes[..count], token);
            PartialBytesWritten += count;
            throw f.Error;
        }
        await base.WriteStagingAsync(target, bytes, token);
    }
    internal override Task FlushStagingAsync(FileStream target, CancellationToken token)
    { if (Hit("stage-flush", target.Name) is { } f) throw f.Error; return base.FlushStagingAsync(target, token); }
    internal override void FlushStagingToDisk(FileStream target)
    { if (Hit("stage-durable", target.Name) is { } f) throw f.Error; base.FlushStagingToDisk(target); }
    internal override void MoveObject(string source, string destination)
    { if (Hit("finalize", destination) is { } f) throw f.Error; base.MoveObject(source, destination); }
    internal override void DeleteStagingFile(string path)
    { if (Hit("delete-file", path) is { } f) throw f.Error; base.DeleteStagingFile(path); }
    internal override void DeleteStagingDirectory(string path)
    {
        if (Hit("delete-directory", path) is { } f) throw f.Error;
        if (DeleteDirectoryGate is { } gate)
        { gate.Entered.TrySetResult(); if (!gate.Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Staging cleanup test gate."); }
        base.DeleteStagingDirectory(path);
    }
    internal override void Close(FileStream target)
    {
        var kind = FileKind(target.Name);
        if (kind == "journal" && CloseGate is { } gate)
        { gate.Entered.TrySetResult(); if (!gate.Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Close fault-test gate."); }
        if (Hit("close-" + kind + "-before", target.Name) is { } before) throw before.Error;
        base.Close(target);
        if (Hit("close-" + kind + "-after", target.Name) is { } after) throw after.Error;
    }
    internal override void Write(FileStream target, ReadOnlyMemory<byte> bytes)
    {
        if (FileKind(target.Name) == "journal") BeforeJournalWrite?.Invoke();
        if (Hit("write-" + FileKind(target.Name), target.Name) is { } fault)
        {
            var count = fault.After ? bytes.Length : Math.Min(fault.Prefix, bytes.Length);
            if (count > 0) base.Write(target, bytes[..count]); PartialBytesWritten += count; throw fault.Error;
        }
        base.Write(target, bytes);
    }
    internal override void Flush(FileStream target)
    {
        if (Hit("flush-" + FileKind(target.Name), target.Name) is { } fault) { if (fault.After) base.Flush(target); throw fault.Error; }
        base.Flush(target);
    }
    internal override void Truncate(FileStream target, long length)
    { if (Hit("truncate", target.Name) is { } fault) throw fault.Error; base.Truncate(target, length); }
    internal override void PublishRoot(string source, string destination)
    { if (Hit("publish", destination) is { } fault) throw fault.Error; base.PublishRoot(source, destination); }
}

internal sealed class NomFaultGate
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly ManualResetEventSlim Release = new();
}

internal sealed class NomFaultContext : IAsyncDisposable
{
    internal NomTestDirectory Directory { get; } = new();
    internal NomFaultIo Io { get; } = new();
    internal IApp App { get; }
    internal NomStoreHost Host => NomTestDirectory.Host(App);
    internal IFactory<IProjectAssetStore, OpenAssetStoreArguments> Factory => NomTestDirectory.Factory(App);
    internal NomFaultContext()
    {
        var options = new NomOptions { DrainTimeout = TimeSpan.FromSeconds(2) };
        var settings = options.Snapshot();
        App = global::TrueMoon.App.Build(context =>
        {
            context.UseNom();
            context.Services(services => services.RemoveAll<NomStoreHost>()
                .Singleton<NomStoreHost>(_ => new NomStoreHost(new CatalogFactory(4, Io), settings)));
        });
    }
    public async ValueTask DisposeAsync()
    {
        Io.Disarm();
        try { await Host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); await App.DisposeAsync(); }
        finally { Directory.Dispose(); }
    }
    internal static AssetRecord Record(Guid id, string locator) => new(id, Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
        Guid.Parse("eeeeeeee-0000-0000-0000-000000000002"), locator, [0x31, 0x32]);
    internal static AssetResourceWrite Spec(AssetRecord record, byte[] bytes) => new(record, bytes.Length, NomTestDirectory.Digest(bytes), Math.Max(1, bytes.Length));
    internal static IEnumerable<Exception> Flatten(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate)
            foreach (var child in aggregate.InnerExceptions)
                foreach (var item in Flatten(child)) yield return item;
        else if (error.InnerException is { } inner)
            foreach (var item in Flatten(inner)) yield return item;
    }
    internal static async Task AssertError(Exception expected, Func<Task> action)
    {
        var error = await Xunit.Record.ExceptionAsync(async () => await action().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(error);
        Assert.Contains(Flatten(error), item => ReferenceEquals(item, expected));
    }
    internal static async Task AssertBytes(IProjectAssetStore owner, AssetResourceVersion version, byte[] expected)
    {
        await using var stream = await owner.OpenResourceAsync(version);
        using var copied = new MemoryStream(); await stream.CopyToAsync(copied);
        Assert.Equal(expected, copied.ToArray());
        Assert.Equal(expected.Length, version.Length);
        Assert.Equal(NomTestDirectory.Digest(expected), version.Digest);
    }
}

internal sealed class NomThrowingSource(Exception failure) : Stream
{
    internal bool WasDisposed { get; private set; }
    internal int Reads;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) { Reads++; throw failure; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { Reads++; return ValueTask.FromException<int>(failure); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
}
