using System.Security.Cryptography;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes;

public sealed class ScenePersistenceRegistrationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task App_DefaultPersistenceSharesServicesAndRoundTripsIndependentEngineDocuments(bool di)
    {
        using var files = new SceneFiles();
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherAuthoring();
            Assert.Same(context, context.UseAetherScenePersistence());
            Assert.Same(context, context.UseAetherScenePersistence());
        }).Build();
        await app.StartAsync();
        var codec = Assert.Single(app.Services.ResolveAll<SceneFileCodec>());
        var store = Assert.IsType<JsonSceneDocumentStore>(Assert.Single(app.Services.ResolveAll<ISceneDocumentStore>()));
        Assert.Same(codec, app.Services.Resolve<SceneFileCodec>()); Assert.Same(store, app.Services.Resolve<ISceneDocumentStore>());
        var source = Scene(); var expectedBytes = codec.Encode(source);
        var stamp = await store.WriteAsync(files.Path, source, null);
        Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(files.Path));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedBytes)), stamp.Sha256);
        var loaded = await store.ReadAsync(files.Path);
        Assert.Equal(stamp, loaded.Stamp); Assert.NotSame(source, loaded.Document);
        Assert.Equal(source.Id, loaded.Document.Id); Assert.Equal(7, loaded.Document.Revision);
        Assert.Equal(3.5d, Assert.Single(Assert.Single(loaded.Document.Definitions).Changes.AddedComponents).Properties["x"].GetNumber());
        Assert.Equal(expectedBytes, codec.Encode(loaded.Document));
        var replacement = new SceneDocument(source.Id, 8, source.Entities, source.Definitions, source.Schemas);
        var next = await store.WriteAsync(files.Path, replacement, stamp); Assert.NotEqual(stamp, next);
        var conflict = await Assert.ThrowsAsync<SceneFileConflictException>(() => store.WriteAsync(files.Path, source, stamp));
        Assert.Equal(files.Path, conflict.Path); Assert.Equal(codec.Encode(replacement), await File.ReadAllBytesAsync(files.Path));
        Assert.Equal(8, (await store.ReadAsync(files.Path)).Document.Revision);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task App_ExistingOverridesRemainLazyAndHaveOneAppOwnerAcrossRepeatedAttachment(bool di)
    {
        var authoring = new FactorySpy(); var store = new StoreSpy(); var codecActivations = 0; var storeActivations = 0;
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services
                .Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => authoring)
                .Singleton<SceneFileCodec>(resolver => { codecActivations++; return new(resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!); })
                .Singleton<ISceneDocumentStore>(_ => { storeActivations++; return store; }));
            context.UseAetherScenePersistence().UseAetherScenePersistence();
        }).Build();
        try
        {
            await app.StartAsync(); Assert.Equal((0, 0, 0), (codecActivations, storeActivations, authoring.Calls));
            var codec = Assert.Single(app.Services.ResolveAll<SceneFileCodec>());
            Assert.Same(codec, app.Services.Resolve<SceneFileCodec>()); Assert.Same(store, Assert.Single(app.Services.ResolveAll<ISceneDocumentStore>()));
            Assert.Equal((1, 1, 0), (codecActivations, storeActivations, authoring.Calls));
            Assert.Equal((0, 0), (authoring.Disposals, store.Disposals));
            await app.DisposeAsync(); await app.DisposeAsync();
            Assert.Equal((1, 1), (authoring.Disposals, store.Disposals));
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task App_DefaultServicesDoNotCreateAuthoringProductsOrFilesDuringStartup(bool di)
    {
        using var files = new SceneFiles(); var factory = new FactorySpy(); var activations = 0;
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => { activations++; return factory; }));
            context.UseAetherScenePersistence();
        }).Build();
        try
        {
            await app.StartAsync(); Assert.Equal(0, activations); Assert.Equal(0, factory.Calls); Assert.Empty(Directory.EnumerateFiles(files.Directory));
            _ = Assert.IsType<JsonSceneDocumentStore>(app.Services.Resolve<ISceneDocumentStore>());
            Assert.Equal(1, activations); Assert.Equal(0, factory.Calls); Assert.Empty(Directory.EnumerateFiles(files.Directory));
            await app.DisposeAsync(); Assert.Equal(1, factory.Disposals);
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void App_MissingAuthoringFailsBeforeActivatingPersistenceOverrides(bool di)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services
                .Singleton<SceneFileCodec>(_ => { activations++; throw new IOException("Must stay lazy."); })
                .Singleton<ISceneDocumentStore>(_ => { activations++; throw new IOException("Must stay lazy."); }));
            context.UseAetherScenePersistence();
        });
        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("Call UseAetherAuthoring before UseAetherScenePersistence.", error.Message); Assert.Equal(0, activations);
    }

    [Fact]
    public void NullContext_IsRejectedBeforeRegistration() =>
        Assert.Throws<ArgumentNullException>(() => ScenePersistenceConfigurationExtensions.UseAetherScenePersistence(null!));

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
    private static SceneDocument Scene()
    {
        var definition = new EntityDefinition(new(Guid.Parse("dc7a6591-ec61-4b0c-9e49-c270e7e22e42")), new(addedComponents:
            [new(new(Guid.Parse("51e90e09-8c7b-42a2-9395-bf774194cdca")), BuiltInComponents.TransformId, 1, [KeyValuePair.Create("x", PropertyValue.From(3.5d))])]));
        return new(new(Guid.Parse("287b2a42-f56e-4b09-9bcb-e11912ea9ae7")), 7,
            [new(new(Guid.Parse("2136f7a3-7f47-48a0-a33c-19dc1094e22e")), definition.Id, new())], [definition], BuiltInComponents.Schemas);
    }

    private sealed class FactorySpy : IFactory<ISceneAuthoringSession, SceneDocument>, IDisposable
    {
        internal int Calls, Disposals;
        public ISceneAuthoringSession Create(SceneDocument arguments) { Calls++; throw new InvalidOperationException("No authoring products during registration."); }
        public void Dispose() => Disposals++;
    }
    private sealed class StoreSpy : ISceneDocumentStore, IDisposable
    {
        internal int Disposals;
        public Task<SceneFile> ReadAsync(string path, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No IO during registration.");
        public Task<SceneFileStamp> WriteAsync(string path, SceneDocument document, SceneFileStamp? expected, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No IO during registration.");
        public void Dispose() => Disposals++;
    }
    private sealed class SceneFiles : IDisposable
    {
        internal string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrueMoon-AetherScenePersistence-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Directory, "scene.aetherscene");
        internal SceneFiles() => System.IO.Directory.CreateDirectory(Directory);
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Directory);
            var root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            if (!string.Equals(System.IO.Path.GetDirectoryName(resolved), root, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.Path.GetFileName(resolved).StartsWith("TrueMoon-AetherScenePersistence-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected scene test directory.");
            System.IO.Directory.Delete(resolved, recursive: true);
        }
    }
}
