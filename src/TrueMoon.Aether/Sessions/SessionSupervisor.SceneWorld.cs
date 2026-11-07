using System.Security.Cryptography;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Transport;

namespace TrueMoon.Aether.Sessions;

public sealed partial class SessionSupervisor
{
    private string? _activeWorldId, _activeRegistryId, _activeSceneOperation;
    private int _activeSceneBytes, _activeEntityCount, _activeComponentCount;

    /// <summary>Starts a fresh fixed-build runtime with a complete scene, using negotiated bounded chunks.</summary>
    /// <param name="scene">Complete authoring document.</param><param name="behavior">Explicit probe behavior.</param>
    /// <param name="token">Startup cancellation, including transfer and activation.</param><returns>Completion after Paused readiness.</returns>
    public Task StartWorldAsync(SceneWorldData scene, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(scene); return StartCoreAsync(null, null, null, behavior, token, worldScene: scene); }

    /// <summary>Starts a selected game artifact without loading its CLR types in the parent.</summary>
    /// <param name="scene">Complete neutral source.</param><param name="build">Validated selected artifact.</param>
    /// <param name="behavior">Explicit probe behavior.</param><param name="token">Startup cancellation.</param><returns>Completion after complete initial activation.</returns>
    public Task StartWorldAsync(SceneWorldData scene, GameBuildArtifact build, string behavior = "normal", CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(build);
        return StartCoreAsync(null, build, null, behavior, token, worldScene: scene);
    }

    private async Task<ControlReply> TransferWorldAsync(SceneWorldData data, byte[] bytes, int expectedComponents, ControlReply hello, CancellationToken token)
    {
        if (hello.SceneWorldVersion != SceneWorldStaging.Version)
            throw new NotSupportedException("Runtime does not support the required full scene World version.");
        var registry = SceneWorldStaging.ParseId(hello.SceneRegistryId).ToString("D");
        ValidateCapability(hello, registry);
        if (hello.SceneLoadId != "" || hello.SceneLoadOffset != 0 || hello.SceneWorldId != "" || hello.SceneEntityCount != 0 || hello.SceneComponentCount != 0)
            throw new InvalidDataException("Hello returned a previously used scene runtime.");
        var operation = Guid.NewGuid().ToString("D");
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        var begin = await ExchangeAsync(CommandKind.BeginSceneWorld, request =>
        {
            Stamp(request); request.SceneId = data.Scene.Id.Value.ToString("D"); request.Revision = data.Scene.Revision;
            request.SceneBytes = bytes.Length; request.SceneDigest = digest;
        }, token).ConfigureAwait(false);
        ValidateProgress(begin, 0, false);
        var offset = 0;
        while (offset < bytes.Length)
        {
            token.ThrowIfCancellationRequested();
            var size = Math.Min(SceneWorldStaging.ChunkBytes, bytes.Length - offset);
            var chunk = bytes.AsSpan(offset, size).ToArray();
            var reply = await ExchangeAsync(CommandKind.AppendSceneWorld, request =>
            { Stamp(request); request.SceneOffset = offset; request.SceneChunk = chunk; }, token).ConfigureAwait(false);
            offset += size; ValidateProgress(reply, offset, false);
        }
        var activated = await ExchangeAsync(CommandKind.ActivateSceneWorld, Stamp, token).ConfigureAwait(false);
        ValidateProgress(activated, bytes.Length, true);
        _activeWorldId = activated.SceneWorldId; _activeRegistryId = registry; _activeSceneOperation = operation;
        _activeSceneBytes = bytes.Length; _activeEntityCount = activated.SceneEntityCount; _activeComponentCount = activated.SceneComponentCount;
        return activated;

        void Stamp(ControlRequest request) { request.SceneLoadId = operation; request.SceneRegistryId = registry; }
        void ValidateProgress(ControlReply reply, int expectedOffset, bool active)
        {
            ValidateCapability(reply, registry);
            if (reply.SceneLoadId != operation || reply.SceneLoadOffset != expectedOffset || reply.Tick != 0 ||
                reply.Phase != (active ? "Paused" : "HelloAccepted") || reply.Revision != (active ? data.Scene.Revision : -1))
                throw new InvalidDataException("Scene transfer acknowledgement mismatch.");
            if (active)
            {
                SceneWorldStaging.ParseId(reply.SceneWorldId);
                if (reply.SceneEntityCount != data.Scene.Entities.Length || reply.SceneComponentCount != expectedComponents)
                    throw new InvalidDataException("Scene activation cardinality mismatch.");
            }
            else if (reply.SceneWorldId != "" || reply.SceneEntityCount != 0 || reply.SceneComponentCount != 0)
                throw new InvalidDataException("Runtime exposed a partial active World during transfer.");
        }
    }
    private static void ValidateCapability(ControlReply reply, string registry)
    {
        if (reply.SceneWorldVersion != SceneWorldStaging.Version || reply.SceneRegistryId != registry ||
            reply.SceneMaximumBytes != SceneWorldCodec.MaximumBytes || reply.SceneChunkBytes != SceneWorldStaging.ChunkBytes)
            throw new InvalidDataException("Scene transfer capability or registry generation changed.");
    }
    private void ValidateActiveWorld(ControlReply reply)
    {
        if (_activeWorldId == null) return;
        ValidateCapability(reply, _activeRegistryId!);
        if (reply.SceneWorldId != _activeWorldId || reply.SceneLoadId != _activeSceneOperation || reply.SceneLoadOffset != _activeSceneBytes ||
            reply.SceneEntityCount != _activeEntityCount || reply.SceneComponentCount != _activeComponentCount)
            throw new InvalidDataException("Active scene World acknowledgement changed identity or source cardinality.");
    }
}
