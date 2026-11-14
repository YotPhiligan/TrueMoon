using System.Runtime.CompilerServices;

// Runtime resolution builds immutable snapshots through the same neutral invariants.
// Workflow friendship is temporary until ARCH3.4 moves the remaining authoring implementation.
[assembly: InternalsVisibleTo("TrueMoon.Aether.Runtime")]
[assembly: InternalsVisibleTo("TrueMoon.Aether.Tooling")]
[assembly: InternalsVisibleTo("TrueMoon.Aether.Tests")]
[assembly: InternalsVisibleTo("TrueMoon.Aether.Editor.Tests")]
