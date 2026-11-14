using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Runtime.Hosting;

internal sealed class RuntimeEngineFactory(IFactory<SessionSupervisor> supervisors, IFactory<GpuResourceHost> graphics,
    IFactory<FrameSession, (SessionSupervisor Supervisor, GpuResourceHost Gpu)> frames,
    IFactory<GameReplacement, (SessionSupervisor Supervisor, FrameSession Frames)> replacements) : IFactory<RuntimeEngine>
{
    public RuntimeEngine Create()
    {
        List<IAsyncDisposable> acquired = [];
        try
        {
            var supervisor = Own(supervisors.Create()!); var gpu = Own(graphics.Create()!);
            var frame = Own(frames.Create((supervisor, gpu)));
            var replacement = replacements.Create((supervisor, frame)); ArgumentNullException.ThrowIfNull(replacement);
            return new(supervisor, frame, replacement, gpu);
        }
        catch (Exception original)
        {
            List<Exception> errors = [original];
            foreach (var value in Enumerable.Reverse(acquired))
                try { value.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 1) throw new AggregateException("Runtime creation and cleanup failed.", errors);
            ExceptionDispatchInfo.Capture(original).Throw(); throw;
        }
        T Own<T>(T value) where T : IAsyncDisposable { ArgumentNullException.ThrowIfNull(value); acquired.Add(value); return value; }
    }
    public RuntimeEngine Create<TData>(TData? data = default) => data is null ? Create() : throw new ArgumentException("RuntimeEngine does not accept arguments.", nameof(data));
}
