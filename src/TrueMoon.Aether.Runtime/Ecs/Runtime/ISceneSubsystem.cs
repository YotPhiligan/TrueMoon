namespace TrueMoon.Aether.Runtime.Ecs.Runtime;

/// <summary>Prepared resources belonging exclusively to one scene session.</summary>
/// <remarks>Create through an injected IFactory with SceneSubsystemArguments, before scheduler activation.
/// A factory failure must clean its partial resources or throw SceneWorldPreparationException with a retained owner.
/// The scheduler owns system instances; this owner releases their dependencies after scheduler disposal. Neither
/// the subsystem, its factories nor its resources may alias an owned system. Scene/World/App services are borrowed.
/// Preparation is paused and silent. No file IO, decoding or service resolution belongs in Execute.</remarks>
public interface ISceneSubsystem : IDisposable
{
    /// <summary>Gets bounded system registrations. Metadata/factory references are copied immediately after creation.</summary>
    IReadOnlyList<EcsSystemRegistration> Systems { get; }
    /// <summary>Changes real-time playback without advancing the World. Initially false; repeated values are harmless.</summary>
    /// <param name="playing">Actual runtime playback, after readiness/presentation gates.</param>
    void SetPlaying(bool playing);
}
