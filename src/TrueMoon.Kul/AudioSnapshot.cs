namespace TrueMoon.Kul;

/// <summary>App-local ownership and negotiated output observations; no native handles escape.</summary>
/// <param name="IsRunning">Whether playback admission is open.</param>
/// <param name="Mode">Chosen output mode.</param><param name="SampleRate">Configured mix rate.</param>
/// <param name="Sessions">Active sessions.</param><param name="Clips">Loaded clips, including clips retained by voices.</param>
/// <param name="Voices">Active playback owners.</param><param name="MusicStreams">Streaming playback owners.</param>
/// <param name="ResidentSoundBytes">Retained decoded PCM, counted once per clip.</param>
/// <param name="DevicePeriodFrames">Negotiated device period, zero in offline mode.</param>
/// <param name="DevicePeriods">Negotiated period count, zero in offline mode.</param>
public sealed record AudioSnapshot(bool IsRunning, AudioMode Mode, uint SampleRate, int Sessions, int Clips,
    int Voices, int MusicStreams, long ResidentSoundBytes, uint DevicePeriodFrames, uint DevicePeriods);
