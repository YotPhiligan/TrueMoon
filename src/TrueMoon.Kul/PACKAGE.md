# TrueMoon.Kul

Windows x64 sound playback and music streaming using the patched
`TrueMoon.MiniAudioEx 4.0.1-truemoon.1` dependency. Requires Windows and the MSVC x64
runtime used to build the native package. Do not load the original JAJ package in
the same process. This package depends only on TrueMoon.Contracts and the audio
binding; Aether, rendering and UI are not required.

```csharp
using TrueMoon;
using TrueMoon.Kul;

// In the application composition root:
await using var app = App.Build(context => context.UseAudio(options =>
{
    options.Volume = .4f;
}));
await app.StartAsync();
// Inject IAudioService and IFactory<AudioSession> into your consumer.
// Use Resolve only at the composition root if retrieving them directly.
```

Consumer example, with a factory supplied by DI:

```csharp
public sealed class GameAudio(IFactory<AudioSession> sessions) : IDisposable
{
    private AudioSession? session;
    private AudioClip? effect;
    private AudioPlayback? music;

    public void Load()
    {
        session = sessions.Create()!;
        effect = session.LoadSound("Assets/effect.wav");
        music = session.PlayMusic("Assets/music.ogg", new() { Loop = true, Volume = .3f });
    }
    public AudioPlayback PlayEffect() => session!.PlaySound(effect!);
    public void Dispose() => session?.Dispose();
}
```

Register consumers through App Services. Returned voices are caller-owned and are
also tracked by their session: dispose early, or call `CollectFinished` for ended
voices. Admission also collects ended voices. Pause/Stop retain resources; Dispose
releases them. Dispose the session when leaving a game/preview; App shutdown drains
forgotten sessions. Disposing a clip prevents new plays while existing voices keep
its PCM alive. Session products are not DI-owned aliases.

File preparation is synchronous control-thread work. Prepare sounds outside frame
loops. Music uses native resource-manager workers and keeps its file open until
voice disposal. Stream seek queues worker refill and may briefly render silence.
Input files must remain stable throughout their lifetime. No I/O,
resolution, managed callback or user delegate is installed on the audio thread.
All public control operations serialize per App; do not call them from native callbacks.

`AudioMode.Offline` opens no device and requires `IAudioService.Render` with an
even-length stereo float32 span. Device mode opens WASAPI on App startup and fails
explicitly on error. `MuteOutput=true` mutes only this output stream. Configure
before App startup; stopped Apps cannot be restarted. Stop and Dispose are
synchronous, non-cancellable native drain operations; native driver stalls cannot
be forcibly interrupted. A failed drain retains the graph for disposal retry.

Options bound sessions, clips, voices, streams, encoded sound file size and retained
decoded PCM. PCM admission is checked after decoding; it is not a bound on temporary
decoder allocation peaks. Stream pages/workers use miniaudio defaults. This version
does not provide command queues, spatial audio, ECS/asset adapters or device-loss
recovery. OGG duration can be unavailable (`LengthFrames=null`); loop cursors can
remain monotonic. AOT, other OS/RIDs and long-running performance acceptance are separate.

`UseConfiguredAudio` binds the exact `AudioConfigurationSchema.Default` registered
in the application's common configuration profile. Keys are under `Audio:`;
explicit callback overrides are applied through the shared configuration stage.

Source checkout build instructions: `docs/audio/README.md` and
`eng/native/MiniAudioEx/README.md`. The private fork must be built and supplied by
a package feed before restoring this module; it is not published to nuget.org.
