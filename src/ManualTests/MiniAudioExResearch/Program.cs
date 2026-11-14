using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MiniAudioEx.Native;

namespace MiniAudioExResearch;

internal static class Program
{
    private static readonly List<object> checks = [];
    private static readonly Dictionary<string, object?> observations = [];
    private static string fixtures = "";

    private static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("mode fixtures-directory report-path"); return 2; }
        string mode = args[0]; fixtures = Path.GetFullPath(args[1]);
        AudioGraph.RawVorbisByDefault = args.Length > 3 && args[3] == "raw-vorbis";
        observations["RawVorbisBackend"] = AudioGraph.RawVorbisByDefault;
        string reportPath = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        bool completed = false;
        string? failure = null;
        try
        {
            Require(OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64, "Windows x64 profile");
            switch (mode)
            {
                case "fixtures": Fixtures.Generate(fixtures); break;
                case "inspect": Inspect(); break;
                case "offline": Offline(); break;
                case "cleanup": CleanupProbe(); break;
                case "raw-vorbis-defect": RawVorbisDefect(); break;
                case "malformed-vorbis-defect": MalformedVorbisDefect(); break;
                case "failure-stress": FailureStress(); break;
                case "device": Device(reportPath, true); break;
                case "device-muted": Device(reportPath, false); break;
                default: throw new ArgumentException("Unknown mode: " + mode);
            }
            Require(NativeOwner.LiveObjects == 0 && NativeOwner.LiveObjectBytes == 0, "all library-allocated object owners released");
            Require(NativeAllocationTracker.CleanupFailures.Count == 0, "all allocation tracker ledgers drained");
            completed = true;
            return 0;
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(failure); return 1; }
        finally
        {
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new
            {
                Completed = completed, Mode = mode, Utc = DateTimeOffset.UtcNow,
                Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                Checks = checks, CheckCount = checks.Count, Observations = observations,
                NativeOwner.LiveObjects, NativeOwner.LiveObjectBytes, NativeAllocationTracker.CleanupFailures, Failure = failure
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{mode}: completed={completed}, checks={checks.Count}; {reportPath}");
        }
    }

    private static void Inspect()
    {
        string native = Path.Combine(AppContext.BaseDirectory, "miniaudioex.dll");
        Require(File.Exists(native), "RID native asset deployed");
        using (var pe = new PEReader(File.OpenRead(native)))
            Require(pe.PEHeaders.CoffHeader.Machine == System.Reflection.PortableExecutable.Machine.Amd64, "native machine AMD64");
        observations["NativeSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(native)));
        observations["ManagedSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MiniAudioNative).Assembly.Location)));
        observations["ManagedAssembly"] = typeof(MiniAudioNative).Assembly.FullName;
        observations["NativeObjectSizes"] = new[] { ma_allocation_type.context, ma_allocation_type.device,
            ma_allocation_type.engine, ma_allocation_type.sound, ma_allocation_type.resource_manager,
            ma_allocation_type.resource_manager_data_source }
            .ToDictionary(x => x.ToString(), x => (ulong)(nuint)MiniAudioNative.ma_get_size_of_type(x));
        observations["AllNativeObjectSizes"] = Enum.GetValues<ma_allocation_type>()
            .ToDictionary(x => x.ToString(), x => (ulong)(nuint)MiniAudioNative.ma_get_size_of_type(x));
        Require(MiniAudioNative.ma_get_bytes_per_sample(ma_format.f32) == 4, "scalar ABI float32");
        Require((nuint)MiniAudioNative.ma_get_size_of_type(ma_allocation_type.engine) > 0, "native object allocation size available");
        using var allocator = new NativeAllocationTracker();
        using (var graph = new AudioGraph(allocator))
        {
            Require(MiniAudioNative.ma_engine_get_channels(graph.Engine) == 2 &&
                    MiniAudioNative.ma_engine_get_sample_rate(graph.Engine) == 48000, "config marshals: stereo 48000 no-device engine");
            Require(graph.Render(128).All(x => x == 0), "offline engine opens without device and renders silence");
        }
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "inspect native allocator drained");
    }

    private static string FilePath(string name) => Path.Combine(fixtures, name);
    private static void Start(ma_sound_ptr sound) => AudioGraph.Check(MiniAudioNative.ma_sound_start(sound), "start");
    private static void Stop(ma_sound_ptr sound) => AudioGraph.Check(MiniAudioNative.ma_sound_stop(sound), "stop");
    private static ulong Cursor(ma_sound_ptr sound)
    { AudioGraph.Check(MiniAudioNative.ma_sound_get_cursor_in_pcm_frames(sound, out ulong cursor), "cursor"); return cursor; }
    private static void Seek(ma_sound_ptr sound, ulong frame) => AudioGraph.Check(MiniAudioNative.ma_sound_seek_to_pcm_frame(sound, frame), "seek");
    private static double Rms(float[] pcm) => pcm.Length == 0 ? 0 : Math.Sqrt(pcm.Sum(x => (double)x * x) / pcm.Length);

    private static void Offline()
    {
        using var allocator = new NativeAllocationTracker();
        using (var graph = new AudioGraph(allocator))
        {
            var sound = graph.Load(FilePath("constant.wav"), false);
            Start(sound);
            float[] full = graph.Render(1024);
            Require(full.Skip(128).All(x => Math.Abs(x - .25f) < .0001), "known PCM mono duplicates to stereo");
            MiniAudioNative.ma_sound_set_volume(sound, .5f);
            float[] half = graph.Render(1024);
            Require(half.Skip(128).All(x => Math.Abs(x - .125f) < .0001), "gain .5 applied to known PCM");
            Stop(sound); ulong before = Cursor(sound);
            Require(graph.Render(256).All(x => x == 0) && Cursor(sound) == before, "pause freezes cursor and renders silence");
            Start(sound); graph.Render(256);
            Require(Cursor(sound) > before, "resume advances cursor");
            Seek(sound, 1000); graph.Render(1024);
            observations["SeekCursor"] = new { Requested = 1000, Rendered = 1024, Actual = Cursor(sound) };
            Require(Cursor(sound) == 2024, "seek measured in PCM frames after full mix block");
            graph.Release(sound);
            Require(graph.Render(256).All(x => x == 0), "released source contributes no samples");

            var impulse = graph.Load(FilePath("impulse.wav"), false, true);
            Start(impulse); float[] loop = graph.Render(32);
            Require(Enumerable.Range(0, 32).All(i => Math.Abs(loop[i * 2] - (i % 8 == 2 ? .5f : 0)) < .0001), "exact WAV loop boundary");
            graph.Release(impulse);

            var sfx = graph.Load(FilePath("эффект.wav"), false);
            Start(sfx); Require(Rms(graph.Render(12000)) > .1, "UTF-16 Cyrillic path sound plays");
            graph.Render(256);
            Require(MiniAudioNative.ma_sound_at_end(sfx) != 0, "one-shot reaches end"); graph.Release(sfx);

            var voices = Enumerable.Range(0, 64).Select(_ => graph.Load(FilePath("constant.wav"), false, true)).ToArray();
            foreach (var voice in voices) { MiniAudioNative.ma_sound_set_volume(voice, 1f / 64); Start(voice); }
            Require(Math.Abs(Rms(graph.Render(1024)) - .25) < .005, "64 simultaneous voices mix known PCM");
            foreach (var voice in voices) graph.Release(voice);

            var ogg = graph.Load(FilePath("музыка-A.ogg"), true, true);
            var lengthResult = MiniAudioNative.ma_sound_get_length_in_pcm_frames(ogg, out ulong length);
            Require(lengthResult is ma_result.success or ma_result.not_implemented, "OGG reports length or explicit unavailable outcome");
            observations["OggDecodedLengthFrames"] = length;
            observations["OggLengthResult"] = lengthResult.ToString();
            observations["OggLengthLimitation"] = "Bundled libvorbis reports no usable length; duration must be captured separately.";
            Start(ogg); Require(Rms(graph.Render(4800)) > .02, "Ogg Vorbis streaming produces PCM");
            Require(Cursor(ogg) >= 4700 && Cursor(ogg) <= 5300, "OGG 44.1kHz stream advances in 48kHz decoded frames");
            Seek(ogg, 6 * 48000); Thread.Sleep(50);
            Require(Rms(graph.Render(4800)) > .02 && Cursor(ogg) >= 6 * 48000, "stream seek refills and advances");
            graph.Release(ogg);

            ExpectLoadFailure(graph, "missing.wav"); ExpectLoadFailure(graph, "corrupt.wav");
        }
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "offline graph allocator returns to zero");
        observations["OfflinePeakTrackedNativeBytes"] = allocator.PeakBytes;
        StreamAndLifetime();
        StartupFailure();
    }

    private static void ExpectLoadFailure(AudioGraph graph, string name)
    {
        bool rejected = false;
        try { graph.Load(FilePath(name), true); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "invalid input rejected: " + name);
    }

    private static void CleanupProbe()
    {
        using var allocator = new NativeAllocationTracker();
        var snapshots = new List<object>();
        var graph = new AudioGraph(allocator);
        snapshots.Add(new { Phase = "engine", allocator.Bytes, allocator.Blocks });
        var sound = graph.Load(FilePath("constant.wav"), false);
        snapshots.Add(new { Phase = "sound-loaded", allocator.Bytes, allocator.Blocks });
        graph.Release(sound);
        snapshots.Add(new { Phase = "sound-released", allocator.Bytes, allocator.Blocks });
        graph.Dispose();
        snapshots.Add(new { Phase = "graph-disposed", allocator.Bytes, allocator.Blocks });
        observations["AllocationSnapshots"] = snapshots;
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "minimal loaded-sound cleanup");
    }

    private static void RawVorbisDefect()
    {
        using var allocator = new NativeAllocationTracker();
        using (var graph = new AudioGraph(allocator, rawVorbis: true))
        { var sound = graph.Load(FilePath("constant.wav"), false); graph.Release(sound); }
        observations["RawVorbisLeak"] = new { allocator.Bytes, allocator.Blocks };
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "raw libvorbis probing cleans up rejected WAV");
    }

    private static void MalformedVorbisDefect()
    {
        using var allocator = new NativeAllocationTracker();
        using (var graph = new AudioGraph(allocator)) ExpectLoadFailure(graph, "corrupt.ogg");
        observations["MalformedVorbisLeak"] = new { allocator.Bytes, allocator.Blocks };
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "malformed OGG failure releases native allocations");
    }

    private static void FailureStress()
    {
        Require(AudioGraph.RawVorbisByDefault, "failure stress uses the native decoder without routing guard");
        using var allocator = new NativeAllocationTracker();
        string[] bad = ["corrupt.wav", "corrupt.ogg", "truncated.ogg", "bad-crc.ogg"];
        using (var graph = new AudioGraph(allocator))
        {
            // Warm successful load before taking a stable resource-manager baseline.
            var warm = graph.Load(FilePath("valid.ogg"), true); graph.Release(warm);
            long baselineBytes = allocator.Bytes, baselineBlocks = allocator.Blocks;
            observations["FailureBaseline"] = new { baselineBytes, baselineBlocks };
            for (int cycle = 0; cycle < 16; cycle++)
            {
                foreach (string name in bad)
                {
                    ExpectLoadFailure(graph, name);
                    Require(CanOpenExclusive(FilePath(name)), $"failure cycle {cycle + 1}: {name} file closed");
                    Require(allocator.Bytes == baselineBytes && allocator.Blocks == baselineBlocks,
                        $"failure cycle {cycle + 1}: {name} allocations returned to baseline");
                }
            }
            var valid = graph.Load(FilePath("valid.ogg"), true, true); Start(valid);
            Require(Rms(graph.Render(4800)) > .02, "valid OGG still plays after 64 malformed-input failures");
        }
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "failure stress native allocations zero after graph disposal");
        observations["FailureCycles"] = 64;
    }

    private static void StreamAndLifetime()
    {
        using var allocator = new NativeAllocationTracker();
        long baselineHandles = HandleCount();
        using (var graph = new AudioGraph(allocator))
        {
            var music = graph.Load(FilePath("long-music.wav"), true, true);
            Require(!CanOpenExclusive(FilePath("long-music.wav")), "stream retains file while active");
            Start(music); Require(Rms(graph.Render(4800)) > .02, "long streamed WAV produces PCM");
            Seek(music, 599 * 48000); Thread.Sleep(50);
            for (int i = 0; i < 120; i++) { graph.Render(512); Thread.Sleep(1); }
            Require(Cursor(music) < 2 * 48000, "long stream crosses EOF and loops");
            observations["LongStream"] = new
            {
                FileBytes = new FileInfo(FilePath("long-music.wav")).Length,
                FullDecodedFloatBytes = 600L * 48000 * 2 * sizeof(float),
                PeakTrackedNativeBytes = allocator.PeakBytes
            };
            Require(allocator.PeakBytes < 16 * 1024 * 1024, "stream working allocations below 16 MiB for ten-minute WAV");
        }
        Require(CanOpenExclusive(FilePath("long-music.wav")), "stream file unlocked after graph dispose");
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "stream graph native allocation ledger zero");
        var handleSamples = new List<int>();
        var privateByteSamples = new List<long>();
        for (int i = 0; i < 32; i++)
        {
            using var cycleAllocator = new NativeAllocationTracker();
            var graph = new AudioGraph(cycleAllocator);
            var sound = graph.Load(FilePath("музыка-A.ogg"), true, true);
            Start(sound); graph.Render(512);
            graph.Dispose(); graph.Dispose(); // Active playback + workers, deliberately repeated dispose.
            Require(cycleAllocator.Bytes == 0 && cycleAllocator.Blocks == 0, $"active dispose cycle {i + 1}: native allocations zero");
            Require(CanOpenExclusive(FilePath("музыка-A.ogg")), $"active dispose cycle {i + 1}: file closed");
            if (i % 4 == 0)
            {
                // Reverse P/Invoke attaches native workers to the CLR; measure after finalizers retire their thread handles.
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                handleSamples.Add(HandleCount());
                using var process = Process.GetCurrentProcess();
                privateByteSamples.Add(process.PrivateMemorySize64);
            }
        }
        observations["Lifetime"] = new { Cycles = 32, BaselineHandles = baselineHandles, HandleSamples = handleSamples, PrivateByteSamples = privateByteSamples };
        Require(handleSamples[^1] <= handleSamples[1] + 8, "post-GC OS handle trend remains bounded across disposal cycles");
        Require(privateByteSamples[^1] <= privateByteSamples[1] + 16 * 1024 * 1024, "post-GC process private bytes remain bounded across valid OGG cycles");
        using var firstAlloc = new NativeAllocationTracker();
        using var secondAlloc = new NativeAllocationTracker();
        var first = new AudioGraph(firstAlloc); var second = new AudioGraph(secondAlloc);
        try
        {
            Start(first.Load(FilePath("constant.wav"), false, true));
            Start(second.Load(FilePath("constant.wav"), false, true));
            Require(Rms(first.Render(256)) > .2 && Rms(second.Render(256)) > .2, "two independent engines render");
            first.Dispose();
            Require(Rms(second.Render(256)) > .2, "disposing one engine preserves the other");
        }
        finally { second.Dispose(); first.Dispose(); }
        Require(firstAlloc.Bytes == 0 && secondAlloc.Bytes == 0, "both independent allocators drained");
    }

    private static bool CanOpenExclusive(string path)
    {
        try { using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); return true; }
        catch (IOException) { return false; }
    }

    private static int HandleCount()
    { using var process = Process.GetCurrentProcess(); return process.HandleCount; }

    private static void StartupFailure()
    {
        using var allocations = new NativeAllocationTracker();
        allocations.FailNextAllocation();
        bool failed = false;
        try { using var graph = new AudioGraph(allocations); }
        catch (InvalidOperationException) { failed = true; }
        Require(failed, "injected allocation failure rejects startup");
        Require(allocations.Bytes == 0 && allocations.Blocks == 0, "failed startup releases partial native graph");
    }

    private static void Device(string reportPath, bool audible)
    {
        using var allocator = new NativeAllocationTracker();
        float[] rendered, captured = [];
        using (var graph = new AudioGraph(allocator, physical: true))
        {
            observations["Device"] = graph.DeviceInfo;
            LoopbackCapture? optionalCapture = null;
            try { if (audible) optionalCapture = new LoopbackCapture(graph.Context); }
            catch (InvalidOperationException error) { observations["LoopbackUnavailableOutcome"] = error.Message; }
            using var capture = optionalCapture;
            observations["LoopbackAvailable"] = capture is not null;
            observations["AudibleRequested"] = audible;
            var sfx = graph.Load(FilePath("эффект.wav"), false);
            var musicA = graph.Load(FilePath("музыка-A.ogg"), true, true);
            var musicB = graph.Load(FilePath("музыка-B.ogg"), true, true);
            Start(sfx); graph.StartDevice(audible);
            Console.WriteLine($"Playback: audible={audible}; effect, generated Ogg music, crossfade, pause/resume. Master gain 0.22.");
            Thread.Sleep(500); Start(musicA);
            Thread.Sleep(2500);
            MiniAudioNative.ma_sound_set_fade_in_milliseconds(musicA, 1, 0, 1200);
            MiniAudioNative.ma_sound_set_fade_in_milliseconds(musicB, 0, 1, 1200);
            Start(musicB);
            Thread.Sleep(1600); Stop(musicA);
            GC.Collect(); GC.WaitForPendingFinalizers();
            Stop(musicB); ulong paused = Cursor(musicB); Thread.Sleep(350);
            Require(Cursor(musicB) == paused, "actual device pause freezes music cursor");
            Start(musicB); Thread.Sleep(1800);
            Seek(musicB, 10 * 48000); Thread.Sleep(2800);
            observations["MusicCursorAfterSeekAndLoop"] = Cursor(musicB);
            float[] afterLoop = graph.Telemetry!.CopyPcm();
            double tailRms = Rms(afterLoop.TakeLast(48000).ToArray());
            observations["MusicTailRmsAfterKnownEof"] = tailRms;
            observations["UnknownLengthCursorPolicy"] = "With unavailable duration, cursor remains monotonic across decoder loops.";
            Require(tailRms > .005 && MiniAudioNative.ma_sound_at_end(musicB) == 0 &&
                    MiniAudioNative.ma_sound_is_looping(musicB) != 0, "actual OGG audio continues beyond known fixture EOF after seek");
            graph.StopDevice();
            int blocks = graph.Telemetry!.Blocks; Thread.Sleep(100);
            Require(graph.Telemetry.Blocks == blocks, "no playback callbacks after stop/drain");
            if (capture is not null) captured = capture.StopAndCopy();
            rendered = graph.Telemetry.CopyPcm();
            observations["PlaybackCallbacks"] = graph.Telemetry.Snapshot();
            observations["LoopbackFrames"] = captured.Length / 2;
            observations["ProcessLoopbackOnly"] = capture is not null;
            Require(graph.Telemetry.Failures == 0 && graph.Telemetry.OverflowBlocks == 0, "device callbacks have no mix errors or capture overflow");
            Require(Rms(rendered) > .005 && rendered.All(float.IsFinite), "actual playback callback contains finite audio");
            if (capture is not null)
            {
                Require(Rms(captured) > .00005, "own-process WASAPI loopback contains actual output");
                double correlation = Correlate(rendered, captured);
                observations["LoopbackCorrelation"] = correlation;
                Require(correlation > .55, "loopback matches generated playback signature");
            }
        }
        Require(allocator.Bytes == 0 && allocator.Blocks == 0, "device graph native allocations zero after drain/dispose");
        Require(CanOpenExclusive(FilePath("музыка-A.ogg")) && CanOpenExclusive(FilePath("музыка-B.ogg")), "actual device stream files closed");
        observations["DevicePeakTrackedNativeBytes"] = allocator.PeakBytes;
        Fixtures.WriteWave(Path.ChangeExtension(reportPath, ".rendered.wav"), 48000, 2, rendered);
        if (captured.Length > 0) Fixtures.WriteWave(Path.ChangeExtension(reportPath, ".loopback.wav"), 48000, 2, captured);
        for (int i = 0; i < 6; i++)
        {
            using var cycleAllocator = new NativeAllocationTracker();
            var graph = new AudioGraph(cycleAllocator, true);
            graph.StartDevice(audible: false); Thread.Sleep(60); graph.Dispose(); graph.Dispose();
            Require(cycleAllocator.Bytes == 0 && cycleAllocator.Blocks == 0, $"WASAPI reopen/dispose cycle {i + 1} drains");
        }
    }

    private static double Correlate(float[] rendered, float[] captured)
    {
        // Pearson correlation of the 1 kHz energy envelope, tolerant of buffering and mixer gain.
        static double[] Envelope(float[] pcm)
        {
            double[] result = new double[Math.Min(pcm.Length / 96, 8000)];
            for (int i = 0; i < result.Length; i++)
                for (int j = 0; j < 96; j++) result[i] += pcm[i * 96 + j] * (double)pcm[i * 96 + j];
            return result;
        }
        double[] a = Envelope(rendered), b = Envelope(captured);
        double best = 0;
        for (int lag = -1000; lag <= 1000; lag += 2)
        {
            double ab = 0, aa = 0, bb = 0, sa = 0, sb = 0; int count = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int k = i + lag; if ((uint)k >= (uint)b.Length) continue;
                ab += a[i] * b[k]; aa += a[i] * a[i]; bb += b[k] * b[k]; sa += a[i]; sb += b[k]; count++;
            }
            if (count > 2000)
            {
                aa -= sa * sa / count; bb -= sb * sb / count; ab -= sa * sb / count;
                if (aa > 0 && bb > 0) best = Math.Max(best, ab / Math.Sqrt(aa * bb));
            }
        }
        return best;
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("CHECK FAILED: " + name);
        checks.Add(new { Name = name, Passed = true });
        Console.WriteLine("PASS " + name);
    }
}
