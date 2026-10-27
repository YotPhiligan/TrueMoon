using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Scenes;
using System.Text.Json;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether;
using TrueMoon.Dependencies;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class ProjectEntryPoint
{
    internal static async Task CheckSceneAsync(string projectPath, string scenePath)
    {
        await using var app = App.Build(context =>
        {
            context.UseAetherAuthoring(); context.UseAetherEditorDocuments(); context.UseAetherEditorProjects();
        });
        await app.StartAsync();
        var projects = app.Services.Resolve<EditorProjects>()!;
        var session = await projects.OpenAsync(projectPath);
        var documents = session.Documents!;
        if (!await documents.LoadAsync(scenePath)) throw new InvalidOperationException("Scene loading was vetoed.");
        var state = documents.Read(); var content = state.Content!;
        var roundTripBytes = app.Services.Resolve<SceneFileCodec>()!.Encode(content.Document);
        var path = state.Path;
        await app.StopAsync();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Success = true, Path = path, SceneId = content.Document.Id.Value, content.Document.Revision,
            Entities = content.Document.Entities.Length, Definitions = content.Document.Definitions.Length,
            Schemas = content.Document.Schemas.Length, RoundTripBytes = roundTripBytes.Length, state.IsDirty,
            SessionClosed = session.IsClosed, DocumentClosed = documents.Read().IsClosed, ActiveCleared = projects.Active == null,
            GameLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == Path.GetFileNameWithoutExtension(session.Project.Description.EntryAssembly))
        }));
    }

    internal static async Task CheckAsync(string path)
    {
        await using var app = App.Build(context => context.UseAetherEditorProjects());
        await app.StartAsync();
        var projects = app.Services.Resolve<EditorProjects>()!;
        var session = await projects.OpenAsync(path);
        var project = session.Project;
        await app.StopAsync();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Success = true, project.Description.FormatVersion, project.Description.Id, project.Description.Name,
            project.ManifestPath, project.RootDirectory, project.GameProjectPath, project.ScenesDirectory, project.ContentDirectory,
            session.Toolchain.SdkVersion, SessionClosed = session.IsClosed, ActiveCleared = projects.Active == null,
            GameLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == Path.GetFileNameWithoutExtension(project.Description.EntryAssembly))
        }));
    }
}
