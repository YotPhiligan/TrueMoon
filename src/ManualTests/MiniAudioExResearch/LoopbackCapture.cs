using MiniAudioEx.Native;

namespace MiniAudioExResearch;

// Capture this process only. Never fall back to recording unrelated system audio or a microphone.
internal sealed unsafe class LoopbackCapture : IDisposable
{
    private readonly NativeOwner device = new(ma_allocation_type.device);
    private readonly float[] pcm = new float[20 * 48000 * 2];
    private readonly ma_device_data_proc callback;
    private int samples, failures;
    private bool disposed;
    internal int Samples => Volatile.Read(ref samples);

    internal LoopbackCapture(ma_context_ptr context)
    {
        callback = Process;
        try
        {
            var config = MiniAudioNative.ma_device_config_init(ma_device_type.loopback);
            config.capture.format = ma_format.f32;
            config.capture.channels = 2;
            config.sampleRate = 48000;
            config.wasapi.loopbackProcessID = (uint)Environment.ProcessId;
            config.wasapi.noAutoConvertSRC = 1;
            config.wasapi.noDefaultQualitySRC = 1;
            config.wasapi.noHardwareOffloading = 1;
            config.SetDataCallback(callback);
            AudioGraph.Check(MiniAudioNative.ma_device_init(context, ref config, new(device.Pointer)), "process-only loopback init");
            device.Initialized();
            AudioGraph.Check(MiniAudioNative.ma_device_start(new(device.Pointer)), "loopback start");
        }
        catch { device.Dispose(); throw; }
    }

    private void Process(ma_device_ptr unused, nint output, nint input, uint frames)
    {
        try
        {
            int count = checked((int)frames * 2), at = samples;
            if (input == 0 || at + count > pcm.Length) return;
            new ReadOnlySpan<float>((void*)input, count).CopyTo(pcm.AsSpan(at));
            Volatile.Write(ref samples, at + count);
        }
        catch { Interlocked.Increment(ref failures); }
    }

    internal float[] StopAndCopy()
    {
        AudioGraph.Check(MiniAudioNative.ma_device_stop(new(device.Pointer)), "loopback stop/drain");
        if (failures != 0) throw new InvalidOperationException("Loopback callback failure.");
        return pcm.AsSpan(0, Samples).ToArray();
    }

    public void Dispose()
    {
        if (disposed) return;
        device.Dispose();
        GC.KeepAlive(callback);
        disposed = true;
    }
}
