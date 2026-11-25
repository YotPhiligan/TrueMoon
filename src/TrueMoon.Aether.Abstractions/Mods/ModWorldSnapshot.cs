using System.Collections.ObjectModel;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Mods;

/// <summary>Neutral effective scene/component values plus copied system state. No CLR component types or live resources.</summary>
public sealed class ModWorldSnapshot
{
    /// <summary>Supported envelope format.</summary>
    public const int CurrentFormatVersion = 1;
    /// <summary>Creates a detached immutable snapshot. Scene inheritance is resolved by the runtime before use.</summary>
    /// <param name="scene">Persistent entity/component/schema identities and neutral values.</param><param name="systems">Exact stable system IDs and states.</param>
    /// <param name="tickNumber">Last completed simulation tick.</param><param name="formatVersion">Envelope generation.</param>
    public ModWorldSnapshot(SceneDocument scene, IEnumerable<KeyValuePair<string, ModSystemState>>? systems = null, ulong tickNumber = 0, int formatVersion = CurrentFormatVersion)
    {
        ArgumentNullException.ThrowIfNull(scene); Scene = scene; TickNumber = tickNumber; FormatVersion = formatVersion;
        var states = new Dictionary<string, ModSystemState>(StringComparer.Ordinal); long bytes = 0;
        foreach (var pair in systems ?? [])
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 128 || pair.Value == null || states.Count == 128 || !states.TryAdd(pair.Key, new(pair.Value.Version, pair.Value.Bytes.Span)))
                throw new ArgumentException("Invalid/duplicate/excessive system state.", nameof(systems));
            bytes += pair.Value.Bytes.Length; if (bytes > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(systems));
        }
        Systems = new ReadOnlyDictionary<string, ModSystemState>(states);
    }
    /// <summary>Envelope format version.</summary>
    public int FormatVersion { get; }
    /// <summary>Neutral scene data, never a World or component instance.</summary>
    public SceneDocument Scene { get; }
    /// <summary>Copied system states keyed by exact system ID.</summary>
    public IReadOnlyDictionary<string, ModSystemState> Systems { get; }
    /// <summary>Completed tick sequence.</summary>
    public ulong TickNumber { get; }
}
