using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TrueMoon.Aether.Builds;

// Low-level staging resource owned by the compiler's job. No DI service or independent lifetime.
internal sealed class GameBuildCompilation
{
    private static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly HashSet<string> Scalars = new(StringComparer.OrdinalIgnoreCase)
    {
        "noconfig", "unsafe", "checked", "nowarn", "fullpaths", "nostdlib", "errorreport", "warn", "define", "highentropyva",
        "nullable", "features", "debug", "filealign", "optimize", "warnaserror", "utf8output", "deterministic", "langversion",
        "checksumalgorithm", "codepage", "platform", "baseaddress", "subsystemversion", "preferreduilang", "errorendlocation",
        "reportanalyzer", "reportivts", "embed"
    };
    private readonly Dictionary<string, string> _copies = new(Paths), _roots = new(Paths);
    private readonly List<GameBuildInputFile> _files = [];
    private readonly string _job, _directory, _projectDirectory, _sdk, _packs;
    private readonly GameBuildInputSnapshot _inputs;
    private long _bytes;
    private GameBuildCompilation(string job, GameBuildInputSnapshot inputs, string sdk, string packs)
    {
        _job = Path.GetFullPath(job); _directory = Path.Combine(_job, "capture"); _inputs = inputs;
        _projectDirectory = Path.GetDirectoryName(inputs.Request.ProjectPath)!; _sdk = Path.GetFullPath(sdk); _packs = Path.GetFullPath(packs);
    }
    internal GameBuildCompilationSnapshot Snapshot { get; private set; } = null!;
    internal ProcessStartInfo StartInfo { get; private set; } = null!;
    internal IReadOnlyDictionary<string, string> OriginalPaths => _copies.ToDictionary(p => p.Value, p => p.Key, Paths);

