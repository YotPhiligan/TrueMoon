using TrueMoon.Aether.Editor.Documents;

namespace TrueMoon.Aether.Editor.Projects;

internal sealed class ProjectSessionFactory(IFactory<SceneDocumentSession, ResolvedProject>? documents = null) : IFactory<ProjectSession, ProjectSessionArguments>
{
    public ProjectSession Create(ProjectSessionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Project); ArgumentNullException.ThrowIfNull(arguments.Toolchain);
        var owned = documents?.Create(arguments.Project);
        if (documents != null && owned == null) throw new InvalidOperationException("Document session factory returned null.");
        try { return new(arguments, owned); }
        catch (Exception error)
        {
            try { owned?.Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
}
