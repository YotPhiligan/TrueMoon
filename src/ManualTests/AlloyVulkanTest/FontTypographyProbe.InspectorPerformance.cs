using TrueMoon.Argentis.Editors;
using TrueMoon;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const int InspectorPerformanceWidth = 336;
    private const int InspectorPerformanceHeight = 600;
    private static readonly ComponentId InspectorTransform = new(new Guid("40000000-0000-0000-0000-000000000001"));

    private static IApp BuildInspectorPerformanceApp(string output)
    {
        var projectDirectory = Path.Combine(output, "project");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Scenes"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Content"));
        File.WriteAllText(Path.Combine(projectDirectory, "Game.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var app = App.Build(context =>
        {
            context.UseAetherAuthoring();
            context.UseAetherEditorDocuments();
            context.UseAetherEditorProjects();
            context.Services(services => services
                .Singleton<EditorDocumentCommands>(resolver => new(resolver.Resolve<EditorProjects>()!, () => true, () => Task.CompletedTask))
                .Singleton<EditorPreview>(_ => new((_, _) => throw new InvalidOperationException("The inspector fixture does not start preview."),
                    () => Task.CompletedTask)));
            context.UseAetherEditorEditing();
        });
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var project = app.Services.Resolve<EditorProjects>()!.CreateAsync(Path.Combine(projectDirectory, "Inspector.aetherproj"),
                new(ProjectDescription.CurrentVersion, new Guid("10000000-0000-0000-0000-000000000001"),
                    "Inspector performance", "Game.csproj", "Game.dll", "Game.Entry", "Scenes", "Content"), deadline.Token).GetAwaiter().GetResult();
            var (scene, entity) = CreateInspectorPerformanceDocument();
            Require(project.Documents!.NewAsync(scene, cancellationToken: deadline.Token).GetAwaiter().GetResult(), "Could not open the fixture document.");
            var inspector = app.Services.Resolve<EditorInspectorViewModel>()!;
            inspector.Refresh(entity.Value.ToString("D"));
            Require(inspector.HasSelection && inspector.CanEditProperties && inspector.Components.Count == 4, "Fixture did not populate the editor inspector.");
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static (SceneDocument Scene, EntityId Entity) CreateInspectorPerformanceDocument()
    {
        var entity = new EntityId(new Guid("60000000-0000-0000-0000-000000000001"));
        var definition = new EntityDefinitionId(new Guid("50000000-0000-0000-0000-000000000001"));
        var custom = new ComponentSchema(new(new Guid("30000000-0000-0000-0000-000000000010")), 1,
        [
            new("active", PropertyValueKind.Boolean, PropertyValue.From(true)),
            new("note", PropertyValueKind.String, PropertyValue.From("Станция наблюдения · Observation deck\nГотово ✅ · Автор 👩‍💻")),
            new("revision", PropertyValueKind.Integer, PropertyValue.From(20261121000123L))
        ]);
        var source = new EntityDefinition(definition, new(addedComponents:
        [
            new(InspectorTransform, BuiltInComponents.TransformId, 1,
                [KeyValuePair.Create("x", PropertyValue.From(123456.789012345d)), KeyValuePair.Create("y", PropertyValue.From(12.5d))]),
            new(new(new Guid("40000000-0000-0000-0000-000000000002")), BuiltInComponents.AppearanceId, 1),
            new(new(new Guid("40000000-0000-0000-0000-000000000003")), BuiltInComponents.InteractionId, 1),
            new(new(new Guid("40000000-0000-0000-0000-000000000010")), custom.TypeId, 1)
        ]));
        return (new(new(new Guid("70000000-0000-0000-0000-000000000001")), 0,
            [new(entity, definition, null, null, "Станция наблюдения", null)], [source], [.. BuiltInComponents.Schemas, custom]), entity);
    }

    private static (Element Root, TextBox[] Fields) CreateInspectorPerformanceRoot(IFactory<EditorInspectorView> factory)
    {
        var view = factory.Create() ?? throw new InvalidOperationException("The inspector factory did not return a view.");
        if (view.Properties.TryGetValue((InspectorTransform, "x"), out var property) && property.Input is TextBox input)
            return (view, [input]);
        view.Dispose();
        throw new InvalidOperationException("The inspector did not expose the Transform X editor.");
    }
}
