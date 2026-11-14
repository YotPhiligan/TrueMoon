using TrueMoon.Aether.Tooling.Projects;

namespace TrueMoon.Aether.Tooling.SourceTracking;

internal sealed class EditorSourceWatchFactory(EditorSourceWatchOptions options, TimeProvider time) : IFactory<EditorSourceWatch, ResolvedProject>
{
    public EditorSourceWatch Create(ResolvedProject arguments) => new(arguments, options, time);
}
