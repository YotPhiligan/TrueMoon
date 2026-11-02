using TrueMoon.Aether.Builds;

namespace TrueMoon.Aether.Editor.Editing;

internal interface IEditorSourceHints : IDisposable
{
    int Count { get; }
    string? Warning { get; }
    void Stop() { }
}

// Owned native hints only. Callbacks mark a version/wakeup; the worker supplies all file coverage.
internal sealed class EditorSourceHints : IEditorSourceHints
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _gate = new();
    public int Count { get { lock (_gate) return _watchers.Count(watcher => watcher.EnableRaisingEvents); } }
    public string? Warning { get; private set; }

    internal EditorSourceHints(GameBuildSourceWatchDescriptor descriptor, EditorSourceWatchOptions options, Action<bool> signal)
    {
        if (!options.UseFileSystemNotifications || options.MaximumWatchers == 0) return;
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tree in descriptor.SourceTrees) Add(tree, "*", true);
        foreach (var path in descriptor.Files.Select(file => file.Path).Concat(descriptor.Probes.Select(probe => probe.Path)))
        {
            if (descriptor.TreeFiles.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;
            var parent = Path.GetDirectoryName(path)!;
            if (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(parent)) == null) continue;
            // Ancestor/root probes remain exact polling, without broad drive/root subscriptions.
            if (descriptor.SourceTrees.Any(tree => Inside(tree, parent) || StringComparer.OrdinalIgnoreCase.Equals(tree, parent))) continue;
            Add(parent, Path.GetFileName(path), false);
        }

        void Add(string directory, string filter, bool recursive)
        {
            if (!targets.Add(directory + "|" + filter + "|" + recursive)) return;
            if (_watchers.Count >= options.MaximumWatchers) { Warning = "Notification handle limit; complete polling covers remaining targets."; return; }
            FileSystemWatcher? watcher = null;
            try
            {
                if (!Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\", StringComparison.Ordinal) ||
                    Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory)) == null) throw new NotSupportedException();
                watcher = new(directory, filter) { IncludeSubdirectories = recursive, InternalBufferSize = 8192,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Security };
                void Changed(object sender, FileSystemEventArgs args)
                {
                    if (recursive && Excluded(args.FullPath)) return;
                    signal(false);
                }
                void Renamed(object sender, RenamedEventArgs args)
                { if (!recursive || !Excluded(args.FullPath) || !Excluded(args.OldFullPath)) signal(false); }
                watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Renamed;
                watcher.Error += (_, _) => signal(true);
                watcher.EnableRaisingEvents = true; _watchers.Add(watcher);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                try { watcher?.Dispose(); }
                catch { if (watcher != null) _watchers.Add(watcher); /* Keep failed partial cleanup owned for Close retry. */ }
                Warning = "Notifications unavailable; complete polling remains active.";
            }
        }

        bool Excluded(string path)
        {
            // No filesystem reads in callbacks. Check both entry kinds, and each lexical directory on descent.
            if (descriptor.Policy.ExcludesEntry(path, false)) return true;
            for (var current = Path.GetDirectoryName(path); current != null; current = Path.GetDirectoryName(current))
            {
                if (descriptor.SourceTrees.Contains(current, StringComparer.OrdinalIgnoreCase)) break;
                if (descriptor.Policy.ExcludesEntry(current, true)) return true;
            }
            // An excluded directory's own event may cause one harmless scan; file writes under it do not loop.
            return false;
        }
    }

    private static bool Inside(string path, string directory) => path.StartsWith(
        Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public void Stop()
    {
        List<Exception>? errors = null;
        lock (_gate)
            foreach (var watcher in _watchers)
                try { watcher.EnableRaisingEvents = false; } catch (Exception error) { (errors ??= []).Add(error); }
        if (errors != null) throw new AggregateException(errors);
    }
    public void Dispose()
    {
        List<Exception>? errors = null;
        lock (_gate)
        foreach (var watcher in _watchers.ToArray())
        {
            try { watcher.Dispose(); _watchers.Remove(watcher); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors != null) throw new AggregateException(errors);
    }
}
