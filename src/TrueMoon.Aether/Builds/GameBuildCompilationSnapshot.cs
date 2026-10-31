using System.Security.Cryptography;
using System.Text.Json;

namespace TrueMoon.Aether.Builds;

/// <summary>Process-local identity of the byte inputs used by a captured C# compilation.</summary>
/// <remarks>MSBuild preparation and the installed dotnet runtime remain trusted. This is not a sandbox for arbitrary generators or build tasks.</remarks>
public sealed class GameBuildCompilationSnapshot
{
    private readonly string[] _arguments;
    private readonly string _job;
    internal GameBuildCompilationSnapshot(string sdkVersion, IEnumerable<GameBuildInputFile> files, IEnumerable<string> arguments, string job, string inputFingerprint)
    {
        _job = Path.TrimEndingDirectorySeparator(Path.GetFullPath(job)); _arguments = arguments.ToArray(); InputFingerprint = inputFingerprint;
        SdkVersion = sdkVersion; Files = Array.AsReadOnly(files.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray());
        Fingerprint = Compute(Files);
    }
    internal string InputFingerprint { get; }
    private string Compute(IEnumerable<GameBuildInputFile> files)
    {
        string Normalize(string value) => value.Replace(_job, "$JOB",
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            SdkVersion, Files = files.Select(f => f with { Path = Normalize(f.Path) }), Arguments = _arguments.Select(Normalize)
        })));
    }
    internal async Task VerifyOriginalsAsync(CancellationToken token)
    {
        var actual = new List<GameBuildInputFile>(); var buffer = new byte[65536]; long total = 0;
        foreach (var file in Files)
        {
            token.ThrowIfCancellationRequested();
            // Generated inputs lived in the completed job; their captured identities remain provenance.
            if (file.Path.StartsWith(_job + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            { actual.Add(file); continue; }
            if (!File.Exists(file.Path)) { actual.Add(file with { Length = -1, Sha256 = "missing" }); continue; }
            if ((File.GetAttributes(file.Path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new NotSupportedException("Captured inputs must remain regular files.");
            await using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous);
            var length = stream.Length;
            if (length > GameBuildInputSnapshot.MaximumFileBytes || total > GameBuildInputSnapshot.MaximumTotalBytes - length) throw new InvalidDataException("Captured inputs exceeded their verification budget.");
            total += length; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long read = 0; int count;
            while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                read += count; if (read > length) throw new IOException("Captured input changed during verification."); hash.AppendData(buffer, 0, count);
            }
            if (read != length) throw new IOException("Captured input changed during verification.");
            actual.Add(new(file.Path, length, Convert.ToHexString(hash.GetHashAndReset())));
        }
        var fingerprint = Compute(actual);
        if (fingerprint != Fingerprint) throw new GameBuildInputsChangedException(Fingerprint, fingerprint);
    }
    /// <summary>SDK version used to prepare the captured compiler invocation.</summary>
    public string SdkVersion { get; }
    /// <summary>SHA256 of captured file identities and original compiler arguments, normalizing owned job paths.</summary>
    public string Fingerprint { get; }
    /// <summary>Immutable original-path identities, including generated sources, reference packs and compiler assemblies.</summary>
    public IReadOnlyList<GameBuildInputFile> Files { get; }
    /// <summary>Total bytes copied for the compiler invocation.</summary>
    public long TotalBytes => Files.Sum(f => f.Length);
}
