using TrueMoon.Aether.Ecs;
using TrueMoon.Kton.Contacts;

namespace TrueMoon.Aether.Kton;

/// <summary>Transient event entity published after Movement. Gameplay reads tick N contacts during tick N+1.</summary>
/// <remarks>The bridge retires its previous event entities at the next Movement boundary. Entity/body generations
/// may already be stale for End events; never reinterpret them as a newly spawned entity or persist these handles.</remarks>
/// <param name="Contact">Copied backend-neutral event, including exact body generations and issuing World.</param>
/// <param name="EntityA">Mapped original ECS identity.</param><param name="EntityB">Mapped original ECS identity.</param>
public readonly record struct PhysicsContact(PhysicsContactEvent Contact, EntityHandle EntityA, EntityHandle EntityB) : IComponent;
