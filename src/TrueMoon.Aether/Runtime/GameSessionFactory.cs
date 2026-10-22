using System.Reflection;
using TrueMoon.Aether.Sessions;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime;

// The dynamic assembly supplies explicit factory code; no constructor reflection fallback.
internal sealed class GameSessionFactory(IServiceResolver? services) : IFactory<IGameSession, Type>
{
    public IGameSession Create(Type arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!arguments.IsVisible || arguments.IsAbstract || !typeof(IGameSession).IsAssignableFrom(arguments) ||
            !typeof(IGameSessionEntry).IsAssignableFrom(arguments))
            throw new InvalidDataException("Game entry must implement IGameSession and IGameSessionEntry.");
        var dispatch = typeof(GameSessionFactory).GetMethod(nameof(CreateEntry), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(arguments).CreateDelegate<Func<IServiceResolver?, IGameSession>>();
        return dispatch(services) ?? throw new InvalidDataException("Game entry factory returned null.");
    }
    private static IGameSession CreateEntry<T>(IServiceResolver? services) where T : IGameSessionEntry => T.Create(services);
}
