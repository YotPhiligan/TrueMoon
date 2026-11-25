using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Sessions;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril;

namespace TrueMoon.Aether.Runtime;

internal sealed class RuntimeHostFactory(RuntimeEngine engine, IAppLifetime lifetime, int parentId,
    IFactory<UnitParentProcessEventsHandler> parents, IFactory<GameBuildLoader, GameBuildReference> games,
    IFactory<FrameProducer> frames, IFactory<RuntimeProtocol, (SessionLaunch Launch, IGameSession Game, FrameProducer Frames)> protocols,
    IFactory<PipesInvocationServer<IEngineControl>, (SessionLaunch Launch, RuntimeProtocol Protocol)> servers,
    IFactory<PreviewGameSession>? previews = null, IFactory<ChildDiagnosticForwarder, SessionLaunch>? diagnostics = null, RuntimeStartupSettings? settings = null, RuntimeMeshRenderHost? meshes = null)
    : IFactory<AetherRuntimeHost, SessionLaunch>
{
    public AetherRuntimeHost Create(SessionLaunch arguments)
    {
        var acquired = new List<object>();
        try
        {
            var parent = Own(parents.Create()!);
            IDisposable game; IGameSession session;
            if (arguments.Role == RuntimeRole.EditPreview)
            {
                var preview = Own(previews?.Create() ?? throw new InvalidOperationException("Missing preview session factory."));
                game = preview; session = preview;
            }
            else
            {
                var loaded = Own(games.Create(arguments.Build!)); game = loaded; session = loaded.Session;
            }
            var frame = Own(frames.Create()!);
            var protocol = Own(protocols.Create((arguments, session, frame)));
            var server = Own(servers.Create((arguments, protocol)));
            var delivery = diagnostics == null ? null : Own(diagnostics.Create(arguments));
            return new(engine, lifetime, parent, game, frame, protocol, server, arguments, parentId, delivery, settings, meshes);
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            foreach (var value in Enumerable.Reverse(acquired))
                try
                {
                    if (value is IAsyncDisposable asyncDisposable) asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    else if (value is IDisposable disposable) disposable.Dispose();
                }
                catch (Exception cleanup) { errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException(original.Message, errors);
            ExceptionDispatchInfo.Capture(original).Throw(); throw;
        }
        T Own<T>(T value) { ArgumentNullException.ThrowIfNull(value); acquired.Add(value); return value; }
    }
}
