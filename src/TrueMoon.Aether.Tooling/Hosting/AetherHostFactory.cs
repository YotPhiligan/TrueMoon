using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Preview;

namespace TrueMoon.Aether.Tooling.Hosting;

// Factory results are owned only by the returned host, never by disposable DI aliases.
internal sealed class AetherHostFactory(
    IFactory<RuntimeEngine> runtimes,
    IFactory<GameBuildCompiler> builds,
    IFactory<GameEditor, (SessionSupervisor Supervisor, FrameSession Frames, GameBuildCompiler Builds, GameReplacement Replacement)> editors,
    IFactory<ScenePreview, FrameSession> previews)
    : IFactory<AetherHost>
{
    public AetherHost Create()
    {
        var acquired = new List<IAsyncDisposable>();
        RuntimeEngine? runtime = null;
        try
        {
            runtime = runtimes.Create() ?? throw new InvalidOperationException("RuntimeEngine factory returned null.");
            var supervisor = runtime.Supervisor; var frame = runtime.Frames; var replacement = runtime.Replacement;
            var preview = Own(previews.Create(frame)); var compiler = Own(builds.Create()!);
            var editor = editors.Create((supervisor, frame, compiler, replacement));
            ArgumentNullException.ThrowIfNull(editor);
            return new(runtime, compiler, editor, preview);
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            foreach (var value in Enumerable.Reverse(acquired))
                try { value.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            try { runtime?.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception cleanup) { errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException("Aether creation and cleanup failed.", errors);
            ExceptionDispatchInfo.Capture(original).Throw(); throw;
        }
        T Own<T>(T value) where T : IAsyncDisposable
        { ArgumentNullException.ThrowIfNull(value); acquired.Add(value); return value; }
    }
    public AetherHost Create<TData>(TData? data = default) => data is null ? Create() : throw new ArgumentException("AetherHost does not accept creation arguments.", nameof(data));
}
