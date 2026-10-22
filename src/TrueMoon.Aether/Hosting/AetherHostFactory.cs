using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Hosting;

// Factory results are owned only by the returned host, never by disposable DI aliases.
internal sealed class AetherHostFactory(
    IFactory<SessionSupervisor> supervisors,
    IFactory<GpuResourceHost> graphics,
    IFactory<FrameSession, (SessionSupervisor Supervisor, GpuResourceHost Gpu)> frames,
    IFactory<GameBuildCompiler> builds,
    IFactory<GameReplacement, (SessionSupervisor Supervisor, FrameSession Frames)> replacements,
    IFactory<GameEditor, (SessionSupervisor Supervisor, FrameSession Frames, GameBuildCompiler Builds, GameReplacement Replacement)> editors)
    : IFactory<AetherHost>
{
    public AetherHost Create()
    {
        var acquired = new List<IAsyncDisposable>();
        try
        {
            var supervisor = Own(supervisors.Create()!);
            var gpu = Own(graphics.Create()!);
            var frame = Own(frames.Create((supervisor, gpu)));
            var compiler = Own(builds.Create()!);
            var replacement = replacements.Create((supervisor, frame));
            ArgumentNullException.ThrowIfNull(replacement);
            var editor = editors.Create((supervisor, frame, compiler, replacement));
            ArgumentNullException.ThrowIfNull(editor);
            return new(supervisor, frame, compiler, replacement, editor, gpu);
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            foreach (var value in Enumerable.Reverse(acquired))
                try { value.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException("Aether creation and cleanup failed.", errors);
            ExceptionDispatchInfo.Capture(original).Throw(); throw;
        }
        T Own<T>(T value) where T : IAsyncDisposable
        { ArgumentNullException.ThrowIfNull(value); acquired.Add(value); return value; }
    }
    public AetherHost Create<TData>(TData? data = default) => data is null ? Create() : throw new ArgumentException("AetherHost does not accept creation arguments.", nameof(data));
}