    internal static async Task<GameBuildCompilation> CaptureAsync(GameBuildInputSnapshot inputs, string preparation, string job, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Encoding.UTF8.GetByteCount(preparation) > GameBuildInputSnapshot.MaximumDocumentBytes) throw new InvalidDataException("Compiler preparation exceeds its document limit.");
        using var document = JsonDocument.Parse(preparation);
        var properties = document.RootElement.GetProperty("Properties");
        string Property(string name) => properties.GetProperty(name).GetString()!;
        if (new[] { "CscToolPath", "CscToolExe", "CscEnvironment" }.Any(name => !string.IsNullOrEmpty(Property(name))))
            throw new NotSupportedException("Captured compilation requires the SDK compiler without custom Csc tools/environment.");
        var capture = new GameBuildCompilation(job, inputs, Property("MSBuildToolsPath"), Property("NetCoreTargetingPackRoot"));
        var compiler = Path.Combine(Path.GetFullPath(Property("RoslynTargetsPath")), "bincore");
        if (!Inside(compiler, capture._sdk)) throw new NotSupportedException("Compiler must belong to the selected SDK.");
        Directory.CreateDirectory(capture._directory);
        foreach (var name in new[] { "csc.dll", "csc.deps.json", "csc.runtimeconfig.json", "Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll" })
            await capture.CopyAsync(Path.Combine(compiler, name), token).ConfigureAwait(false);
        var raw = document.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs").EnumerateArray()
            .Select(row => row.GetProperty("Identity").GetString()!).ToArray();
        if (raw.Length is < 1 or > 8192 || raw.Any(arg => arg.IndexOfAny(['\r', '\n', '\0']) >= 0)) throw new InvalidDataException("Invalid captured compiler arguments.");
        var arguments = new List<string>(); var pathMaps = new List<string>(); var sources = 0; var output = false; var library = false;
        foreach (var argument in raw)
        {
            token.ThrowIfCancellationRequested();
            if (argument.StartsWith('@')) throw new NotSupportedException("Nested compiler response files are unsupported by capture.");
            if (!(argument.StartsWith('/') || argument.StartsWith('-')) || Path.IsPathFullyQualified(Unquote(argument)) && File.Exists(Unquote(argument)))
            {
                arguments.Add(Quote(await capture.CopyAsync(Unquote(argument), token).ConfigureAwait(false))); sources++; continue;
            }
            var split = argument.IndexOf(':'); var name = argument[1..(split < 0 ? argument.Length : split)].TrimEnd('+', '-');
            var value = split < 0 ? "" : Unquote(argument[(split + 1)..]);
            switch (name.ToLowerInvariant())
            {
                case "noconfig": break; // This option must be on the command line, outside the response file.
                case "reference":
                    var equals = value.IndexOf('='); var alias = equals < 0 ? "" : value[..(equals + 1)]; var path = Unquote(value[(equals + 1)..]);
                    arguments.Add("/reference:" + alias + Quote(await capture.CopyAsync(path, token).ConfigureAwait(false))); break;
                case "analyzer":
                    var analyzer = Path.GetFullPath(value, capture._projectDirectory);
                    if (!Inside(analyzer, capture._sdk) && !Inside(analyzer, capture._packs)) throw new NotSupportedException("External analyzers/generators are unsupported by capture.");
                    arguments.Add("/analyzer:" + Quote(await capture.CopyAsync(analyzer, token).ConfigureAwait(false))); break;
                case "analyzerconfig":
                case "additionalfile":
                    var copy = await capture.CopyAsync(value, token).ConfigureAwait(false);
                    if (name.Equals("analyzerconfig", StringComparison.OrdinalIgnoreCase))
                    {
                        if (new FileInfo(copy).Length > GameBuildInputSnapshot.MaximumDocumentBytes) throw new InvalidDataException("Analyzer config exceeds its capture limit.");
                        if (Regex.IsMatch(await File.ReadAllTextAsync(copy, token).ConfigureAwait(false), @"(?m)^[ \t]*\[[ \t]*(?:/|[A-Za-z]:)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking))
                            throw new NotSupportedException("Absolute analyzer-config section patterns are unsupported by capture.");
                    }
                    arguments.Add("/" + name + ":" + Quote(copy)); break;
                case "out":
                    if (!Path.GetFileName(value).Equals(inputs.Request.EntryAssembly, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Captured compiler output does not match the declared entry assembly.");
                    output = true; arguments.Add("/out:" + Quote(Path.Combine(job, "output", inputs.Request.EntryAssembly))); break;
                case "refout": arguments.Add("/refout:" + Quote(Path.Combine(capture._directory, "reference.dll"))); break;
                case "pdb": arguments.Add("/pdb:" + Quote(Path.Combine(capture._directory, "game.pdb"))); break;
                case "doc": arguments.Add("/doc:" + Quote(Path.Combine(capture._directory, "game.xml"))); break;
                case "errorlog": arguments.Add("/errorlog:" + Quote(Path.Combine(job, GameBuildDiagnosticReader.FileName)) + ",version=2.1"); break;
                case "target":
                    if (!value.Equals("library", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Captured game compilation requires target:library.");
                    library = true; arguments.Add(argument); break;
                case "pathmap":
                    foreach (var mapping in value.Split(','))
                    {
                        var pair = mapping.Split('='); if (pair.Length != 2) throw new NotSupportedException("Unsupported compiler path map.");
                        pathMaps.Add(capture.Mirror(Path.GetFullPath(pair[0], capture._projectDirectory)) + "=" + pair[1]);
                    }
                    break;
                default:
                    if (!Scalars.Contains(name) || name.Equals("embed", StringComparison.OrdinalIgnoreCase) && value.Length != 0)
                        throw new NotSupportedException("Unsupported captured compiler switch: " + name);
                    arguments.Add(argument); break;
            }
        }
        if (!output || !library || sources == 0) throw new InvalidDataException("Incomplete C# library invocation.");
        // Preserve CallerFilePath/PDB paths. More-specific user maps precede the root maps.
        pathMaps.Add(capture.Mirror(capture._job) + "=/_/TrueMoon.Build");
        pathMaps.AddRange(capture._roots.Select(p => Path.TrimEndingDirectorySeparator(p.Value) + "=" + Path.TrimEndingDirectorySeparator(p.Key)));
        pathMaps.Add(capture._job + "=/_/TrueMoon.Build"); // PDB/output paths are owned but are not mirrored inputs.
        if (pathMaps.Any(map => map.Contains('"') || map.Contains(','))) throw new NotSupportedException("Unsupported path-map punctuation.");
        arguments.Add("/pathmap:" + Quote(string.Join(',', pathMaps)));
        var response = Path.Combine(capture._directory, "compiler.rsp");
        if (arguments.Sum(argument => (long)Encoding.UTF8.GetByteCount(argument) + 1) > GameBuildInputSnapshot.MaximumDocumentBytes)
            throw new InvalidDataException("Captured compiler response exceeds its limit.");
        Directory.CreateDirectory(Path.Combine(job, "output"));
        await File.WriteAllLinesAsync(response, arguments, new UTF8Encoding(false), token).ConfigureAwait(false);
        foreach (var name in inputs.Request.Dependencies ?? [])
        {
            var rows = document.RootElement.GetProperty("Items").GetProperty("ReferenceCopyLocalPaths").EnumerateArray()
                .Select(row => row.GetProperty("FullPath").GetString()!).Where(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rows.Length != 1) throw new InvalidDataException("Captured private dependency is missing or ambiguous: " + name);
            var dependency = await capture.CopyAsync(rows[0], token).ConfigureAwait(false);
            File.Copy(dependency, Path.Combine(job, "output", name));
        }
        capture.Snapshot = new(Property("NETCoreSdkVersion"), capture._files, raw, job, inputs.Fingerprint);
        capture.StartInfo = new("dotnet") { WorkingDirectory = capture._directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", capture._copies[Path.Combine(compiler, "csc.dll")], "/noconfig", "@" + response }) capture.StartInfo.ArgumentList.Add(argument);
        await File.WriteAllTextAsync(Path.Combine(job, "capture.json"), JsonSerializer.Serialize(capture.Snapshot), token).ConfigureAwait(false);
        return capture;
    }

    private string Mirror(string path)
    {
        var full = Path.GetFullPath(path); var root = Path.GetPathRoot(full)!;
        if (!_roots.TryGetValue(root, out var target))
        {
            target = Path.Combine(_directory, "files", Convert.ToHexString(Encoding.UTF8.GetBytes(root))) + Path.DirectorySeparatorChar;
            _roots.Add(root, target);
        }
        var result = Path.GetFullPath(Path.Combine(target, Path.GetRelativePath(root, full)));
        if (!Inside(result, _directory)) throw new InvalidDataException("Captured path escaped its owner.");
        return result;
    }
    private async Task<string> CopyAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var full = Path.GetFullPath(path, _projectDirectory);
        if (_copies.TryGetValue(full, out var existing)) return existing;
        if (_files.Count >= GameBuildInputSnapshot.MaximumFiles) throw new InvalidDataException("Too many captured compiler files.");
        if ((File.GetAttributes(full) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new NotSupportedException("Capture requires regular input files.");
        var target = Mirror(full); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var source = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous);
        var length = source.Length;
        if (length > GameBuildInputSnapshot.MaximumFileBytes || _bytes > GameBuildInputSnapshot.MaximumTotalBytes - length)
            throw new InvalidDataException("Captured compiler inputs exceed their byte budget.");
        await using var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; long read = 0; int count;
        while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            read += count; if (read > length) throw new IOException("Compiler input changed during capture: " + full);
            hash.AppendData(buffer, 0, count); await destination.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
        }
        if (read != length) throw new IOException("Compiler input changed during capture: " + full);
        var sha = Convert.ToHexString(hash.GetHashAndReset());
        var witnessed = _inputs.Files.FirstOrDefault(file => Paths.Equals(file.Path, full));
        if (witnessed != null && (witnessed.Length != length || witnessed.Sha256 != sha)) throw new IOException("Witnessed input changed before capture: " + full);
        _files.Add(new(full, length, sha)); _bytes += length; _copies.Add(full, target); return target;
    }
    private static bool Inside(string path, string directory) => Path.GetFullPath(path).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    private static string Quote(string value)
    {
        if (value.IndexOfAny(['"', '\r', '\n', '\0']) >= 0) throw new NotSupportedException("Unsupported captured path punctuation.");
        return "\"" + value + "\"";
    }
}
