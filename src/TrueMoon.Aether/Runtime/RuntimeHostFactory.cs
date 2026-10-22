using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril;

namespace TrueMoon.Aether.Runtime;

internal sealed class RuntimeHostFactory(AetherHost engine, IAppLifetime lifetime, int parentId,
    IFactory<UnitParentProcessEventsHandler> parents, IFactory<GameBuildLoader, GameBuildReference> games,
    IFactory<FrameProducer> frames, IFactory<RuntimeProtocol, (SessionLaunch Launch, IGameSession Game, FrameProducer Frames)> protocols,
    IFactory<PipesInvocationServer<IEngineControl>, (SessionLaunch Launch, RuntimeProtocol Protocol)> servers)
    : IFactory<AetherRuntimeHost, SessionLaunch>
{
    public AetherRuntimeHost Create(SessionLaunch arguments)
    {
        var acquired = new List<object>();
        try
        {
            var parent = Own(parents.Create()!);
            var game = Own(games.Create(arguments.Build!));
            var frame = Own(frames.Create()!);
            var protocol = protocols.Create((arguments, game.Session, frame));
            var server = Own(servers.Create((arguments, protocol)));
            return new(engine, lifetime, parent, game, frame, protocol, server, arguments, parentId);
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
