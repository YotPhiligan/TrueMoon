namespace TrueMoon.Aether.Sessions;

/// <summary>Optional notification of actual runtime playback, including presentation-induced pauses.</summary>
/// <remarks>Sessions start paused. The host serializes notifications with ticks. Preparation must not start audible
/// playback. A paused Step may still execute one tick. Stop/drain and disposal ownership remain with the session owner.</remarks>
public interface IGamePlaybackSession
{
    /// <summary>Changes playback without advancing simulation time. Repeated values are harmless.</summary>
    /// <param name="playing">Whether continuous gameplay is actually permitted.</param>
    void SetPlaying(bool playing);
}
