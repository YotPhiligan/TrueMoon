using TrueMoon.Aether.Editor;
using TrueMoon.Configuration;
using TrueMoon.Aether.Hosting;
namespace TrueMoon.ManualTests.AetherEditorHost;

/// <summary>Explicit application launch inputs. An empty launch opens a workspace without selecting a demo project.</summary>
/// <param name="ProjectPath">Absolute .aetherproj, or an explicitly selected legacy game csproj.</param>
/// <param name="CheckProjectPath">Absolute manifest for headless preflight.</param>
/// <param name="SmokeDirectory">Fresh legacy UI regression output directory.</param>
/// <param name="EntryAssembly">Legacy game DLL override.</param>
/// <param name="EntryType">Legacy game entry override.</param>
/// <param name="Light">Use the light theme.</param>
/// <param name="Validation">Request process-local graphics validation.</param>
/// <param name="CheckScenePath">Scene preflight within the explicitly selected project's scenes root.</param>
/// <param name="DocumentSmoke">Run directed document UI acceptance against the explicit project fixture.</param>
/// <param name="TransformSmoke">Run directed Transform/history/preview acceptance against the explicit project fixture.</param>
/// <param name="HierarchySmoke">Run directed hierarchy/history/save/preview acceptance against a disposable fixture.</param>
/// <param name="InspectorSmoke">Run schema-driven scalar/component acceptance against a disposable fixture.</param>
/// <param name="NavigationSmoke">Run directed retained camera/selection input acceptance.</param>
/// <param name="AcceptanceSmoke">Run camera lifecycle/context native acceptance.</param>
/// <param name="NativeInputSmoke">Observe Windows input in a prepared native preview.</param>
/// <param name="CameraSmoke">Run explicit preview-view camera/presentation acceptance without navigation input.</param>
public sealed record EditorLaunchOptions(string? ProjectPath, string? CheckProjectPath, string? SmokeDirectory,
    string? EntryAssembly, string? EntryType, bool Light, bool Validation, string? CheckScenePath = null, bool DocumentSmoke = false, bool TransformSmoke = false, bool HierarchySmoke = false, bool InspectorSmoke = false, bool CameraSmoke = false, bool NavigationSmoke = false, bool AcceptanceSmoke = false, bool NativeInputSmoke = false)
{
    /// <summary>Runs directed world translation/outline/commit/history acceptance on a disposable fixture.</summary>
    public bool TranslationSmoke { get; init; }
    /// <summary>Whether the command explicitly selected validation, including false.</summary>
    public bool ValidationSpecified { get; init; }
    /// <summary>Combines the frozen command with selected public environment flags; explicit CLI wins.</summary>
    /// <param name="capturedEnvironment">Selected common bootstrap capture.</param><returns>Immutable runtime flags.</returns>
    public RuntimeStartupSettings ResolveRuntimeSettings(IReadOnlyDictionary<string, string?> capturedEnvironment)
    {
        ArgumentNullException.ThrowIfNull(capturedEnvironment);
        return ValidationSpecified || Validation ? new(Validation) : RuntimeStartupSettings.Parse(capturedEnvironment);
    }
    /// <summary>Gets whether a legacy game csproj was explicitly selected.</summary>
    public bool LegacyProject => ProjectPath?.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) == true;

    private sealed class LaunchData
    {
        public string? ProjectPath { get; set; }
        public string? CheckProjectPath { get; set; }
        public string? CheckScenePath { get; set; }
        public string? SmokeDirectory { get; set; }
        public string? EntryAssembly { get; set; }
        public string? EntryType { get; set; }
        public bool Light { get; set; }
        public bool Validation { get; set; }
        public bool DocumentSmoke { get; set; }
        public bool TransformSmoke { get; set; }
        public bool HierarchySmoke { get; set; }
        public bool InspectorSmoke { get; set; }
        public bool CameraSmoke { get; set; }
        public bool NavigationSmoke { get; set; }
        public bool AcceptanceSmoke { get; set; }
        public bool NativeInputSmoke { get; set; }
        public bool TranslationSmoke { get; set; }
    }
    private static readonly IConfigurationSchema<LaunchData> LaunchSchema = ConfigurationSchemas.Create("EditorLaunch", () => new LaunchData(),
        value => new LaunchData { ProjectPath = value.ProjectPath, CheckProjectPath = value.CheckProjectPath, CheckScenePath = value.CheckScenePath, SmokeDirectory = value.SmokeDirectory, EntryAssembly = value.EntryAssembly, EntryType = value.EntryType, Light = value.Light, Validation = value.Validation, DocumentSmoke = value.DocumentSmoke, TransformSmoke = value.TransformSmoke, HierarchySmoke = value.HierarchySmoke, InspectorSmoke = value.InspectorSmoke, CameraSmoke = value.CameraSmoke, NavigationSmoke = value.NavigationSmoke, AcceptanceSmoke = value.AcceptanceSmoke, NativeInputSmoke = value.NativeInputSmoke, TranslationSmoke = value.TranslationSmoke }, value =>
        {
            if (value.ProjectPath is not null && string.IsNullOrWhiteSpace(value.ProjectPath)) throw new ArgumentException("An editor argument requires a value.");
            if (value.CheckProjectPath is not null && string.IsNullOrWhiteSpace(value.CheckProjectPath)) throw new ArgumentException("An editor argument requires a value.");
            if (value.CheckScenePath is not null && string.IsNullOrWhiteSpace(value.CheckScenePath)) throw new ArgumentException("An editor argument requires a value.");
            if (value.SmokeDirectory is not null && string.IsNullOrWhiteSpace(value.SmokeDirectory)) throw new ArgumentException("An editor argument requires a value.");
            if (value.EntryAssembly is not null && string.IsNullOrWhiteSpace(value.EntryAssembly)) throw new ArgumentException("An editor argument requires a value.");
            if (value.EntryType is not null && string.IsNullOrWhiteSpace(value.EntryType)) throw new ArgumentException("An editor argument requires a value.");
        }, fields =>
        {
            fields.Field("ProjectPath", value => value.ProjectPath, (value, next) => value.ProjectPath = next, new(Aliases: ["--project"]));
            fields.Field("CheckProjectPath", value => value.CheckProjectPath, (value, next) => value.CheckProjectPath = next, new(Aliases: ["--check-project"]));
            fields.Field("CheckScenePath", value => value.CheckScenePath, (value, next) => value.CheckScenePath = next, new(Aliases: ["--check-scene"]));
            fields.Field("SmokeDirectory", value => value.SmokeDirectory, (value, next) => value.SmokeDirectory = next, new(Aliases: ["--smoke"]));
            fields.Field("EntryAssembly", value => value.EntryAssembly, (value, next) => value.EntryAssembly = next, new(Aliases: ["--assembly"]));
            fields.Field("EntryType", value => value.EntryType, (value, next) => value.EntryType = next, new(Aliases: ["--entry"]));
            fields.Field("Light", value => value.Light, (value, next) => value.Light = next, new(Aliases: ["--light"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("Validation", value => value.Validation, (value, next) => value.Validation = next, new(Aliases: ["--validation"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("DocumentSmoke", value => value.DocumentSmoke, (value, next) => value.DocumentSmoke = next, new(Aliases: ["--smoke-documents"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("TransformSmoke", value => value.TransformSmoke, (value, next) => value.TransformSmoke = next, new(Aliases: ["--smoke-transform"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("HierarchySmoke", value => value.HierarchySmoke, (value, next) => value.HierarchySmoke = next, new(Aliases: ["--smoke-hierarchy"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("InspectorSmoke", value => value.InspectorSmoke, (value, next) => value.InspectorSmoke = next, new(Aliases: ["--smoke-inspector"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("CameraSmoke", value => value.CameraSmoke, (value, next) => value.CameraSmoke = next, new(Aliases: ["--smoke-camera"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("NavigationSmoke", value => value.NavigationSmoke, (value, next) => value.NavigationSmoke = next, new(Aliases: ["--smoke-navigation"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("AcceptanceSmoke", value => value.AcceptanceSmoke, (value, next) => value.AcceptanceSmoke = next, new(Aliases: ["--smoke-viewport"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("NativeInputSmoke", value => value.NativeInputSmoke, (value, next) => value.NativeInputSmoke = next, new(Aliases: ["--smoke-native-input"], Argument: ConfigurationArgumentKind.Flag));
            fields.Field("TranslationSmoke", value => value.TranslationSmoke, (value, next) => value.TranslationSmoke = next, new(Aliases: ["--smoke-translation"], Argument: ConfigurationArgumentKind.Flag));
        });

    /// <summary>Parses explicit CLI choices and rejects ambiguous/unsupported combinations.</summary>
    /// <param name="arguments">Arguments excluding the runtime/help branches.</param>
    /// <returns>Normalized launch data, without opening files or starting services.</returns>
    public static EditorLaunchOptions Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        LaunchData data;
        bool validationSpecified;
        try
        {
            var parsed = ConfigurationCommandLine.ParseSingleUse(arguments, LaunchSchema.Keys);
            validationSpecified = parsed.Entries.Any(entry => entry.Path == "EditorLaunch:Validation");
            if (parsed.Positionals.Count != 0) throw new ArgumentException("Unexpected editor positional arguments. Use --help.");
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(LaunchSchema);
                builder.AddValues("editor-command-line", ConfigurationSourcePriority.Arguments, parsed.Entries);
            });
            data = LaunchSchema.Bind(profile.LoadAsync().GetAwaiter().GetResult());
        }
        catch (ConfigurationException) { throw new ArgumentException("Invalid editor arguments. Use --help.", nameof(arguments)); }
        string? FullPath(string? path) => path is null ? null : Path.GetFullPath(path);
        var result = new EditorLaunchOptions(FullPath(data.ProjectPath), FullPath(data.CheckProjectPath), FullPath(data.SmokeDirectory),
            data.EntryAssembly, data.EntryType, data.Light, data.Validation, data.CheckScenePath, data.DocumentSmoke, data.TransformSmoke,
            data.HierarchySmoke, data.InspectorSmoke, data.CameraSmoke, data.NavigationSmoke, data.AcceptanceSmoke, data.NativeInputSmoke)
            { TranslationSmoke = data.TranslationSmoke, ValidationSpecified = validationSpecified };
        if (result.ProjectPath != null && !result.LegacyProject && !result.ProjectPath.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--project requires a .aetherproj manifest or explicit legacy .csproj.");
        if (result.CheckProjectPath != null && (!result.CheckProjectPath.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase) ||
            result.ProjectPath != null || result.SmokeDirectory != null || result.EntryAssembly != null || result.EntryType != null))
            throw new ArgumentException("--check-project requires one .aetherproj and cannot be combined with project/smoke/entry options.");
        if (!result.LegacyProject && (result.EntryAssembly != null || result.EntryType != null))
            throw new ArgumentException("Assembly/entry overrides apply only to explicit legacy csproj launches; .aetherproj owns its entry.");
        if (result.CheckScenePath != null && (result.ProjectPath == null || result.LegacyProject || result.CheckProjectPath != null ||
            result.SmokeDirectory != null || !result.CheckScenePath.EndsWith(".aetherscene", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("--check-scene requires --project project.aetherproj and one .aetherscene; it cannot be combined with check-project/smoke.");
        if (result.DocumentSmoke && (result.SmokeDirectory == null || result.ProjectPath == null || result.LegacyProject ||
            result.CheckProjectPath != null || result.CheckScenePath != null))
            throw new ArgumentException("--smoke-documents requires --smoke fresh-directory and --project fixture.aetherproj.");
        if (result.TransformSmoke && (result.DocumentSmoke || result.SmokeDirectory == null || result.ProjectPath == null || result.LegacyProject ||
            result.CheckProjectPath != null || result.CheckScenePath != null))
            throw new ArgumentException("--smoke-transform requires --smoke fresh-directory and --project fixture.aetherproj, without other smoke profiles.");
        if (result.HierarchySmoke && (result.DocumentSmoke || result.TransformSmoke || result.SmokeDirectory == null || result.ProjectPath == null ||
            result.LegacyProject || result.CheckProjectPath != null || result.CheckScenePath != null))
            throw new ArgumentException("--smoke-hierarchy requires --smoke fresh-directory and --project fixture.aetherproj, without other smoke profiles.");
        if (result.InspectorSmoke && (result.DocumentSmoke || result.TransformSmoke || result.HierarchySmoke || result.SmokeDirectory == null || result.ProjectPath == null ||
            result.LegacyProject || result.CheckProjectPath != null || result.CheckScenePath != null))
            throw new ArgumentException("--smoke-inspector requires --smoke fresh-directory and --project fixture.aetherproj, without other smoke profiles.");
        if (result.CameraSmoke && (result.DocumentSmoke || result.TransformSmoke || result.HierarchySmoke || result.InspectorSmoke ||
            result.SmokeDirectory == null || result.ProjectPath == null || result.LegacyProject || result.CheckProjectPath != null || result.CheckScenePath != null))
            throw new ArgumentException("--smoke-camera requires --smoke fresh-directory and --project fixture.aetherproj, without other smoke profiles.");
        if (result.NavigationSmoke && (result.DocumentSmoke || result.TransformSmoke || result.HierarchySmoke || result.InspectorSmoke || result.CameraSmoke ||
            result.SmokeDirectory == null || result.ProjectPath == null || result.LegacyProject || result.CheckProjectPath != null || result.CheckScenePath != null))
            throw new ArgumentException("--smoke-navigation requires --smoke fresh-directory and --project fixture.aetherproj, without other smoke profiles.");
        if (result.AcceptanceSmoke || result.NativeInputSmoke || result.TranslationSmoke)
        {
            var profiles = new[] { result.DocumentSmoke, result.TransformSmoke, result.HierarchySmoke, result.InspectorSmoke, result.CameraSmoke, result.NavigationSmoke, result.AcceptanceSmoke, result.NativeInputSmoke, result.TranslationSmoke };
            if (profiles.Count(p => p) != 1 || result.SmokeDirectory == null || result.ProjectPath == null || result.LegacyProject || result.CheckProjectPath != null || result.CheckScenePath != null)
                throw new ArgumentException("Viewport acceptance requires one profile, --project fixture.aetherproj and --smoke fresh-directory.");
        }
        return result;
    }
}
