namespace TrueMoon.Aether.Kul;

/// <summary>Nonspatial source. Gain/loop may change; AutoPlay is binding-time policy, subsequent playback uses commands.</summary>
/// <param name="Asset">Captured identity.</param><param name="Volume">Finite linear gain,0..1.</param>
/// <param name="Loop">Loop policy.</param><param name="AutoPlay">Desired Playing on first attachment/rebinding, still gated by actual session mode.</param>
public readonly record struct AudioSource(AudioAssetId Asset, float Volume = 1, bool Loop = false, bool AutoPlay = false);
