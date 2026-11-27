using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Checkpoints;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Scenes;
using SceneDocument = TrueMoon.Aether.Scenes.SceneDocument;
using SceneSnapshot = TrueMoon.Aether.Scenes.SceneSnapshot;

namespace TrueMoon.Aether.Runtime;

/// <summary>Immutable selected deployment and captured scene. It contains no App, game instance, source project or live file reader.</summary>
public sealed class PlayerStart
{
    private PlayerStart(GameBuildArtifact artifact, SceneSnapshot? snapshot, SceneWorldData? world, string? sceneHash, GameCheckpoint? checkpoint = null)
    { Artifact = artifact; Snapshot = snapshot; World = world; SceneHash = sceneHash; Checkpoint = checkpoint; }
    /// <summary>Validated immutable selected build; Start revalidates it before loading code.</summary>
    public GameBuildArtifact Artifact { get; }
    /// <summary>Legacy bounded game input, exclusive with World.</summary>
    public SceneSnapshot? Snapshot { get; }
    /// <summary>Complete immutable scene input, exclusive with Snapshot.</summary>
    public SceneWorldData? World { get; }
    /// <summary>Exact captured scene bytes fingerprint; null for explicitly constructed data.</summary>
    public string? SceneHash { get; }
    /// <summary>Optional checkpoint to apply before local Player readiness.</summary>
    public GameCheckpoint? Checkpoint { get; }

    /// <summary>Returns a new start that will restore the exact selected Build and scene.</summary>
    /// <param name="checkpoint">Bounded state from the same host, Build, scene ID and revision.</param>
    /// <returns>A new start with the same frozen deployment and scene.</returns>
    public PlayerStart WithCheckpoint(GameCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (Checkpoint != null) throw new InvalidOperationException("A Player start already carries a checkpoint.");
        if (!string.Equals(checkpoint.SourceGameBuildId, Artifact.BuildId, StringComparison.Ordinal))
            throw new InvalidDataException("Checkpoint was captured from a different Player Build.");
        if (World is { } world)
            checkpoint.ValidateFor(Artifact.HostContractId, world.Scene.Id.Value.ToString("D"), world.Scene.Revision);
        else
            checkpoint.ValidateFor(Artifact.HostContractId, Snapshot!);
        GameCheckpointCodec.SceneByteCount(checkpoint.SceneId);
        return new(Artifact, Snapshot, World, SceneHash, checkpoint);
    }
    /// <summary>Captures immutable legacy data.</summary><param name="artifact">Selected deployment.</param><param name="scene">Frozen scene.</param><returns>Start data.</returns>
    public static PlayerStart FromSnapshot(GameBuildArtifact artifact, SceneSnapshot scene)
    { ArgumentNullException.ThrowIfNull(artifact); ArgumentNullException.ThrowIfNull(scene); return new(artifact, new(scene.Id, scene.Revision, scene.CopyEntities()), null, null); }
    /// <summary>Captures complete immutable data.</summary><param name="artifact">Selected deployment.</param><param name="scene">Frozen scene.</param><returns>Start data.</returns>
    public static PlayerStart FromWorld(GameBuildArtifact artifact, SceneWorldData scene)
    { ArgumentNullException.ThrowIfNull(artifact); ArgumentNullException.ThrowIfNull(scene); if (scene.FormatVersion != SceneWorldData.CurrentFormatVersion) throw new InvalidDataException("Unknown scene World version."); return new(artifact, null, scene, null); }
    /// <summary>Pre-App bootstrap adapter: validates selected artifacts and captures a bounded scene, never builds or loads game code.</summary>
    /// <param name="artifactDirectory">Delivered content-addressed build.</param><param name="hostId">Exact standalone Player fingerprint.</param>
    /// <param name="scenePath">Explicit scene file.</param><param name="legacySnapshot">Explicit legacy JSON rather than full .aetherscene JSON.</param>
    /// <param name="token">Read cancellation.</param><returns>Frozen inputs independent of subsequent scene-file changes.</returns>
    public static async Task<PlayerStart> CaptureAsync(string artifactDirectory, string hostId, string scenePath, bool legacySnapshot = false, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); var artifact = GameBuildArtifact.Open(artifactDirectory, hostId);
        await using var stream = new FileStream(Path.GetFullPath(scenePath), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > SceneDocumentCodec.MaximumBytes) throw new InvalidDataException("Invalid Player scene size.");
        var bytes = new byte[checked((int)stream.Length)]; await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (legacySnapshot)
        {
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Expected a legacy scene object.");
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in json.RootElement.EnumerateObject())
                if(!names.Add(property.Name))throw new InvalidDataException("Duplicate legacy scene property.");
            var value = JsonSerializer.Deserialize<LegacyScene>(bytes, LegacyJson) ?? throw new InvalidDataException("Missing legacy scene.");
            if (value.FormatVersion != 1 || value.Entities == null) throw new InvalidDataException("Unsupported legacy Player scene.");
            return new(artifact, new(value.Id, value.Revision, value.Entities), null, hash);
        }
        // CPU-only bootstrap composition. Products are temporary validators, not App services or live authoring state.
        var resolution = new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(scene => new(scene.Definitions, scene.Schemas)));
        var codec = new SceneDocumentCodec(resolution);
        var document = codec.Decode(bytes, token); return new(artifact, null, new(document), hash);
    }
    private static readonly JsonSerializerOptions LegacyJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
    private sealed record LegacyScene([property:JsonRequired]int FormatVersion, [property:JsonRequired]string Id,
        [property:JsonRequired]long Revision, [property:JsonRequired]SceneEntity[] Entities);
}
