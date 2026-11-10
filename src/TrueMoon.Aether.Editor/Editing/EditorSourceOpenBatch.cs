using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Builds;

namespace TrueMoon.Aether.Editor.Editing;

// One attempt owns at most four staged readers. All native opens finish before
// hand-off; Take transfers one reader, Dispose closes the unconsumed remainder.
internal sealed class EditorSourceOpenBatch : IDisposable
{
    internal const int Capacity = 4;
    internal const int MinimumFiles = 256;
    private readonly Slot[] _slots;
    private readonly CancellationToken _token;
    private readonly Func<string, EditorSourceFile?>? _open;
    private int _count;
    private bool _disposed;
    private EditorSourceOpenBatch(int capacity, CancellationToken token, Func<string, EditorSourceFile?>? open)
    { _slots = new Slot[capacity]; _token = token; _open = open; }
    internal int Count => _count;

    internal static async Task<EditorSourceOpenBatch> CreateAsync(IReadOnlyList<GameBuildInputFile> files, int offset,
        Action<string> validate, CancellationToken token, Func<string, EditorSourceFile?>? open = null)
    {
        ArgumentNullException.ThrowIfNull(files); ArgumentNullException.ThrowIfNull(validate);
        if (offset < 0 || offset > files.Count) throw new ArgumentOutOfRangeException(nameof(offset));
        var count = Math.Min(Capacity, files.Count - offset);
        var batch = new EditorSourceOpenBatch(count, token, open);
        var tasks = new PendingTasks(); var started = 0;
        for (var index = 0; index < count; index++)
        {
            batch._count = index + 1;
            string path;
            try
            {
                token.ThrowIfCancellationRequested();
                path = files[offset + index].Path;
                // The coordinator alone mutates the directory cache. Native workers only open.
                validate(path);
            }
            catch (Exception error)
            {
                batch._slots[index].Error = ExceptionDispatchInfo.Capture(error);
                break;
            }
            // Keep Task.Run's scheduler, execution-context flow and completion behavior,
            // but use static state and nongeneric workers writing one indexed slot each.
            // No scheduler cancellation: every started job must report its owner/error for drain.
            try
            {
                tasks[started] = Task.Factory.StartNew(static state =>
                {
                    var work = (Work)state!; work.Batch.Open(work.Index, work.Path);
                }, new Work(batch, index, path), CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                started++;
            }
            catch (Exception error)
            {
                batch._slots[index].Error = ExceptionDispatchInfo.Capture(error);
                break;
            }
        }
        await Task.WhenAll(tasks[..started]).ConfigureAwait(false);
        return batch;
    }

    private void Open(int index, string path)
    {
        EditorSourceFile? file = null;
        try
        {
            _token.ThrowIfCancellationRequested();
            file = _open != null ? _open(path) : EditorSourceFile.Open(path);
            _token.ThrowIfCancellationRequested();
            _slots[index].File = file;
        }
        catch (Exception error)
        {
            try { file?.Dispose(); }
            catch (Exception cleanup) { _slots[index].File = file; error = new AggregateException(error, cleanup); }
            _slots[index].Error = ExceptionDispatchInfo.Capture(error);
        }
    }

    internal EditorSourceFile? Take(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        ref var slot = ref _slots[index];
        if (slot.Taken) throw new InvalidOperationException("Source reader ownership was already transferred.");
        slot.Error?.Throw();
        var file = slot.File; slot.File = null; slot.Taken = true;
        return file;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (ref var slot in _slots.AsSpan()) { slot.File?.Dispose(); slot.File = null; }
    }
    private struct Slot { internal EditorSourceFile? File; internal ExceptionDispatchInfo? Error; internal bool Taken; }
    private sealed record Work(EditorSourceOpenBatch Batch, int Index, string Path);
    [System.Runtime.CompilerServices.InlineArray(Capacity)]
    private struct PendingTasks { private Task _first; }
}
