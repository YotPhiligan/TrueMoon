using TrueMoon.Aether.Runtime.Ecs.Runtime;

namespace TrueMoon.Aether.Runtime;

/// <summary>Opt-in session subsystem preparation without activating backends, a World or an audio device.</summary>
public static class SceneSubsystemConfigurationExtensions
{
    /// <summary>Registers one frozen selection of subsystem factories. Their results belong to each game session.</summary>
    /// <param name="context">App composition root.</param><param name="configure">Subsystem selection; register each typed factory through DI.</param>
    /// <returns>The same configuration context.</returns>
    public static IAppConfigurationContext UseAetherSceneSubsystems(this IAppConfigurationContext context, Action<SceneSubsystemsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(configure);
        var builder = new SceneSubsystemsBuilder(); configure(builder); var registrations = builder.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<IFactory<SceneSubsystemSession, SceneSubsystemArguments>>()) throw new InvalidOperationException("Scene subsystems are already registered.");
            services.Singleton<IFactory<SceneSubsystemSession, SceneSubsystemArguments>>(resolver =>
            {
                var factories = registrations.Select(resolve => resolve(resolver)).ToArray();
                return new Factory<SceneSubsystemSession, SceneSubsystemArguments>(arguments => SceneSubsystemSession.Create(arguments, factories));
            });
        });
        return context;
    }
}
