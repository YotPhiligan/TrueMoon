using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Diagnostics;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Aether.Tooling.SourceTracking;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.ToolingConsumer;

internal static class Program
{
    private static readonly Guid SceneGuid = Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d301");
    private static readonly EntityId Entity = new(Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d302"));
    private static readonly ComponentId Transform = new(Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d303"));
    private static readonly ComponentId Opaque = new(Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d304"));
    private static readonly ComponentTypeId Custom = new(Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d305"));
    private const string OpaqueText = "custom game data: Мир 🛰️\n\"unchanged\"";
    private static int _checks;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 6) throw new ArgumentException("Expected work, report, provider, target framework, language version and feature snapshot.");
            var work = Path.GetFullPath(args[0]); var report = Path.GetFullPath(args[1]);
            var microsoft = args[2] == "microsoft"; var framework = args[3];
            var game = Path.Combine(work, "Game"); var scenes = Path.Combine(work, "Scenes");
            Directory.CreateDirectory(game); Directory.CreateDirectory(scenes); Directory.CreateDirectory(Path.Combine(work, "Content"));
            var artifactRoot = Path.Combine(work, "builds"); var manifest = Path.Combine(work, "headless.aetherproj");
            var gameProject = Path.Combine(game, "Headless.Game.csproj"); var gameSource = Path.Combine(game, "Game.cs");
            await File.WriteAllTextAsync(gameProject, new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", framework), new XElement("AssemblyName", "Headless.Game"),
                    new XElement("Nullable", "enable"), new XElement("ImplicitUsings", "enable"), new XElement("LangVersion", args[4]),
                    new XElement("Features", args[5])),
                new XElement("ItemGroup", Reference("TrueMoon.Aether.Abstractions", "AetherAbstractionsPath", typeof(SceneDocument).Assembly.Location),
                    Reference("TrueMoon.Contracts", "TrueMoonContractsPath", typeof(IFactory<>).Assembly.Location))).ToString());
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("HeadlessGameSource")!)
            using (var reader = new StreamReader(stream)) await File.WriteAllTextAsync(gameSource, await reader.ReadToEndAsync());
            var originalSource = await File.ReadAllTextAsync(gameSource);
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(ToolingConfigurationSchemas.Settings);
                builder.Override("Aether:Tooling:BuildDirectory", artifactRoot);
                builder.Override("Aether:Tooling:SourceTracking:UseFileSystemNotifications", "false");
            });
            ToolingHost host;
            var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context =>
                context.UseConfiguration(profile).UseConfiguredAetherTooling(new(BuildIdentity.HostId))).Build();
            try
            {
                await app.StartAsync(); host = app.Services.Resolve<ToolingHost>()!;
                Check(!Directory.Exists(artifactRoot), "Idle App created artifact IO.");
                Check(app.Services.ResolveAll<RuntimeEngine>().Count() == 0 && app.Services.ResolveAll<AetherHost>().Count() == 0, "Headless graph contains execution workflow.");
                Check(!typeof(ToolingHost).Assembly.GetReferencedAssemblies().Any(Forbidden), "Tooling references UI.");
                var description = new ProjectDescription(1, Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d399"), "Headless", "Game/Headless.Game.csproj",
                    "Headless.Game.dll", "Headless.Game.Game", "Scenes", "Content");
                var project = await host.Projects.CreateAsync(manifest, description);
                Check(project.Toolchain.SdkVersion.StartsWith(framework[3..].Split('.')[0] + ".", StringComparison.Ordinal), "Unexpected selected SDK.");
                Check(project.SourceWatch!.Read().State == EditorSourceState.NoBuild, "Source worker started before Build.");
                var documents = project.Documents!;
                await documents.NewAsync(Scene());
                Check(X(documents) == 2 && documents.Read().IsDirty, "Initial scene state differs.");
                documents.Apply(new(Operation(documents), [new(Entity, Transform, new(9, 1, 2))]));
                Check(X(documents) == 9, "Apply did not commit.");
                documents.Undo(Operation(documents)); Check(X(documents) == 2, "Undo did not restore original values.");
                documents.Redo(Operation(documents)); Check(X(documents) == 9, "Redo did not restore committed values.");
                var saved = await documents.SaveAsAsync("headless.aetherscene");
                Check(!documents.Read().IsDirty, "Save checkpoint differs from the persisted revision.");
                var file = await app.Services.Resolve<ISceneDocumentStore>()!.ReadAsync(saved.Path);
                Check(file.Document.Id.Value == SceneGuid && file.Document.Entities.Single().Id == Entity, "Persistent IDs changed.");
                Check(file.Document.Definitions.Single().Changes.AddedComponents.Single(c => c.Id == Opaque).Properties["payload"].GetString() == OpaqueText,
                    "Custom component data was rewritten.");
                var result = await host.BuildAsync(documents.Read().Content!.Document.Revision);
                Check(ReferenceEquals(result, host.LastGoodBuild) && !result.Artifact.RuntimeAccess, "Neutral last-good candidate was not selected.");
                Check(result.Inputs != null && result.Compilation != null && host.Builds.ProcessId == null, "Frozen provenance/reap is incomplete.");
                Check(project.SourceWatch.Read().BuildId == result.BuildId, "Source baseline was not bound.");
                await File.WriteAllTextAsync(gameSource, originalSource + "\ninvalid C# source here\n");
                await ExpectAsync<GameBuildException>(() => host.BuildAsync(900));
                Check(ReferenceEquals(result, host.LastGoodBuild) && X(documents) == 9 && !documents.Read().IsDirty, "Failed Build changed selected artifact/document.");
                await File.WriteAllTextAsync(gameSource, originalSource);
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.CancelAfter(TimeSpan.FromMilliseconds(50));
                    await ExpectAsync<OperationCanceledException>(() => host.BuildAsync(901, cancelled.Token));
                }
                Check(ReferenceEquals(result, host.LastGoodBuild) && host.Builds.ProcessId == null, "Cancelled Build lost last-good or retained compiler process.");
                documents.Apply(new(Operation(documents), [new(Entity, Transform, new(11, 1, 2))]));
                var memory = documents.Read().Content!.Document;
                var savedBytes = await File.ReadAllBytesAsync(saved.Path);
                await File.AppendAllTextAsync(saved.Path, "\n ");
                var externalBytes = await File.ReadAllBytesAsync(saved.Path);
                var expectedFileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(externalBytes));
                await ExpectAsync<SceneFileConflictException>(() => documents.SaveAsync());
                Check(ReferenceEquals(memory, documents.Read().Content!.Document) && documents.Read().IsDirty && X(documents) == 11, "Failed Save changed memory/history.");
                var afterConflictBytes = await File.ReadAllBytesAsync(saved.Path);
                var afterFileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(afterConflictBytes));
                Check(expectedFileHash == afterFileHash, $"Conflict file SHA changed: {expectedFileHash} -> {afterFileHash}.");
                Check(ReferenceEquals(result, host.LastGoodBuild), "Failed Save changed selected Build.");
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    await ExpectAsync<OperationCanceledException>(() => documents.SaveAsync(cancelled.Token));
                }
                await File.WriteAllBytesAsync(saved.Path, savedBytes);
                await host.Projects.CloseAsync(DocumentCloseChoice.Discard);
                Check(host.LastGoodBuild == null && project.IsClosed && project.SourceWatch.Read().State == EditorSourceState.Stopped, "Closing opening retained selected owner/observer.");
                var reopened = await host.Projects.OpenAsync(manifest);
                await reopened.Documents!.LoadAsync("headless.aetherscene");
                Check(X(reopened.Documents) == 9 && !reopened.Documents.Read().IsDirty, "Reopen did not recover exact saved content.");
                Check(reopened.Project.Description.Id == description.Id && reopened.Documents.Read().Content!.Document.Id.Value == SceneGuid, "Reopen changed identities.");
                await host.Projects.CloseAsync();
                await app.Services.Resolve<EditorDiagnosticReportWriter>()!.WriteAsync(new([], 0, 0, 0, 0, false), Path.Combine(work, "diagnostic.json"));
                await app.StopAsync(); await app.DisposeAsync(); await host.DisposeAsync();
                Check(host.IsClosed && reopened.IsClosed && host.Builds.ProcessId == null, "App cleanup did not drain actual owners.");
                var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName()).Where(Forbidden).Select(a => a.Name).ToArray();
                Check(loaded.Length == 0, "UI assembly was loaded in headless execution.");
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                var native = process.Modules.Cast<System.Diagnostics.ProcessModule>().Select(module => module.ModuleName)
                    .Where(name => name.Contains("vulkan", StringComparison.OrdinalIgnoreCase) || name.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("glfw", StringComparison.OrdinalIgnoreCase) || name.Contains("MiniAudio", StringComparison.OrdinalIgnoreCase)).ToArray();
                Check(native.Length == 0, "Graphics/audio native module was activated by headless workflow.");
                await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new
                {
                    Completed = true, Stage = "ARCH3.4", Provider = microsoft ? "MicrosoftDI" : "Cobalt", Checks = _checks,
                    ProjectId = description.Id, SceneId = SceneGuid, SavedX = 9, FailedSaveMemoryX = 11,
                    OpaquePreserved = true, FrozenBuild = result.BuildId, RuntimeAccess = result.Artifact.RuntimeAccess,
                    FailedBuildRetainedLastGood = true, CancelledBuildReaped = true, FailedSavePreservedFileAndMemory = true,
                    Reopened = true, Closed = host.IsClosed, CompilerPid = host.Builds.ProcessId, ForbiddenAssemblies = loaded, ForbiddenNativeModules = native,
                    Limits = "Windows x64 headless IO/SDK/package proof; no native/GPU activation, mod reload or release acceptance"
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            finally { await app.DisposeAsync(); }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static XElement Reference(string name, string property, string fallback) => new("Reference", new XAttribute("Include", name),
        new XElement("HintPath", new XAttribute("Condition", $"'$({property})' != ''"), $"$({property})"),
        new XElement("HintPath", new XAttribute("Condition", $"'$({property})' == ''"), fallback), new XElement("Private", "false"));
    private static bool Forbidden(AssemblyName name) => name.Name!.StartsWith("TrueMoon.Aether.Editor", StringComparison.Ordinal) ||
        name.Name.StartsWith("TrueMoon.Alloy", StringComparison.Ordinal) || name.Name.StartsWith("TrueMoon.Argentis", StringComparison.Ordinal);
    private static void Check(bool condition, string message) { _checks++; if (!condition) throw new InvalidOperationException(message); }
    private static async Task ExpectAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { _checks++; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static AuthoringOperation Operation(SceneDocumentSession documents) => new(Guid.NewGuid(), documents.Read().Content!.Document.Revision, "headless");
    private static double X(SceneDocumentSession documents) => documents.Read().Content!.Resolved.Entities[Entity].Components[Transform].Properties["x"].Value.GetNumber();
    private static SceneDocument Scene()
    {
        var definition = new EntityDefinitionId(Guid.Parse("5a334100-c9cb-4fac-9122-b0688ff9d306"));
        var schema = new ComponentSchema(Custom, 7, [new("payload", PropertyValueKind.String, PropertyValue.From(""))]);
        return new(new(SceneGuid), 7, [new(Entity, definition)], [new(definition, new(addedComponents:
            [new(Transform, BuiltInComponents.TransformId, 1, [KeyValuePair.Create("x", PropertyValue.From(2d))]),
             new(Opaque, Custom, 7, [KeyValuePair.Create("payload", PropertyValue.From(OpaqueText))])]))], [.. BuiltInComponents.Schemas, schema]);
    }
}
