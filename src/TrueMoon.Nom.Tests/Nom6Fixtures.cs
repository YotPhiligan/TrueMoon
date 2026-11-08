using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Dependencies;
using Xunit;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

internal sealed class Nom6Context : IAsyncDisposable
{
    internal readonly NomTestDirectory Directory = new();
    internal readonly Nom6Io Io;
    internal readonly IApp App;
    internal readonly OpenAssetStoreArguments Arguments;
    internal readonly IFactory<IProjectAssetStore, OpenAssetStoreArguments> Factory;
    internal readonly IFactory<IProjectAssetStore, RestoreAssetStoreArguments> RestoreFactory;
    internal IProjectAssetStore Store;
    internal string Backup => Path.Combine(Directory.Root, "backup");
    internal string Restored => Path.Combine(Directory.Root, "restored");
    internal Nom6Context(Nom6Io? io = null, long domainIndexBytes = 64L * 1024 * 1024)
    {
        Io = io ?? new(); Arguments = Directory.Arguments();
        App = TrueMoon.App.Build(c =>
        {
            c.UseNom(o => o.DomainIndexBytes = domainIndexBytes); c.Services(s => s.RemoveAll<NomStoreHost>().Singleton<NomStoreHost>(_ =>
                new NomStoreHost(new CatalogFactory(4, Io), new NomOptions { DrainTimeout = TimeSpan.FromSeconds(2), DomainIndexBytes = domainIndexBytes }.Snapshot())));
        });
        Factory = NomTestDirectory.Factory(App); RestoreFactory = App.Services.Resolve<IFactory<IProjectAssetStore, RestoreAssetStoreArguments>>()!;
        Store = Factory.Create(Arguments);
    }
    internal RestoreAssetStoreArguments RestoreArguments(string? root = null, string? backup = null, NomBackupOptions? options = null) =>
        new(backup ?? Backup, root ?? Restored, Arguments.ProjectId, Arguments.StoreId, options);
    internal Task<(AssetWriteTicket Ticket, AssetResourceVersion Version)> Publish(AssetRecord asset, byte[] bytes) => Nom3Fixtures.Publish(Store, asset, bytes);
    internal async Task Reopen() { await Store.DisposeAsync(); Store = Factory.Create(Arguments with { Mode = AssetStoreOpenMode.Open }); }
    public async ValueTask DisposeAsync() { Io.Disarm(); await Store.DisposeAsync(); await App.DisposeAsync(); Directory.Dispose(); }
    internal static void AssertActualHandlesClosed(Nom6Io io) => Assert.All(io.Opened, file => Assert.True(file.Handle.IsClosed));
}

internal sealed class Nom6Io : NomFileOperations
{
    internal readonly ConcurrentQueue<(string Point, string Path)> Events = new();
    internal readonly ConcurrentQueue<(string Path, SafeFileHandle Handle)> Opened = new();
    internal int MaximumWrite;
    internal Action<string, string>? Before, After;
    private string? faultPoint, pathPart;
    private Exception? failure;
    private int occurrence, calls, prefix;
    private bool after;
    internal void Arm(string point, Exception error, string? pathContains = null, int occurrence = 1, int prefix = 0, bool after = false)
    { faultPoint = point; failure = error; pathPart = pathContains; this.occurrence = occurrence; this.prefix = prefix; this.after = after; calls = 0; }
    internal void Disarm() { faultPoint = null; failure = null; Before = After = null; }
    private Exception? Hit(string point, string path)
    {
        Events.Enqueue((point, path)); Before?.Invoke(point, path);
        if (point != faultPoint || pathPart != null && !path.Contains(pathPart, StringComparison.OrdinalIgnoreCase)) return null;
        return ++calls == occurrence ? failure : null;
    }
    internal override FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
    {
        if (Hit("open", path) is { } error) throw error;
        var result = base.OpenFile(path, mode, access, share); Opened.Enqueue((path, result.SafeFileHandle)); After?.Invoke("open", path); return result;
    }
    internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
    {
        MaximumWrite = Math.Max(MaximumWrite, bytes.Length);
        if (Hit("write", file.Name) is { } error)
        { var count = after ? bytes.Length : Math.Min(prefix, bytes.Length); if (count > 0) base.Write(file, bytes[..count]); throw error; }
        base.Write(file, bytes); After?.Invoke("write", file.Name);
    }
    internal override void Flush(FileStream file)
    {
        if (Hit("flush", file.Name) is { } error) { if (after) base.Flush(file); throw error; }
        base.Flush(file); After?.Invoke("flush", file.Name);
    }
    internal override void Close(FileStream file)
    {
        if (Hit("close", file.Name) is { } error) { if (after) base.Close(file); throw error; }
        base.Close(file); After?.Invoke("close", file.Name);
    }
    internal override void PublishBackupDirectory(string source, string destination)
    {
        if (Hit("publish-backup", destination) is { } error) { if (after) base.PublishBackupDirectory(source, destination); throw error; }
        base.PublishBackupDirectory(source, destination); After?.Invoke("publish-backup", destination);
    }
    internal override void CreateBackupDirectory(string path)
    {
        if (Hit("create-backup", path) is { } error) throw error;
        base.CreateBackupDirectory(path); After?.Invoke("create-backup", path);
    }
    internal override void DeleteFile(string path)
    {
        if(Hit("delete",path) is { } error) {if(after)base.DeleteFile(path);throw error;}
        base.DeleteFile(path);After?.Invoke("delete",path);
    }
}
