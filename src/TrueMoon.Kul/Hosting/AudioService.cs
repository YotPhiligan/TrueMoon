namespace TrueMoon.Kul.Hosting;

internal sealed class AudioService(AudioHost host) : IAudioService
{
    public float Volume { get => host.Volume; set => host.Volume = value; }
    public AudioSnapshot GetSnapshot() => host.Snapshot();
    public void Render(Span<float> stereoSamples) => host.Render(stereoSamples);
}
