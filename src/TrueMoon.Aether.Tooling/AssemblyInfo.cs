using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrueMoon.Aether.Tests")]
[assembly: InternalsVisibleTo("TrueMoon.Aether.Editor.Tests")]

[assembly: InternalsVisibleTo("TrueMoon.Aether.Editor.Tooling")]

// The source-only native acceptance host can publish a directed ledger fixture.
[assembly: InternalsVisibleTo("AetherEditorHost")]
