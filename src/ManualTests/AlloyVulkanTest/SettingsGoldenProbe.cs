using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using AlloyTest;
using SkiaSharp;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

// Full premultiplied RGBA comparison. Baselines are explicitly recorded CPU raster frames.
internal sealed class SettingsGoldenProbe
{
    private const string Schema = "truemoon-settings-golden-v1";
    private static readonly Dictionary<int, string> Frames = new()
    {
        [0] = "initial-dark", [1] = "selection", [15] = "loading", [16] = "error",
        [22] = "light", [23] = "light-compact", [24] = "dark-compact", [26] = "disabled",
        [28] = "scrolled", [30] = "restored"
    };
    private readonly string _fixtures, _output;
    private readonly bool _record;
    private readonly List<Comparison> _comparisons = [];
    private readonly Dictionary<string, byte[]> _raster = [];
    private readonly Dictionary<string, byte[]> _opengl = [];
    private readonly List<Fixture> _recorded = [];
    private readonly Dictionary<string, object> _hardware = [];
    private readonly Manifest? _manifest;
    private readonly string _fontHash, _assetHash, _nativeHash;
    private readonly Dictionary<string, string> _fontFiles;
    private sealed record Fixture(string File, string Sha256, int Width, int Height, float Scale, int Frame);
    private sealed record Manifest(string Schema, string FontSha256, string AssetSha256, string NativeSha256, int ChannelTolerance, int GpuEdgeRadius, int GpuEdgeTolerance, Fixture[] Fixtures);
    private sealed record Comparison(string File, string Backend, string Reference, PixelDifference Result);
    internal SkiaFontCatalog Fonts { get; }

