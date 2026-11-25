using TrueMoon.Aether.Mods;

namespace TrueMoon.Aether.Runtime.Mods;

// No back-reference to ModGroup. Retained Guard delegates remain default-ALC objects after Retire.
internal sealed class ModGeneration : IModGeneration
{
    private readonly object _sync = new();
    private readonly List<Slot> _slots = [];
    private bool _active, _retired, _stopping;
    private int _running;
    private TaskCompletionSource? _drained;
    public bool IsActive { get { lock (_sync) return _active; } }
    public Action Guard(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_sync)
        {
            if (_retired || _stopping || _slots.Count == 256) throw new InvalidOperationException("Generation callbacks are retired or their budget is exhausted.");
            var slot = new Slot(this, callback); _slots.Add(slot); return slot.Invoke;
        }
    }
    internal void Open() { lock (_sync) { if (_retired) throw new InvalidOperationException("Generation is retired."); _active = !_stopping; } }
    internal void Close() { lock (_sync) { _stopping = true; _active = false; } }
    internal Task QuiesceAsync(CancellationToken token)
    {
        lock (_sync)
        {
            _active = false;
            if (_running == 0) return Task.CompletedTask;
            _drained ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _drained.Task.WaitAsync(token);
        }
    }
    internal void Retire()
    {
        lock (_sync)
        {
            if (_running != 0) throw new InvalidOperationException("Drain callbacks before retirement.");
            _active = false; _retired = true;
            foreach (var slot in _slots) slot.Callback = null;
            _slots.Clear();
        }
    }
    private sealed class Slot(ModGeneration owner, Action callback)
    {
        internal Action? Callback = callback;
        internal void Invoke()
        {
            Action action;
            lock (owner._sync)
            {
                if (!owner._active || Callback == null) return;
                action = Callback; owner._running++;
            }
            try { action(); }
            catch { throw new ModOperationException(new(ModLoadError.ActivationFailed, "Generation callback failed.")); }
            finally
            {
                lock (owner._sync)
                {
                    if (--owner._running == 0) { owner._drained?.TrySetResult(); owner._drained = null; }
                }
            }
        }
    }
}
