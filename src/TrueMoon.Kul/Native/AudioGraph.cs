using MiniAudioEx.Native;

namespace TrueMoon.Kul.Native;

internal sealed unsafe class AudioGraph : IDisposable
{
    private DecoderTableHandle? tables;
    private NativeHandle? manager, engine, context;
    private readonly AudioOptions settings;
    internal ma_engine_ptr Engine => new(engine!.Pointer);
    internal uint PeriodFrames { get; }
    internal uint Periods { get; }

    internal AudioGraph(AudioOptions settings, Action<string>? checkpoint = null)
    {
        this.settings = settings;
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
            throw new PlatformNotSupportedException("TrueMoon.Audio currently supports Windows x64.");
        try
        {
            tables = new();
            manager = new(ma_allocation_type.resource_manager, tables);
            var resource = MiniAudioNative.ma_resource_manager_config_init();
            resource.decodedFormat = ma_format.f32;
            resource.decodedSampleRate = settings.SampleRate;
            resource.decodedChannels = 2;
            resource.jobThreadCount = 2;
            resource.ppCustomDecodingBackendVTables = tables.Pointer;
            resource.customDecodingBackendCount = 1;
            Check(MiniAudioNative.ma_resource_manager_init(ref resource, new(manager.Pointer)), "Initialize audio resources");
            manager.Initialized();
            checkpoint?.Invoke("resources");
            if (settings.Mode == AudioMode.Device)
            {
                context = new(ma_allocation_type.context);
                var config = MiniAudioNative.ma_context_config_init();
                Check(MiniAudioNative.ma_context_init([ma_backend.wasapi], ref config, new(context.Pointer)), "Initialize WASAPI");
                context.Initialized();
            }
            engine = context is null ? new(ma_allocation_type.engine, manager) : new(ma_allocation_type.engine, manager, context);
            var options = MiniAudioNative.ma_engine_config_init();
            options.pResourceManager = new(manager.Pointer);
            options.channels = 2;
            options.sampleRate = settings.SampleRate;
            options.periodSizeInFrames = settings.PeriodFrames;
            options.noAutoStart = 1;
            options.noDevice = settings.Mode == AudioMode.Offline ? 1u : 0u;
            if (context is not null) options.pContext = new(context.Pointer);
            Check(MiniAudioNative.ma_engine_init(ref options, Engine), "Initialize audio mixer");
            engine.Initialized();
            Check(MiniAudioNative.ma_engine_set_volume(Engine, settings.Volume), "Set master gain");
            if (settings.Mode == AudioMode.Device)
            {
                var device = MiniAudioNative.ma_engine_get_device(Engine);
                Check(MiniAudioNative.ma_device_set_master_volume(device, settings.MuteOutput ? 0 : 1), "Set output stream gain");
                var actual = MiniAudioNative.ma_device_get_playback(device).Get();
                PeriodFrames = actual->internalPeriodSizeInFrames;
                Periods = actual->internalPeriods;
            }
            checkpoint?.Invoke("engine");
            if (settings.Mode == AudioMode.Device) Check(MiniAudioNative.ma_engine_start(Engine), "Start playback device");
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("Audio startup and rollback failed.", error, cleanup); }
            throw;
        }
    }

    internal NativeHandle Load(string path, bool streaming)
    {
        var sound = new NativeHandle(ma_allocation_type.sound, engine!);
        try
        {
            var flags = ma_sound_flags.wait_init | ma_sound_flags.no_pitch | ma_sound_flags.no_spatialization |
                        (streaming ? ma_sound_flags.stream : ma_sound_flags.decode);
            Check(MiniAudioNative.ma_sound_init_from_file_w(Engine, path, flags, default, default, new(sound.Pointer)), "Prepare audio file");
            sound.Initialized();
            return sound;
        }
        catch { sound.Dispose(); throw; }
    }
    internal NativeHandle Copy(NativeHandle clip)
    {
        var sound = new NativeHandle(ma_allocation_type.sound, engine!);
        try
        {
            Check(MiniAudioNative.ma_sound_init_copy(Engine, new(clip.Pointer), ma_sound_flags.no_pitch | ma_sound_flags.no_spatialization, default, new(sound.Pointer)), "Create sound voice");
            sound.Initialized();
            return sound;
        }
        catch { sound.Dispose(); throw; }
    }
    internal void Drain()
    {
        if (engine is { IsClosed: false, IsInitialized: true } && settings.Mode == AudioMode.Device)
            Check(MiniAudioNative.ma_engine_stop(Engine), "Stop and drain playback device");
    }
    public void Dispose()
    {
        Drain(); // Keep dependencies alive and retryable if native drain fails.
        engine?.Dispose();
        manager?.Dispose(); // Joins native decode workers before tables/context can disappear.
        tables?.Dispose();
        context?.Dispose();
    }
    internal static void Check(ma_result result, string operation)
    {
        if (result != ma_result.success) throw new AudioException(operation, (int)result);
    }
}
