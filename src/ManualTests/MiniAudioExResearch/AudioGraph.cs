using System.Runtime.InteropServices;
using MiniAudioEx.Native;

namespace MiniAudioExResearch;

// Low-level research owner, intentionally independent of TrueMoon App, ECS and production DI.
internal sealed unsafe class AudioGraph : IDisposable
{
    internal static bool RawVorbisByDefault { get; set; }
    private readonly List<NativeOwner> sounds = [];
    private NativeOwner? manager, engine, context, device;
    private nint decoderTables;
    private FileVorbisBackend? vorbis;
    private bool disposed;
    private readonly NativeAllocationTracker allocations;
    private readonly PlaybackTelemetry? telemetry;
    internal ma_engine_ptr Engine => new(engine!.Pointer);
    internal ma_context_ptr Context => new(context!.Pointer);
    internal PlaybackTelemetry? Telemetry => telemetry;
    internal object? DeviceInfo { get; private set; }

    internal AudioGraph(NativeAllocationTracker allocations, bool physical = false, bool? rawVorbis = null)
    {
        this.allocations = allocations;
        try
        {
            manager = new(ma_allocation_type.resource_manager);
            var resourceConfig = MiniAudioNative.ma_resource_manager_config_init();
            resourceConfig.allocationCallbacks = allocations.Callbacks;
            resourceConfig.decodedFormat = ma_format.f32;
            resourceConfig.decodedSampleRate = 48000;
            resourceConfig.jobThreadCount = 2;
            // Keep the table array, not just the vtable objects, alive until resource-manager uninit.
            decoderTables = (nint)NativeMemory.Alloc((nuint)IntPtr.Size);
            if (rawVorbis ?? RawVorbisByDefault) *(nint*)decoderTables = (nint)MiniAudioNative.ma_libvorbis_get_decoding_backend();
            else { vorbis = new(); *(nint*)decoderTables = vorbis.Table; resourceConfig.pCustomDecodingBackendUserData = vorbis.UserData; }
            if (*(nint*)decoderTables == 0) throw new InvalidOperationException("libvorbis backend absent.");
            resourceConfig.ppCustomDecodingBackendVTables = decoderTables;
            resourceConfig.customDecodingBackendCount = 1;
            Check(MiniAudioNative.ma_resource_manager_init(ref resourceConfig, new(manager.Pointer)), "resource manager init");
            manager.Initialized();

            engine = new(ma_allocation_type.engine);
            var engineConfig = MiniAudioNative.ma_engine_config_init();
            engineConfig.pResourceManager = new(manager.Pointer);
            engineConfig.channels = 2;
            engineConfig.sampleRate = 48000;
            engineConfig.noDevice = 1;
            engineConfig.allocationCallbacks = allocations.Callbacks;
            Check(MiniAudioNative.ma_engine_init(ref engineConfig, Engine), "engine init");
            engine.Initialized();

            if (physical)
            {
                context = new(ma_allocation_type.context);
                var contextConfig = MiniAudioNative.ma_context_config_init();
                contextConfig.allocationCallbacks = allocations.Callbacks;
                Check(MiniAudioNative.ma_context_init([ma_backend.wasapi], ref contextConfig, Context), "WASAPI context init");
                context.Initialized();
                telemetry = new(Engine, 20);
                device = new(ma_allocation_type.device);
                var config = MiniAudioNative.ma_device_config_init(ma_device_type.playback);
                config.playback.format = ma_format.f32;
                config.playback.channels = 2;
                config.sampleRate = 48000;
                config.periodSizeInFrames = 256;
                config.SetDataCallback(telemetry.Callback);
                Check(MiniAudioNative.ma_device_init(Context, ref config, new(device.Pointer)), "WASAPI playback init");
                device.Initialized();
                var playback = MiniAudioNative.ma_device_get_playback(new(device.Pointer)).Get();
                DeviceInfo = new
                {
                    Backend = "WASAPI", RequestedFrames = 256,
                    ActualPeriodFrames = playback->internalPeriodSizeInFrames,
                    ActualPeriods = playback->internalPeriods,
                    ActualSampleRate = playback->internalSampleRate,
                    Name = Marshal.PtrToStringUTF8((nint)playback->name)
                };
            }
        }
        catch { Dispose(); throw; }
    }

    internal ma_sound_ptr Load(string path, bool streaming, bool loop = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var owner = new NativeOwner(ma_allocation_type.sound);
        try
        {
            var sound = new ma_sound_ptr(owner.Pointer);
            var flags = ma_sound_flags.no_spatialization | ma_sound_flags.no_pitch | ma_sound_flags.wait_init |
                        (streaming ? ma_sound_flags.stream : ma_sound_flags.decode);
            // _w is explicit UTF-16 on Windows; avoids the binding's default ANSI char* conversion.
            Check(MiniAudioNative.ma_sound_init_from_file_w(Engine, path, flags, default, default, sound), "sound init: " + Path.GetFileName(path));
            owner.Initialized();
            MiniAudioNative.ma_sound_set_looping(sound, loop ? 1u : 0u);
            sounds.Add(owner);
            return sound;
        }
        catch { owner.Dispose(); throw; }
    }

    internal void Release(ma_sound_ptr sound)
    {
        var owner = sounds.Find(x => x.Pointer == sound.pointer) ?? throw new InvalidOperationException("Unknown sound owner.");
        Check(MiniAudioNative.ma_sound_stop(sound), "sound stop");
        owner.Dispose();
        sounds.Remove(owner);
    }

    internal float[] Render(int frames)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        float[] pcm = new float[checked(frames * 2)];
        fixed (float* output = pcm)
            Check(MiniAudioNative.ma_engine_read_pcm_frames(Engine, (nint)output, (ulong)frames), "offline mix");
        return pcm;
    }

    internal void StartDevice(bool audible = true)
    {
        Check(MiniAudioNative.ma_engine_set_volume(Engine, 0.22f), "master gain");
        Check(MiniAudioNative.ma_device_set_master_volume(new(device!.Pointer), audible ? 1 : 0), "own output-stream gain");
        Check(MiniAudioNative.ma_device_start(new(device!.Pointer)), "device start");
    }

    internal void StopDevice()
    {
        if (device is null || device.IsClosed) return;
        Check(MiniAudioNative.ma_device_stop(new(device.Pointer)), "device stop/drain");
    }

    public void Dispose()
    {
        if (disposed) return;
        // Stop/drain must succeed before buffers or engine can be released.
        if (device is not null && !device.IsClosed)
        {
            var pointer = new ma_device_ptr(device.Pointer);
            if (device.HasInitialized && MiniAudioNative.ma_device_is_started(pointer) != 0)
                Check(MiniAudioNative.ma_device_stop(pointer), "dispose device drain");
            device.Dispose();
        }
        for (int i = sounds.Count - 1; i >= 0; i--) sounds[i].Dispose();
        sounds.Clear();
        engine?.Dispose();
        manager?.Dispose(); // joins resource-manager workers before decoder table or allocator can die.
        vorbis?.Dispose();
        context?.Dispose();
        if (decoderTables != 0) { NativeMemory.Free((void*)decoderTables); decoderTables = 0; }
        GC.KeepAlive(telemetry);
        GC.KeepAlive(allocations);
        disposed = true;
    }

    internal static void Check(ma_result result, string operation)
    {
        if (result != ma_result.success) throw new InvalidOperationException($"{operation}: {result} ({(int)result})");
    }
}
