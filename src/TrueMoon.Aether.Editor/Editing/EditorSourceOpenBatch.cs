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
    private bool _disposed;
    private EditorSourceOpenBatch(Slot[] slots) => _slots = slots;
    internal int Count => _slots.Length;

    internal static async Task<EditorSourceOpenBatch> CreateAsync(IReadOnlyList<GameBuildInputFile> files, int offset,
        Action<string> validate, CancellationToken token, Func<string, EditorSourceFile?>? open = null)
    {
        ArgumentNullException.ThrowIfNull(files); ArgumentNullException.ThrowIfNull(validate);
        if (offset < 0 || offset > files.Count) throw new ArgumentOutOfRangeException(nameof(offset));
        var count = Math.Min(Capacity, files.Count - offset);
        var tasks = new List<Task<Slot>>(count);
        for (var index = 0; index < count; index++)
        {
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
                tasks.Add(Task.FromResult(new Slot { Error = ExceptionDispatchInfo.Capture(error) }));
                break;
            }
            // No scheduler cancellation: every started job must return its owner/error for drain.
            tasks.Add(Task.Run(() => Open(path, token, open)));
        }
        return new(await Task.WhenAll(tasks).ConfigureAwait(false));
    }

    private static Slot Open(string path, CancellationToken token, Func<string, EditorSourceFile?>? open)
    {
        EditorSourceFile? file = null;
        try
        {
            token.ThrowIfCancellationRequested();
            file = open != null ? open(path) : EditorSourceFile.Open(path);
            token.ThrowIfCancellationRequested();
            return new() { File = file };
        }
        catch (Exception error)
        {
            file?.Dispose();
            return new() { Error = ExceptionDispatchInfo.Capture(error) };
        }
    }

    internal EditorSourceFile? Take(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)index >= (uint)_slots.Length) throw new ArgumentOutOfRangeException(nameof(index));
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
}
