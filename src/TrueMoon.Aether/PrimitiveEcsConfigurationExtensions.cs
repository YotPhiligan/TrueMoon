using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Services;

namespace TrueMoon.Aether;

/// <summary>Registers lazy borrowed-World factories for the primitive ECS profile.</summary>
public static class PrimitiveEcsConfigurationExtensions
{
    /// <summary>Configures one App-local primitive profile. Startup creates no scene, systems or native resources.</summary>
    /// <param name="context">App configuration.</param><param name="configure">Optional copied limits/camera/fixed step.</param><returns>The same configuration.</returns>
    /// <remarks>Factory results are caller-owned; dispose controllers before their borrowed scene. Existing individual system factories are preserved.</remarks>
    public static IAppConfigurationContext UseAetherPrimitiveEcs(this IAppConfigurationContext context, Action<PrimitiveSimulationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new PrimitiveSimulationOptions(); configure?.Invoke(options); var limits = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()) throw new InvalidOperationException("Primitive ECS is already registered.");
            RegisterDefaults(services, limits);
        });
        return context;
    }
    internal static void RegisterDefaults(IServicesRegistrationContext services, PrimitiveSimulationOptions? options = null)
    {
        if (services.Exist<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()) return;
        var limits = (options ?? new()).Snapshot();
        if (!services.Exist<IFactory<TransformSystem, World>>())
            services.Singleton<IFactory<TransformSystem, World>>(_ => new Factory<TransformSystem, World>(world => new(world, limits.MaximumTransformEntities)));
        if (!services.Exist<IFactory<MovementSystem, World>>())
            services.Singleton<IFactory<MovementSystem, World>>(_ => new Factory<MovementSystem, World>(world => new(world, limits.MaximumTransformEntities)));
        if (!services.Exist<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>())
            services.Singleton<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(_ =>
                new Factory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>(arguments => new(arguments)));
        services.Singleton<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>(resolver => new PrimitiveSimulationFactory(
            resolver.Resolve<IFactory<TransformSystem, World>>(), resolver.Resolve<IFactory<MovementSystem, World>>(),
            resolver.Resolve<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(), limits));
    }
}
