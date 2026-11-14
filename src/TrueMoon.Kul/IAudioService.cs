namespace TrueMoon.Kul;

/// <summary>Borrowed App audio service. Create caller-owned sessions through IFactory&lt;AudioSession&gt;.</summary>
public interface IAudioService
{
    /// <summary>Linear master gain in the range zero to one.</summary>
    float Volume { get; set; }
    /// <summary>Observes ownership without advancing playback or collecting voices.</summary>
    /// <returns>An immutable snapshot.</returns>
    AudioSnapshot GetSnapshot();
    /// <summary>Renders interleaved stereo float32 PCM in explicit Offline mode.</summary>
    /// <param name="stereoSamples">Nonempty even-length output; never retains this span.</param>
    void Render(Span<float> stereoSamples);
}
