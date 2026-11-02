using TrueMoon.Aether.Editor.Projects;

namespace TrueMoon.Aether.Editor.Editing;

internal sealed class EditorSourceWatchFactory(EditorSourceWatchOptions options, TimeProvider time) : IFactory<EditorSourceWatch, ResolvedProject>
{
    public EditorSourceWatch Create(ResolvedProject arguments) => new(arguments, options, time);
}
