using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Copied standalone window settings, supplied after the host's configuration overrides.</summary>
/// <param name="Title">Base caption; project/document state is appended by the shared presentation model.</param>
/// <param name="ClientSize">Requested logical client size, independent of the content layout.</param>
public sealed record EditorMainWindowArguments(string Title, Size ClientSize);
