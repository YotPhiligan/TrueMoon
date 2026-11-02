using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;

namespace TrueMoon.Aether.Editor.Projects;

internal sealed class ProjectSessionFactory(IFactory<SceneDocumentSession, ResolvedProject>? documents = null,
    IFactory<EditorSourceWatch, ResolvedProject>? source = null) : IFactory<ProjectSession, ProjectSessionArguments>, IDisposable
{
    private readonly List<IDisposable> _retired = [];
    public ProjectSession Create(ProjectSessionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Project); ArgumentNullException.ThrowIfNull(arguments.Toolchain);
        SceneDocumentSession? owned = null; EditorSourceWatch? watch = null;
        try
        {
            owned = documents?.Create(arguments.Project);
            if (documents != null && owned == null) throw new InvalidOperationException("Document session factory returned null.");
            watch = source?.Create(arguments.Project);
            if (source != null && watch == null) throw new InvalidOperationException("Source session factory returned null.");
            return new(arguments, owned, watch);
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            foreach (var product in new IDisposable?[] { watch, owned })
                try { product?.Dispose(); }
                catch (Exception cleanup) { lock (_retired) _retired.Add(product!); errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException(errors);
            throw;
        }
    }
    // Only partial-creation orphans belong to this factory; successful products belong solely to ProjectSession.
    public void Dispose()
    {
        List<Exception>? errors = null;
        lock (_retired)
        {
            foreach (var product in _retired.ToArray())
                try { product.Dispose(); _retired.Remove(product); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors != null) throw new AggregateException(errors);
    }
}
