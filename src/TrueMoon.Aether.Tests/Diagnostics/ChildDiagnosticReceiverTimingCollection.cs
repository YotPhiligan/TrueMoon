using Xunit;

namespace TrueMoon.Aether.Tests.Diagnostics;

// This fixture checks real 20/50 ms option floors, not a concurrent-workload SLA.
// Keep it apart from compiler/process fixtures without changing production budgets.
[CollectionDefinition(nameof(ChildDiagnosticReceiverTimingCollection), DisableParallelization = true)]
public sealed class ChildDiagnosticReceiverTimingCollection;
