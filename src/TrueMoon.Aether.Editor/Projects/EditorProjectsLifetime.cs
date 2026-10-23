namespace TrueMoon.Aether.Editor.Projects;

// Lifecycle aliases borrow the one DI-owned coordinator; they never dispose it a second time.
internal sealed class EditorProjectsLifetime(EditorProjects projects) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(projects.IsClosed, projects);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default) => projects.DisposeAsync().AsTask();
}
