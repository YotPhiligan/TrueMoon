namespace TrueMoon.Aether.Kul;

/// <summary>One immutable captured file and its fixed voice reservation.</summary>
/// <param name="Id">Stable identity.</param><param name="FileName">Flat portable WAV/OGG file name.</param>
/// <param name="Sha256">Encoded file SHA256.</param><param name="Length">Positive encoded byte length.</param>
/// <param name="Kind">Clip or stream preparation.</param><param name="Voices">Fixed per-scene capacity,1..16.</param>
public sealed record AudioContentAsset(AudioAssetId Id, string FileName, string Sha256, long Length, AudioAssetKind Kind, int Voices = 1);
