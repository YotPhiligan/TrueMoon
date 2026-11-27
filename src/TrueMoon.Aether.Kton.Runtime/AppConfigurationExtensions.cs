using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Configuration;
using TrueMoon.Kton;

namespace TrueMoon.Aether.Kton.Runtime;

/// <summary>Optional App composition for provider-neutral Kton scene integration.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Registers lazy session-owned physics factories after the selected provider. Creates no world at Build/Start.</summary>
    /// <param name="context">App composition root.</param><param name="configure">Copied bridge limits and collision assets.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherKton(this IAppConfigurationContext context, Action<PhysicsBridgeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new PhysicsBridgeOptions();
        configure?.Invoke(options);
        Register(context, options);
        return context;
    }
    private static void Register(IAppConfigurationContext context, PhysicsBridgeOptions options)
    {
        var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (!services.Exist<IFactory<IPhysicsWorld>>()) throw new InvalidOperationException("Register a Kton provider before UseAetherKton.");
            if (services.Exist<IFactory<PhysicsSceneSubsystem, SceneSubsystemArguments>>()) throw new InvalidOperationException("Aether Kton is already registered.");
            services.Singleton<IFactory<PhysicsBridgeSystem, PhysicsSceneSubsystem>>(_ =>
                new Factory<PhysicsBridgeSystem, PhysicsSceneSubsystem>(owner => new(owner)));
            services.Singleton<IFactory<PhysicsSceneSubsystem, SceneSubsystemArguments>>(resolver => new PhysicsSceneSubsystemFactory(
                resolver.Resolve<IFactory<IPhysicsWorld>>()!, resolver.Resolve<IFactory<PhysicsBridgeSystem, PhysicsSceneSubsystem>>()!, snapshot));
        });
    }
    /// <summary>Binds the exact common schema then registers the same lazy factories.</summary>
    /// <param name="context">App with the common configuration stage.</param><param name="configure">Explicit startup overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherKton(this IAppConfigurationContext context, Action<PhysicsBridgeOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, PhysicsBridgeConfigurationSchema.Default, Register, configure);
    /// <summary>Selects Kton in the single shared scene-subsystem composition. Register UseAetherKton in the same App.</summary>
    /// <param name="builder">Shared subsystem builder.</param><returns>The same builder.</returns>
    public static SceneSubsystemsBuilder AddKtonPhysics(this SceneSubsystemsBuilder builder)
    { ArgumentNullException.ThrowIfNull(builder); return builder.Add<PhysicsSceneSubsystem>("kton.physics"); }
}
