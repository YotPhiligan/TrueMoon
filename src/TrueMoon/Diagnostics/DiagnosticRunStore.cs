using System.Globalization;
using System.Text;

namespace TrueMoon.Diagnostics;

/// <summary>Owns only recognized flat JSONL run archives. Active marker leases outlive all file IO.</summary>
internal sealed class DiagnosticRunStore
{
    internal const string MarkerFile = ".diagnostic-run";
    private const string Magic = "TrueMoon.Diagnostics.Run/1\n";
    private readonly string _root;
    private readonly DiagnosticRunRetentionOptions _options;

    internal DiagnosticRunStore(string root, DiagnosticRunRetentionOptions options)
    {
        _root = Path.GetFullPath(root);
        _options = options.Snapshot();
    }

    internal Run Create(DateTimeOffset nowUtc)
    {
        RejectLinks(_root);
        Directory.CreateDirectory(_root);
        RejectLinks(_root);
        var directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        RejectLinks(directory);
        FileStream? lease = null;
        try
        {
            lease = new FileStream(Path.Combine(directory, MarkerFile), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            lease.Write(Encoding.ASCII.GetBytes(Magic + nowUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
            lease.Flush();
            return new(directory, lease);
        }
        catch { lease?.Dispose(); throw; }
    }

    // Called by the JSON sink worker, never by a diagnostic event producer.
    internal DiagnosticRetentionStatus Sweep(DateTimeOffset nowUtc, string? protectedDirectory = null)
    {
        var runs = new List<ClosedRun>();
        var active = 0; var skipped = 0; var deleted = 0; var failures = 0; string? lastError = null;
        var busy = false;
        try
        {
            RejectLinks(_root);
            if (!Directory.Exists(_root)) return Status();
            var lockPath = Path.Combine(_root, ".retention.lock"); RejectLinks(lockPath);
            FileStream rootLease;
            try { rootLease = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) when (IsSharingViolation(error)) { busy = true; return Status(); }
            using (rootLease)
            {
                foreach (var directory in Directory.EnumerateDirectories(_root))
                {
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) { skipped++; continue; }
                    try
                    {
                        RejectLinks(directory);
                        var marker = Path.Combine(directory, MarkerFile);
                        if (!File.Exists(marker)) { skipped++; continue; }
                        RejectLinks(marker);
                        using var lease = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        if (!TryReadCreation(lease, out var created)) { skipped++; continue; }
                        var files = OwnedFiles(directory);
                        if (files is null) { skipped++; continue; }
                        runs.Add(new(directory, created, files.Sum(path => new FileInfo(path).Length)));
                    }
                    catch (IOException error) when (IsSharingViolation(error)) { active++; }
                    catch (Exception error) when (IsFileError(error)) { skipped++; Fail(error); }
                }

                // Keep the newest closed run even after its age/byte limit; report any unsatisfied limit.
                var newest = runs.OrderByDescending(run => run.CreatedUtc).ThenByDescending(run => run.Directory, StringComparer.Ordinal).FirstOrDefault();
                var count = runs.Count; var bytes = runs.Sum(run => run.Bytes);
                foreach (var run in runs.OrderBy(run => run.CreatedUtc).ThenBy(run => run.Directory, StringComparer.Ordinal).ToArray())
                {
                    if (run == newest || protectedDirectory != null && SamePath(run.Directory, protectedDirectory)) continue;
                    if (!Expired(run) && count <= _options.MaxRuns && bytes <= _options.MaxTotalBytes) continue;
                    try
                    {
                        Delete(run);
                        runs.Remove(run); count--; bytes -= run.Bytes; deleted++;
                    }
                    catch (Exception error) when (IsFileError(error)) { Fail(error); }
                }
            }
        }
        catch (Exception error) when (IsFileError(error)) { Fail(error); }
        return Status();

        bool Expired(ClosedRun run) => nowUtc > run.CreatedUtc && nowUtc - run.CreatedUtc > _options.MaxAge;
        void Fail(Exception error)
        {
            failures++;
            var summary = error.GetType().Name + ": " + error.Message;
            lastError = summary.Length <= 1024 ? summary : summary[..1024];
        }
        DiagnosticRetentionStatus Status()
        {
            var bytes = runs.Sum(run => run.Bytes);
            return new(runs.Count, bytes, active, skipped, deleted, failures, lastError,
                runs.Count > _options.MaxRuns || bytes > _options.MaxTotalBytes || runs.Any(Expired), busy);
        }
    }

    private void Delete(ClosedRun run)
    {
        var resolved = Path.GetFullPath(run.Directory);
        if (!SamePath(Path.GetDirectoryName(resolved)!, _root) || !Guid.TryParseExact(Path.GetFileName(resolved), "N", out _))
            throw new IOException("Diagnostics cleanup escaped its owned run root.");
        RejectLinks(resolved);
        var marker = Path.Combine(resolved, MarkerFile); RejectLinks(marker);
        using (var lease = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            if (!TryReadCreation(lease, out var created) || created != run.CreatedUtc)
                throw new IOException("Diagnostics run ownership changed during maintenance.");
            var files = OwnedFiles(resolved) ?? throw new IOException("Diagnostic run contains unowned files.");
            foreach (var file in files)
                if (Path.GetFileName(file) != MarkerFile) File.Delete(file);
        }
        // The root lease fences other cooperating cleaners. New runs always have a fresh GUID.
        File.Delete(marker);
        Directory.Delete(resolved); // Deliberately non-recursive: unexpected files or subdirectories remain protected.
    }

    private static string[]? OwnedFiles(string directory)
    {
        var files = Directory.EnumerateFileSystemEntries(directory).ToArray();
        foreach (var path in files)
        {
            var name = Path.GetFileName(path);
            if (name != MarkerFile && name != "events.lock" && !IsEventFile(name)) return null;
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return null;
        }
        return files;
    }

    internal static bool TryReadCreation(FileStream stream, out DateTimeOffset created)
    {
        created = default;
        if (stream.Length is < 1 or > 128) return false;
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var text = Encoding.ASCII.GetString(bytes);
        return text.StartsWith(Magic, StringComparison.Ordinal)
            && DateTimeOffset.TryParseExact(text[Magic.Length..], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out created);
    }

    private static bool SamePath(string first, string second)
        => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    internal static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Diagnostics archive links are unsupported.");
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { }
        }
    }

    private static bool IsSharingViolation(IOException error) => (error.HResult & 0xffff) is 32 or 33;
    private static bool IsFileError(Exception error) => error is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    internal static bool IsEventFile(string name)
    {
        if (!name.StartsWith("events-", StringComparison.Ordinal) || !name.EndsWith(".jsonl", StringComparison.Ordinal)) return false;
        var suffix = name[7..^6];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
            && suffix == slot.ToString(CultureInfo.InvariantCulture);
    }

    internal sealed class Run(string directory, FileStream lease) : IDisposable
    {
        private FileStream? _lease = lease;
        internal string Directory { get; } = directory;
        public void Dispose() { _lease?.Dispose(); _lease = null; }
    }

    private sealed record ClosedRun(string Directory, DateTimeOffset CreatedUtc, long Bytes);
}