    private SettingsGoldenProbe(string fixtures, string output, bool record)
    {
        _fixtures = Path.GetFullPath(fixtures); _output = Path.GetFullPath(output); _record = record;
        var relativeOutput = Path.GetRelativePath(_fixtures, _output);
        if (!Path.IsPathRooted(relativeOutput) && relativeOutput != ".." && !relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Golden output must be outside the fixture directory.");
        if (Directory.Exists(_output) && Directory.EnumerateFileSystemEntries(_output).Any())
            throw new ArgumentException("Use a new empty golden output directory.");
        Directory.CreateDirectory(_output);
        var font = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf");
        var symbols = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSansSymbols-Regular.ttf");
        _fontFiles = new() { [Path.GetFileName(font)] = HashFile(font), [Path.GetFileName(symbols)] = HashFile(symbols) };
        _fontHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', _fontFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)))));
        _assetHash = HashFile(Path.Combine(AppContext.BaseDirectory, "Assets/alloy-mark.png"));
        _nativeHash = HashFile(SkiaNativeLibrary.LoadedPath!);
        Fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = font, ["Segoe UI Symbol"] = symbols });
        if (record)
        {
            if (Directory.Exists(_fixtures) && Directory.EnumerateFileSystemEntries(_fixtures).Any())
                throw new ArgumentException("Record into a new fixture directory, then review and replace the old fixtures explicitly.");
            Directory.CreateDirectory(_fixtures);
        }
        else
        {
            _manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(_fixtures, "manifest.json")))
                ?? throw new InvalidOperationException("Missing golden manifest.");
            if (_manifest.Schema != Schema || _manifest.FontSha256 != _fontHash || _manifest.AssetSha256 != _assetHash
                || _manifest.NativeSha256 != _nativeHash || _manifest.ChannelTolerance != 2 || _manifest.GpuEdgeRadius != 1 || _manifest.GpuEdgeTolerance != 24
                || _manifest.Fixtures.Length != Frames.Count * 3)
                throw new InvalidOperationException("Golden fixture provenance/tolerance differs. Review before explicitly recording a new baseline.");
            foreach (var fixture in _manifest.Fixtures)
            {
                if (Path.GetFileName(fixture.File) != fixture.File || HashFile(Path.Combine(_fixtures, fixture.File)) != fixture.Sha256)
                    throw new InvalidOperationException("Golden fixture SHA256 mismatch.");
            }
        }
    }

    internal static void Run(string[] args)
    {
        string Value(string name, string fallback)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"{name} requires a path.");
            return args[index + 1];
        }
        var record = args.Contains("--record-golden");
        if (record && !args.Contains("--golden-fixtures")) throw new ArgumentException("Recording requires an explicit new --golden-fixtures directory.");
        var probe = new SettingsGoldenProbe(Value("--golden-fixtures", Path.Combine(AppContext.BaseDirectory, "Golden/Settings")),
            Value("--golden-output", Path.Combine("TestResults/AlloyGolden", DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture))), record);
        Exception? failure = null;
        try
        {
            SettingsBackendProbe.Run(args.Contains("--validation"), probe);
            if (probe._comparisons.Count != (record ? 90 : 180)) throw new InvalidOperationException("Golden matrix incomplete.");
            if (probe._comparisons.Any(result => !result.Result.Passed)) throw new InvalidOperationException("Full-image golden comparison failed. See report/diff PNGs.");
            if (record) File.WriteAllText(Path.Combine(probe._fixtures, "manifest.json"), JsonSerializer.Serialize(
                new Manifest(Schema, probe._fontHash, probe._assetHash, probe._nativeHash, 2, 1, 24, probe._recorded.ToArray()), JsonOptions));
        }
        catch (Exception error) { failure = error; throw; }
        finally { probe.Report(failure, args.Contains("--validation")); }
        Console.WriteLine($"Settings golden: {probe._comparisons.Count} full-frame comparisons passed; output {probe._output}.");
    }

    internal bool CapturesFrame(int frame) => Frames.ContainsKey(frame);
    internal void RecordHardware(string backend, object hardware) => _hardware[backend] = hardware;
    internal void Capture(string backend, int frame, UiViewport viewport, byte[] pixels)
    {
        var file = $"scale-{viewport.Scale.ToString("0.0", CultureInfo.InvariantCulture)}-{Frames[frame]}.png";
        Save(Path.Combine(_output, backend + "-" + file), viewport.Width, viewport.Height, pixels);
        RequireAlpha(pixels, viewport);
        if (backend == "raster")
        {
            if (frame == 0) _raster.Clear(); // Retain one scale's reference frames, not the complete matrix.
            _raster.Add(file, pixels);
            if (_record)
            {
                var path = Path.Combine(_fixtures, file); Save(path, viewport.Width, viewport.Height, pixels);
                _recorded.Add(new(file, HashFile(path), viewport.Width, viewport.Height, viewport.Scale, frame));
                return;
            }
        }
        if (!_record)
        {
            var fixture = _manifest!.Fixtures.Single(item => item.File == file);
            if (fixture.Width != viewport.Width || fixture.Height != viewport.Height || fixture.Scale != viewport.Scale || fixture.Frame != frame)
                throw new InvalidOperationException("Golden dimensions/frame differ.");
            using var image = SKImage.FromEncodedData(Path.Combine(_fixtures, file));
            if (image == null || image.Width != viewport.Width || image.Height != viewport.Height) throw new InvalidOperationException("Invalid golden PNG dimensions.");
            Compare(file, backend, "golden", RasterProbe.ReadSnapshot(image), pixels, viewport);
        }
        if (backend != "raster") Compare(file, backend, "current-raster", _raster[file], pixels, viewport);
        if (backend == "opengl")
        {
            if (frame == 0) _opengl.Clear();
            _opengl.Add(file, pixels);
        }
        if (backend == "vulkan") Compare(file, backend, "current-opengl", _opengl[file], pixels, viewport);
    }

    private void Compare(string file, string backend, string reference, byte[] expected, byte[] actual, UiViewport viewport)
    {
        var result = PixelDifference.Compare(expected, actual, viewport.Width, viewport.Height, 2, backend == "raster" || reference == "current-opengl" ? 0 : 1);
        _comparisons.Add(new(file, backend, reference, result));
        if (!result.Passed) Save(Path.Combine(_output, $"diff-{reference}-{backend}-{file}"), viewport.Width, viewport.Height,
            PixelDifference.Diff(expected, actual));
    }

    private static void RequireAlpha(byte[] pixels, UiViewport viewport)
    {
        int Alpha(int x, int y) => pixels[((int)(y * viewport.Scale) * viewport.Width + (int)(x * viewport.Scale)) * 4 + 3];
        if (Alpha(899, 1099) != 0 || Alpha(780, 90) != 128 || Alpha(32, 32) != 255)
            throw new InvalidOperationException("Golden transparent/half-alpha/opaque regions changed.");
    }

    internal static Element Wrap(View1 view)
    {
        view.Width = 720; view.Height = 1000;
        var root = new GoldenPanel(); root.Add(view); return root;
    }
    private sealed class GoldenPanel : Panel
    {
        protected override void DrawCore(IDrawingContext context)
        {
            context.Fill(new Rect(760, 80, 80, 70), new Color(200, 80, 40, 128));
            context.Fill(new Rect(800, 110, 70, 70), new Color(40, 80, 200, 128), 8);
            context.Save();
            try { context.Clip(new Rect(760, 220, 90, 30)); context.Text("Clipping Привет", 745, 215, new Color(240, 240, 240, 180), 24, "unregistered-golden-family"); }
            finally { context.Restore(); }
        }
    }

    private void Report(Exception? failure, bool validation)
    {
        var root = new DirectoryInfo(Environment.CurrentDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "TrueMoon.slnx"))) root = root.Parent;
        var sources = root == null ? [] : new[] { "TrueMoon.Argentis", "TrueMoon.Alloy", "TrueMoon.Alloy.Rendering.Skia", "ManualTests/AlloyTest", "ManualTests/AlloyVulkanTest" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root.FullName, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal).Select(path => new { File = Path.GetRelativePath(root.FullName, path), Sha256 = HashFile(path) }).ToArray();
        File.WriteAllText(Path.Combine(_output, "report.json"), JsonSerializer.Serialize(new
        {
            Schema, CreatedUtc = DateTime.UtcNow, Passed = failure == null, Error = failure?.ToString(), Record = _record,
            Configuration = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            Runtime = RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture,
            FontSha256 = _fontHash, FontFiles = _fontFiles, AssetSha256 = _assetHash, NativeSha256 = _nativeHash,
            AssemblySha256 = HashFile(Assembly.GetExecutingAssembly().Location), SourceFiles = sources,
            ValidationRequested = validation, ValidationPassedIncludingTeardown = validation && failure == null,
            ChannelTolerance = 2, GpuEdgeRadius = 1, GpuEdgeTolerance = 24, Scope = "Full premultiplied RGBA, no excluded pixels or allowed outliers; raster/golden and GL/Vulkan strict per-channel; GPU/raster symmetric 3x3 per-channel envelope: contrast>=8 allows envelope+24, otherwise +2. 10 frames x 3 logical scales; 40-step layout/model/input equivalence. No physical DPI/WSI/device-loss/VRAM proof.",
            Hardware = _hardware, Comparisons = _comparisons,
            BinaryFiles = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll").Order(StringComparer.Ordinal)
                .Select(path => new { File = Path.GetFileName(path), Sha256 = HashFile(path) }).ToArray()
        }, JsonOptions));
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Save(string path, int width, int height, byte[] pixels)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path); data.SaveTo(stream);
    }
}
