using Xunit;

namespace TrueMoon.Aether.Tests.Diagnostics;

/// <summary>Isolates live-pipe timing barriers from unrelated compiler/process suite load.</summary>
[CollectionDefinition(nameof(ChildDiagnosticForwarderCollection), DisableParallelization = true)]
public sealed class ChildDiagnosticForwarderCollection;
