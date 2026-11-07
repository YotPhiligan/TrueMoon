using System.Runtime.Loader;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class SceneWorldGameBuildLifetimeTests(GameBuildFixture fixture) : IClassFixture<GameBuildFixture>
{
    [Fact]
    public void FailedGameDisposal_RetainsSessionContextAndArtifactPinUntilSuccessfulRetry()
    {
        var artifact = fixture.CopyA();
        var expected = new IOException("game scene cleanup fixture");
        var game = new ThrowOnceGame(expected); var creations = 0; var unloads = 0;
        AssemblyLoadContext? context = null;
        var sessions = new Factory<IGameSession, Type>(type =>
        {
            creations++;
            Assert.Equal(artifact.EntryType, type.FullName);
            context = AssemblyLoadContext.GetLoadContext(type.Assembly);
            Assert.NotNull(context);
            Assert.True(context.IsCollectible);
            context.Unloading += _ => unloads++;
            return game;
        });
        var owner = GameBuildLoader.Load(new(artifact.DirectoryPath, artifact.BuildId), GameBuildFixture.Hash, sessions);
        var manifest = Path.Combine(artifact.DirectoryPath, GameBuildArtifact.ManifestFile);
        var retired = manifest + ".retired";
        try
        {
            Assert.Same(game, owner.Session);
            Assert.Equal(1, creations);
            Assert.Throws<IOException>(() => File.Move(manifest, retired));
            Assert.Same(expected, Assert.Throws<IOException>(() => owner.Dispose()));
            Assert.Equal((1, 0, 1), (game.Disposals, unloads, creations));
            Assert.Same(game, owner.Session);
            Assert.NotNull(context);
            Assert.Throws<IOException>(() => File.Move(manifest, retired));

            owner.Dispose();
            Assert.Equal((2, 1, 1), (game.Disposals, unloads, creations));
            Assert.Throws<ObjectDisposedException>(() => owner.Session);
            File.Move(manifest, retired);
            Assert.True(File.Exists(retired));
            Assert.False(File.Exists(manifest));
            File.Move(retired, manifest);
            owner.Dispose();
            Assert.Equal((2, 1), (game.Disposals, unloads));
        }
        finally
        {
            game.AllowCleanup = true;
            owner.Dispose();
            if (File.Exists(retired)) File.Move(retired, manifest);
        }
    }

    private sealed class ThrowOnceGame(Exception error) : IGameSession
    {
        internal int Disposals;
        internal bool AllowCleanup;
        public ulong TickNumber => 0;
        public long Revision => -1;
        public uint PatternColor => 0;
        public void Load(SceneSnapshot scene) { }
        public void Tick() { }
        public void Dispose()
        { if (++Disposals == 1 && !AllowCleanup) throw error; }
    }
}
