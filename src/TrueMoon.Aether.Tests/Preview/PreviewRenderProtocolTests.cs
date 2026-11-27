using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed partial class PreviewRenderProtocolTests
{
    private const string WireHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const int WireChunkBytes = 32 * 1024;
    private const int WireMaximumBytes = 12 * 1024 * 1024 + 16 * 1024;

    [Fact]
    public async Task ChunkedPreviewActivatesOnlyExactCompleteRenderPayload()
    {
        var view = PreviewViewCodecTests.View();
        var payload = CreateWirePayload(view);
        var bytes = payload.CopyChunk(0, payload.Length);
        var operation = Guid.NewGuid().ToString("D");
        Assert.Equal("TPR1"u8.ToArray(), bytes[..4]);
        Assert.Equal(view.Context.SceneId, new Guid(bytes.AsSpan(4, 16)));
        Assert.Equal(view.Context.SceneRevision, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(RenderContentHash.Compute(bytes), payload.Digest);
        Assert.True(bytes.Length > 2 * WireChunkBytes);

        var worlds = 0;
        using var game = new PreviewGameSession(new global::TrueMoon.Factory<World>(() => { worlds++; return new(); }));
        using var wire = new WireClient(game);
        await wire.SendAsync(CommandKind.Hello);
        var badIdentity = wire.Request(CommandKind.BeginPreviewRender, Begin);
        badIdentity.Nonce = new('D', 64);
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.Protocol.SendAsync(badIdentity));
        var badSequence = wire.Request(CommandKind.BeginPreviewRender, Begin);
        badSequence.Sequence++;
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.Protocol.SendAsync(badSequence));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.BeginPreviewRender, r => { Begin(r); r.SceneBytes = WireMaximumBytes + 1; }));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.BeginPreviewRender, r => { Begin(r); r.SceneBytes = 35; }));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.BeginPreviewRender, r => { Begin(r); r.SceneDigest = new('Z', 64); }));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.BeginPreviewRender, r => { Begin(r); r.Revision++; }));
        AssertPreActivation(await wire.SendAsync(CommandKind.BeginPreviewRender, Begin), 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wire.SendAsync(CommandKind.BeginPreviewRender, Begin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => wire.SendAsync(CommandKind.LoadScene, r => r.Entities = []));
        await Assert.ThrowsAsync<NotSupportedException>(() => wire.SendAsync(CommandKind.AppendSceneWorld, Stamp));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.AppendPreviewRender, r =>
        { Fill(r, 0, WireChunkBytes); r.SceneLoadId = Guid.NewGuid().ToString("D"); }));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, 1, WireChunkBytes)));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, 0, WireChunkBytes + 1)));
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, 0, WireChunkBytes - 1)));
        AssertPreActivation(await wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, 0, WireChunkBytes)), WireChunkBytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, 0, WireChunkBytes)));
        for (var offset = WireChunkBytes; offset < bytes.Length;)
        {
            var count = Math.Min(WireChunkBytes, bytes.Length - offset);
            var ack = await wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, offset, count));
            offset += count;
            AssertPreActivation(ack, offset);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => wire.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, bytes.Length, 0)));
        var active = await wire.SendAsync(CommandKind.ActivatePreviewRender, Stamp);
        Assert.Equal("Paused", active.Phase); Assert.Equal(0, active.Tick); Assert.Equal(view.Context.SceneRevision, active.Revision);
        Assert.Equal(operation, active.SceneLoadId); Assert.Equal(bytes.Length, active.SceneLoadOffset);
        Assert.Equal(payload.Digest.ToString(), active.PreviewRenderDigest);
        Assert.Equal(view.Stamp, PreviewViewCodec.ReadStamp(active.PreviewViewStamp!));
        AssertGameFlagsClear(active);
        Assert.True(game.IsRenderMode); Assert.Equal(1, worlds); Assert.Equal(0ul, game.TickNumber);
        Assert.Equal(view, game.View);
        Assert.Equal(payload.Rows[1].World, Assert.Single(game.PrimitiveOverlay).World);
        Assert.Equal(payload.Rows[1].Color, Assert.Single(game.PrimitiveOverlay).Color);
        Assert.False(wire.Protocol.TryCaptureFrame(out _));
        await Assert.ThrowsAsync<InvalidOperationException>(() => wire.SendAsync(CommandKind.ActivatePreviewRender, Stamp));
        await Assert.ThrowsAsync<InvalidOperationException>(() => wire.SendAsync(CommandKind.SetMode, r => r.Playing = true));
        await Assert.ThrowsAsync<NotSupportedException>(() => wire.SendAsync(CommandKind.PresentRenderView));
        Assert.Equal(active.PreviewRenderDigest, (await wire.SendAsync(CommandKind.GetStatus)).PreviewRenderDigest);

        foreach (var kind in new[] { "incomplete", "digest", "payload-scene", "bad-wire-bool", "abort", "stop" })
        {
            using var candidate = new PreviewGameSession(new global::TrueMoon.Factory<World>(() => { worlds++; return new(); }));
            using var fresh = new WireClient(candidate);
            await fresh.SendAsync(CommandKind.Hello);
            var incoming = bytes.ToArray();
            if (kind == "payload-scene") Guid.NewGuid().TryWriteBytes(incoming.AsSpan(4, 16));
            if (kind == "bad-wire-bool")
            {
                var contentSize = BinaryPrimitives.ReadInt32LittleEndian(incoming.AsSpan(28));
                incoming[36 + contentSize + 1 + 16 + 64 + 32] = 2;
            }
            await fresh.SendAsync(CommandKind.BeginPreviewRender, r =>
            {
                Begin(r);
                if (kind == "digest") r.SceneDigest = WireHash;
                else if (kind == "abort") r.SceneBytes = WireMaximumBytes;
                else r.SceneDigest = RenderContentHash.Compute(incoming).ToString();
            });
            if (kind is "digest" or "payload-scene" or "bad-wire-bool")
                for (var offset = 0; offset < incoming.Length; offset += WireChunkBytes)
                    await fresh.SendAsync(CommandKind.AppendPreviewRender, r =>
                    { Stamp(r); r.SceneOffset = offset; r.SceneChunk = incoming.AsSpan(offset, Math.Min(WireChunkBytes, incoming.Length - offset)).ToArray(); });
            if (kind == "stop") Assert.Equal("Stopping", (await fresh.SendAsync(CommandKind.Stop)).Phase);
            else if (kind == "abort") Assert.Equal("HelloAccepted", (await fresh.SendAsync(CommandKind.AbortPreviewRender, Stamp)).Phase);
            else await Assert.ThrowsAsync<InvalidDataException>(() => fresh.SendAsync(CommandKind.ActivatePreviewRender, Stamp));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.SendAsync(CommandKind.AppendPreviewRender, r => Fill(r, 0, WireChunkBytes)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.SendAsync(CommandKind.BeginPreviewRender, Begin));
            Assert.False(candidate.IsRenderMode); Assert.Null(candidate.View); Assert.Equal(0, candidate.Revision);
            Assert.Equal(1, worlds); // Bad requests never reach the World factory.
        }

        foreach (var (role, surface, hasWorld) in new[] { (RuntimeRole.Game, true, true), (RuntimeRole.EditPreview, false, true), (RuntimeRole.EditPreview, true, false) })
        {
            using var legacy = hasWorld ? new PreviewGameSession(new global::TrueMoon.Factory<World>(() => new())) : new PreviewGameSession();
            using var legacyWire = new WireClient(legacy, role, surface);
            var hello = await legacyWire.SendAsync(CommandKind.Hello);
            Assert.Equal(0, hello.PreviewRenderVersion);
            AssertGameFlagsClear(hello);
            await Assert.ThrowsAsync<NotSupportedException>(() => legacyWire.SendAsync(CommandKind.BeginPreviewRender, Begin));
            Assert.False(legacy.IsRenderMode);
        }

        using var primitive = new PreviewGameSession(new global::TrueMoon.Factory<World>(() => new()));
        using var usedWire = new WireClient(primitive);
        await usedWire.SendAsync(CommandKind.Hello);
        await usedWire.SendAsync(CommandKind.LoadScene, r => { r.SceneId = "primitive"; r.Revision = 1; r.Entities = []; });
        await Assert.ThrowsAsync<InvalidOperationException>(() => usedWire.SendAsync(CommandKind.BeginPreviewRender, Begin));
        Assert.Equal(1, primitive.Revision);

        void Begin(ControlRequest request)
        {
            Stamp(request); request.SceneId = view.Context.SceneId.ToString("D"); request.Revision = view.Context.SceneRevision;
            request.SceneBytes = bytes.Length; request.SceneDigest = payload.Digest.ToString(); request.PreviewView = PreviewViewCodec.Encode(view);
        }
        void Stamp(ControlRequest request) => request.SceneLoadId = operation;
        void Fill(ControlRequest request, int offset, int size)
        { Stamp(request); request.SceneOffset = offset; request.SceneChunk = bytes.AsSpan(offset, size).ToArray(); }
        void AssertPreActivation(ControlReply reply, int offset)
        {
            Assert.Equal(1, reply.PreviewRenderVersion); Assert.Equal(operation, reply.SceneLoadId); Assert.Equal(offset, reply.SceneLoadOffset);
            Assert.Empty(reply.PreviewRenderDigest); Assert.Null(reply.PreviewViewStamp); Assert.Equal("HelloAccepted", reply.Phase);
            AssertGameFlagsClear(reply);
            Assert.False(game.IsRenderMode); Assert.Equal(0, worlds);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task SupervisorRejectsUnknownPreviewVersionBeforeBeginningTransfer(int version)
    {
        var view = PreviewViewCodecTests.View(); var payload = CreateWirePayload(view);
        UnknownPreviewLease? lease = null;
        var factory = new global::TrueMoon.Factory<IRuntimeLease, SessionLaunch>(launch => lease = new(launch, version));
        await using var supervisor = new SessionSupervisor(factory, WireHash, WireHash, "preview-render-test");
        await Assert.ThrowsAsync<NotSupportedException>(() => supervisor.StartPreviewRenderViewAsync(payload, view, default));
        Assert.NotNull(lease); Assert.Equal(RuntimeRole.EditPreview, lease.Launch.Role);
        Assert.Null(lease.Launch.Build); Assert.False(lease.Launch.RequireRestore);
        Assert.Equal(BuildIdentity.PreviewId, lease.Launch.Identity.GameBuildId);
        Assert.Equal(CommandKind.Hello, Assert.Single(lease.Commands));
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.False(supervisor.Status.SceneLoaded);
        Assert.True(lease.ResourcesReleased);
    }

    internal static PreviewRenderPayload CreateWirePayload(PreviewViewState view)
    {
        var project = Guid.NewGuid();
        var pixels = new byte[128 * 128 * 4]; Array.Fill(pixels, (byte)255);
        var texture = new BaseColorTextureDefinition(128, 128, pixels); var textureKey = texture.CreateKey(project, Guid.NewGuid());
        var material = new OpaqueUnlitMaterialDefinition(Vector3.One, textureKey); var materialKey = material.CreateKey(project, Guid.NewGuid());
        var slot = Guid.NewGuid();
        var mesh = new MeshDefinition([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [Vector2.Zero, Vector2.UnitX, Vector2.UnitY],
            [0, 1, 2], [new(slot, 0, 3)], new(Vector3.Zero, new(1, 1, 0)));
        var meshKey = mesh.CreateKey(project, Guid.NewGuid());
        var binding = new MaterialBindingSetDefinition(meshKey, [new(slot, materialKey)]);
        var bindingKey = binding.CreateKey(project, Guid.NewGuid());
        var content = new GameBuildRenderContent(project, [new(textureKey, texture), new(materialKey, material), new(meshKey, mesh), new(bindingKey, binding)]);
        var shear = Matrix4x4.Identity; shear.M21 = .25f; shear.M42 = 2;
        return new(new(view.Context.SceneId), view.Context.SceneRevision, content,
        [new(new(Guid.NewGuid()), shear, meshKey.AssetId, bindingKey.AssetId, true, 17, true, new(.1f, .25f, .8f)),
         new(new(Guid.NewGuid()), Matrix4x4.CreateTranslation(1, 2, 3), default, default, false, 0, false, new(.8f, .25f, .1f))]);
    }

    private static void AssertGameFlagsClear(ControlReply reply)
    {
        Assert.Equal(0, reply.RenderViewVersion); Assert.False(reply.RenderViewReady); Assert.False(reply.RenderViewPresentationAccepted);
        Assert.Equal(0, reply.SceneWorldVersion); Assert.Empty(reply.SceneWorldId); Assert.Empty(reply.SceneRegistryId);
    }

    private sealed class WireClient : IDisposable
    {
        private readonly SessionIdentity _identity = new(Guid.NewGuid(), 1, WireHash, BuildIdentity.PreviewId, new('C', 64));
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private ulong _sequence = 1;
        internal RuntimeProtocol Protocol { get; }
        internal WireClient(PreviewGameSession game, RuntimeRole role = RuntimeRole.EditPreview, bool surface = true) =>
            Protocol = new(_identity, game, 311, role: role) { PreviewSurfaceAvailable = surface };
        internal ControlRequest Request(CommandKind kind, Action<ControlRequest>? configure = null)
        { var request = ControlRequest.Create(_identity, _sequence, kind); configure?.Invoke(request); return request; }
        internal async Task<ControlReply> SendAsync(CommandKind kind, Action<ControlRequest>? configure = null)
        {
            _buffer.Clear(); Request(kind, configure).Serialize(_buffer);
            Assert.True(_buffer.WrittenCount < 64 * 1024);
            var consumed = 0;
            var request = new ControlRequest().Deserialize(_buffer.WrittenSpan, ref consumed);
            Assert.Equal(_buffer.WrittenCount, consumed);
            var reply = await Protocol.SendAsync(request); _sequence++;
            _buffer.Clear(); reply.Serialize(_buffer); consumed = 0;
            var decoded = new ControlReply().Deserialize(_buffer.WrittenSpan, ref consumed);
            Assert.Equal(_buffer.WrittenCount, consumed);
            return decoded;
        }
        public void Dispose() => Protocol.Dispose();
    }

    private sealed class UnknownPreviewLease(SessionLaunch launch, int version) : IRuntimeLease
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal SessionLaunch Launch => launch;
        internal List<CommandKind> Commands { get; } = [];
        public int ProcessId => 345;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token) => Task.CompletedTask;
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        {
            Commands.Add((CommandKind)request.Kind);
            return Task.FromResult(new ControlReply { Stamp = request, ProcessId = ProcessId, Phase = "HelloAccepted", PreviewRenderVersion = version, PreviewViewVersion = 1 });
        }
        public Task StopAsync(CancellationToken gracefulDeadline = default) { _exit.TrySetResult(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
}
