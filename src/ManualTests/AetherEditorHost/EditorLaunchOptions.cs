using TrueMoon.Aether.Editor;
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
    /// <summary>Gets whether a legacy game csproj was explicitly selected.</summary>
    public bool LegacyProject => ProjectPath?.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Parses explicit CLI choices and rejects ambiguous/unsupported combinations.</summary>
    /// <param name="arguments">Arguments excluding the runtime/help branches.</param>
    /// <returns>Normalized launch data, without opening files or starting services.</returns>
    public static EditorLaunchOptions Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index++)
        {
            var name = arguments[index];
            if (name is not ("--project" or "--check-project" or "--check-scene" or "--smoke" or "--smoke-documents" or "--smoke-transform" or "--smoke-hierarchy" or "--smoke-inspector" or "--smoke-camera" or "--smoke-navigation" or "--smoke-viewport" or "--smoke-native-input" or "--assembly" or "--entry" or "--light" or "--validation"))
                throw new ArgumentException($"Unknown editor argument '{name}'. Use --help.");
            if (!values.TryAdd(name, null)) throw new ArgumentException($"Duplicate editor argument '{name}'.");
            if (name is "--light" or "--validation" or "--smoke-documents" or "--smoke-transform" or "--smoke-hierarchy" or "--smoke-inspector" or "--smoke-camera" or "--smoke-navigation" or "--smoke-viewport" or "--smoke-native-input") continue;
            if (++index == arguments.Length || string.IsNullOrWhiteSpace(arguments[index]) ||
                arguments[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"{name} requires a value.");
            values[name] = arguments[index];
        }
        string? Value(string name) => values.GetValueOrDefault(name);
        string? FullPath(string name) => Value(name) is { } path ? Path.GetFullPath(path) : null;
        var result = new EditorLaunchOptions(FullPath("--project"), FullPath("--check-project"), FullPath("--smoke"),
            Value("--assembly"), Value("--entry"), values.ContainsKey("--light"), values.ContainsKey("--validation"), Value("--check-scene"), values.ContainsKey("--smoke-documents"), values.ContainsKey("--smoke-transform"), values.ContainsKey("--smoke-hierarchy"), values.ContainsKey("--smoke-inspector"), values.ContainsKey("--smoke-camera"), values.ContainsKey("--smoke-navigation"), values.ContainsKey("--smoke-viewport"), values.ContainsKey("--smoke-native-input"));
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
        if (result.AcceptanceSmoke || result.NativeInputSmoke)
        {
            var profiles = new[] { result.DocumentSmoke, result.TransformSmoke, result.HierarchySmoke, result.InspectorSmoke, result.CameraSmoke, result.NavigationSmoke, result.AcceptanceSmoke, result.NativeInputSmoke };
            if (profiles.Count(p => p) != 1 || result.SmokeDirectory == null || result.ProjectPath == null || result.LegacyProject || result.CheckProjectPath != null || result.CheckScenePath != null)
                throw new ArgumentException("Viewport acceptance requires one profile, --project fixture.aetherproj and --smoke fresh-directory.");
        }
        return result;
    }
}
