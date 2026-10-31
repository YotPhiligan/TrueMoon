using System.Globalization;
using System.Text;

namespace TrueMoon.Aether.Builds;

/// <summary>Compiler-owned flat diagnostics archives. Does not own artifacts or abandoned staging directories.</summary>
internal sealed class BuildDiagnosticStore
{
    internal const string MarkerFile = ".build-diagnostics";
    private const string Magic = "TrueMoon.Aether.BuildDiagnostics/1\n";
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
        { MarkerFile, "build.stdout.log", "build.stderr.log", "build.output.json", GameBuildDiagnosticReader.FileName,
            "prepare.json", "prepare.stdout.log", "prepare.stderr.log", "capture.json" };
    static BuildDiagnosticStore()
    {
        foreach (var phase in new[] { "before", "after", "verify" })
            foreach (var suffix in new[] { ".project.xml", ".evaluation.json", ".inputs.json", "-query.stdout.log", "-query.stderr.log", "-project.stdout.log", "-project.stderr.log" })
                Names.Add(phase + suffix);
    }
    private readonly string _jobs;
    private readonly GameBuildRetentionOptions _options;

    internal BuildDiagnosticStore(string artifactRoot, GameBuildRetentionOptions options)
    {
        _jobs = Path.Combine(Path.GetFullPath(artifactRoot), "jobs");
        _options = options.Snapshot();
    }

    internal Job Begin()
    {
        RejectLinks(_jobs);
        Directory.CreateDirectory(_jobs);
        RejectLinks(_jobs);
        var directory = Path.Combine(_jobs, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        RejectLinks(directory);
        FileStream? lease = null;
        try
        {
            lease = new FileStream(Path.Combine(directory, MarkerFile), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            lease.Write(Encoding.ASCII.GetBytes(Magic + "active"));
            return new(directory, lease);
        }
        catch { lease?.Dispose(); throw; }
    }

    // Called on the compiler's asynchronous work path, never by a diagnostic event producer.
    internal GameBuildRetentionStatus Sweep(DateTimeOffset nowUtc, string? protectedJob = null)
    {
        var jobs = new List<CompletedJob>();
        var active = 0; var skipped = 0; var deleted = 0; var failures = 0; string? lastError = null;
        var busy = false;
        try
        {
            RejectLinks(_jobs);
            if (!Directory.Exists(_jobs)) return Status();
            var lockPath = Path.Combine(_jobs, ".retention.lock"); RejectLinks(lockPath);
            FileStream rootLease;
            try { rootLease = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) when (IsSharingViolation(error)) { busy = true; return Status(); }
            using (rootLease)
            {
                foreach (var directory in Directory.EnumerateDirectories(_jobs))
                {
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) { skipped++; continue; }
                    try
                    {
                        RejectLinks(directory);
                        var marker = Path.Combine(directory, MarkerFile);
                        if (!File.Exists(marker)) { skipped++; continue; }
                        RejectLinks(marker);
                        using var lease = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        if (!TryReadCompletion(lease, out var completed)) { skipped++; continue; }
                        var files = OwnedFiles(directory);
                        if (files is null) { skipped++; continue; }
                        jobs.Add(new(directory, completed, files.Sum(path => new FileInfo(path).Length)));
                    }
                    catch (IOException error) when (IsSharingViolation(error)) { active++; }
                    catch (Exception error) when (IsFileError(error)) { skipped++; Fail(error); }
                }

                // Keep the newest completed job even after its age/byte limit; report any unsatisfied limit.
                var newest = jobs.OrderByDescending(job => job.CompletedUtc).ThenByDescending(job => job.Directory, StringComparer.Ordinal).FirstOrDefault();
                var count = jobs.Count; var bytes = jobs.Sum(job => job.Bytes);
                foreach (var job in jobs.OrderBy(job => job.CompletedUtc).ThenBy(job => job.Directory, StringComparer.Ordinal).ToArray())
                {
                    if (job == newest || protectedJob != null && SamePath(job.Directory, protectedJob)) continue;
                    if (!Expired(job) && count <= _options.MaxJobs && bytes <= _options.MaxTotalBytes) continue;
                    try
                    {
                        Delete(job);
                        jobs.Remove(job); count--; bytes -= job.Bytes; deleted++;
                    }
                    catch (Exception error) when (IsFileError(error)) { Fail(error); }
                }
            }
        }
        catch (Exception error) when (IsFileError(error)) { Fail(error); }
        return Status();

        bool Expired(CompletedJob job) => nowUtc > job.CompletedUtc && nowUtc - job.CompletedUtc > _options.MaxAge;
        void Fail(Exception error)
        {
            failures++;
            var summary = error.GetType().Name + ": " + error.Message;
            lastError = summary.Length <= 1024 ? summary : summary[..1024];
        }
        GameBuildRetentionStatus Status()
        {
            var bytes = jobs.Sum(job => job.Bytes);
            return new(jobs.Count, bytes, active, skipped, deleted, failures, lastError,
                jobs.Count > _options.MaxJobs || bytes > _options.MaxTotalBytes || jobs.Any(Expired), busy);
        }
    }

    private void Delete(CompletedJob job)
    {
        var resolved = Path.GetFullPath(job.Directory);
        if (!SamePath(Path.GetDirectoryName(resolved)!, _jobs) || !Guid.TryParseExact(Path.GetFileName(resolved), "N", out _))
            throw new IOException("Diagnostics cleanup escaped its owned jobs root.");
        RejectLinks(resolved);
        var marker = Path.Combine(resolved, MarkerFile); RejectLinks(marker);
        using (var lease = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            if (!TryReadCompletion(lease, out var completed) || completed != job.CompletedUtc)
                throw new IOException("Diagnostics job ownership changed during maintenance.");
            var files = OwnedFiles(resolved) ?? throw new IOException("Diagnostics job contains unowned files.");
            foreach (var file in files)
                if (Path.GetFileName(file) != MarkerFile) File.Delete(file);
        }
        // The root lease fences other cooperating cleaners. New jobs always have a fresh GUID.
        File.Delete(marker);
        Directory.Delete(resolved); // Deliberately non-recursive: unexpected files or subdirectories remain protected.
    }

    private static string[]? OwnedFiles(string directory)
    {
        var files = Directory.EnumerateFileSystemEntries(directory).ToArray();
        foreach (var path in files)
        {
            if (!Names.Contains(Path.GetFileName(path))) return null;
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return null;
        }
        return files;
    }

    private static bool TryReadCompletion(FileStream stream, out DateTimeOffset completed)
    {
        completed = default;
        if (stream.Length is < 1 or > 128) return false;
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var text = Encoding.ASCII.GetString(bytes);
        return text.StartsWith(Magic, StringComparison.Ordinal)
            && DateTimeOffset.TryParseExact(text[Magic.Length..], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out completed);
    }

    private static bool SamePath(string first, string second)
        => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void RejectLinks(string path)
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

    internal sealed class Job(string directory, FileStream lease) : IDisposable
    {
        private FileStream? _lease = lease;
        internal string Directory { get; } = directory;
        internal void Complete(DateTimeOffset completedUtc)
        {
            var stream = _lease ?? throw new ObjectDisposedException(nameof(Job));
            try
            {
                stream.Position = 0; stream.SetLength(0);
                stream.Write(Encoding.ASCII.GetBytes(Magic + completedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
                stream.Flush();
            }
            finally { Dispose(); }
        }
        public void Dispose() { _lease?.Dispose(); _lease = null; }
    }

    private sealed record CompletedJob(string Directory, DateTimeOffset CompletedUtc, long Bytes);
}
