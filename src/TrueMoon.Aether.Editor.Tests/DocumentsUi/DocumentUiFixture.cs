using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

internal sealed class DocumentUiFixture : IDisposable, IAsyncDisposable
{
    public DocumentFiles Files { get; } = new();
    public DocumentStore Store { get; } = new();
    public DocumentData.Sessions Authoring { get; } = new();
    public EditorProjects Projects { get; }
    public EditorDocumentCommands Commands { get; }
    public bool RuntimeIdle { get; set; } = true;
    public int RuntimeStops { get; private set; }
    public Func<Task>? StopRuntime { get; set; }
    public SceneDocumentSession Document => Projects.Active!.Documents!;

    public DocumentUiFixture(bool openProject = true, ISceneDocumentStore? sceneStore = null, CancellationToken lifetime = default, bool metadataOnly = false)
    {
        Projects = new(new EditorProjectsTests.Store(Files.Project), new EditorProjectsTests.Toolchain(),
            new EditorProjectsTests.Sessions { Activate = arguments => new(arguments, metadataOnly ? null : new(arguments.Project, sceneStore ?? Store, Authoring)) });
        Commands = new(Projects, () => RuntimeIdle, async () => { RuntimeStops++; if (StopRuntime != null) await StopRuntime(); }, lifetime);
        if (openProject) Projects.OpenAsync(Files.Project.ManifestPath).GetAwaiter().GetResult();
    }

    public void Run(DocumentCommand command, string path = "")
    { Assert.True(Commands.Execute(command, path)); Complete(); }
    public void Complete() => Commands.Pending.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    // UI tests deliberately keep the presentation owner thread; production work never captures its context.
    public static void Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    public void Decision() => Assert.True(SpinWait.SpinUntil(() => Commands.Read().AwaitingDecision, TimeSpan.FromSeconds(5)),
        "A tracked transition did not publish its decision.");
    public Task<bool>? BeginTransition(string transition, string path = "other.aetherscene")
    {
        if (transition == "exit") return Commands.RequestExitAsync();
        Assert.True(Commands.Execute(transition switch { "new" => DocumentCommand.New, "open" => DocumentCommand.Open, _ => DocumentCommand.Close }, path));
        return null;
    }
    public async Task CompleteTransition(Task<bool>? exit, bool expectedExit = true)
    { if (exit != null) Assert.Equal(expectedExit, await exit.WaitAsync(TimeSpan.FromSeconds(10))); else await Commands.Pending.WaitAsync(TimeSpan.FromSeconds(10)); }

    public async ValueTask DisposeAsync()
    { try { await Commands.DisposeAsync(); } finally { try { await Projects.DisposeAsync(); } finally { Files.Dispose(); } } }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public static UiSession Ui(Element root) => new(root, new Surface(), new(1280, 800));
    public static IEnumerable<Element> Tree(Element root)
    { yield return root; foreach (var child in root.Children) foreach (var item in Tree(child)) yield return item; }
    public static void Click(UiSession ui, Button button)
    { var b = button.Bounds; ui.HandleInput(new(InputKind.PointerDown, b.X + 4, b.Y + 4)); ui.HandleInput(new(InputKind.PointerUp, b.X + 4, b.Y + 4)); }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private sealed class Surface : IUiRenderSurface
    { public ITextLayoutService TextLayout { get; } = new TextLayout(); public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { } }
}
