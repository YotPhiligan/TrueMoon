using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

// Forced collections and allocation windows must not overlap unrelated short-deadline tests.
[CollectionDefinition(nameof(EntityCommandBufferAllocationCollection), DisableParallelization = true)]
public sealed class EntityCommandBufferAllocationCollection
{
}
