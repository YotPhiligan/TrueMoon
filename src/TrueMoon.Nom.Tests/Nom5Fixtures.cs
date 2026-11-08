using Xunit;
using TrueMoon.Dependencies;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

internal sealed class Nom5Context : IAsyncDisposable
{
    internal readonly NomTestDirectory Directory = new();
    internal readonly OpenAssetStoreArguments Arguments;
    internal readonly IApp App;
    internal readonly IFactory<IProjectAssetStore, OpenAssetStoreArguments> Factory;
    internal IProjectAssetStore Store;
    internal Nom5Context(NomFileOperations? io = null)
    {
        Arguments = Directory.Arguments(); App = TrueMoon.App.Build(context =>
        {
            context.UseNom();
            if (io != null) context.Services(services => services.RemoveAll<NomStoreHost>()
                .Singleton<NomStoreHost>(_ => new NomStoreHost(new CatalogFactory(4, io), new NomOptions { DrainTimeout = TimeSpan.FromSeconds(2) }.Snapshot())));
        });
        Factory = NomTestDirectory.Factory(App); Store = Factory.Create(Arguments);
    }
    internal async Task Reopen()
    { await Store.DisposeAsync(); Store = Factory.Create(Arguments with { Mode = AssetStoreOpenMode.Open }); }
    internal static Guid Id(int value) => Guid.Parse($"{value:x8}-1020-3040-8090-aabbccddeeff");
    internal static AssetRecord Asset(int value, Guid? source = null, Guid? kind = null, string[]? tags = null, AssetResourceVersion[]? dependencies = null)
        => new(Id(value), source ?? Id(1000), kind ?? Id(2000), "asset-" + value, [(byte)value], tags, dependencies);
    internal Task<(AssetWriteTicket Ticket, AssetResourceVersion Version)> Publish(AssetRecord asset, byte[] bytes)
        => Nom3Fixtures.Publish(Store, asset, bytes);
    internal static string ObjectPath(string root, string digest) => Path.Combine(root, "objects", "sha256", digest[..2], digest + ".blob");
    // Live catalog authority uses a read/write handle and owner.lock is intentionally exclusive.
    internal static Dictionary<string, string> Snapshot(string root) => System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetRelativePath(root, path) != "owner.lock")
        .ToDictionary(path => Path.GetRelativePath(root, path), path => NomTestDirectory.Digest(NomKvTestDirectory.ReadBytes(path)), StringComparer.Ordinal);
    internal static void AssertSnapshot(Dictionary<string, string> expected, string root)
    {
        var actual = Snapshot(root); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
    internal static async Task AssertBytes(Stream stream, byte[] expected)
    {
        using var result = new MemoryStream(); await stream.CopyToAsync(result); Assert.Equal(expected, result.ToArray());
    }
    public async ValueTask DisposeAsync()
    { await Store.DisposeAsync(); await App.DisposeAsync(); Directory.Dispose(); }
}
