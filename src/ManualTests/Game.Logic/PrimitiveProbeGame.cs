using TrueMoon;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Services;

namespace Game.Logic;

// Selected child-only consumer of the complete scene, system graph and existing primitive renderer.
public sealed class PrimitiveProbeGame(SceneWorldLoader loader, IFactory<PrimitiveSimulation, PrimitiveSimulationRequest> simulations)
    : PrimitiveGameSession(loader, simulations), IGameSessionEntry
{
    // Proof contract v1: quantize every extracted matrix/color scalar at 1/4096,
    // then fold in draw order with FNV-1a. The parent uses independently known scene values.
    public override uint PatternColor
    {
        get
        {
            if (ActiveScene == null) return base.PatternColor;
            var scene = CaptureRenderScene(); var hash = 14695981039346656037UL;
            foreach (var draw in scene.Primitives)
            {
                var m = draw.World; var c = draw.Color;
                ReadOnlySpan<float> values = [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
                    m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44, c.X, c.Y, c.Z];
                foreach (var value in values)
                {
                    var quantized = checked((long)Math.Round(value * 4096d, MidpointRounding.AwayFromZero));
                    hash = unchecked((hash ^ (ulong)quantized) * 1099511628211UL);
                }
            }
            hash = unchecked((hash ^ (uint)scene.Primitives.Length) * 1099511628211UL);
            return 0xff000000u | (uint)(hash & 0xffffff);
        }
    }

    public static IGameSession Create(IServiceResolver? services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var registry = PrimitiveEcsComponents.Register(new ComponentRegistryBuilder()).Build();
        var loaders = services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>()!;
        return new PrimitiveProbeGame(loaders.Create(registry), services.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!);
    }
}
