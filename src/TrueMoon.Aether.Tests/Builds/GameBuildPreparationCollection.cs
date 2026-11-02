using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

// Keep large SDK/file snapshots separate from short-deadline transport/process tests.
[CollectionDefinition(nameof(GameBuildPreparationCollection), DisableParallelization = true)]
public sealed class GameBuildPreparationCollection;
