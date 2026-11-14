using TrueMoon.Configuration;

namespace TrueMoon.Aether.Runtime.Hosting;

/// <summary>Frozen public launch inputs. Parsing opens no files, loads no game and creates no App.</summary>
public sealed record AetherLaunchOptions(AetherLaunchRole Role, bool Help, string? ArtifactPath, string? ScenePath,
    bool LegacyScene, string? ProjectPath, string? RuntimeHostPath, string? FontPath, string? ReportPath,
    int Width, int Height, string Title, bool Validation, bool ConsoleDiagnostics)
{
    private sealed class Data
    {
        public bool Editor, Player, Runtime, Help, Validation, ConsoleDiagnostics;
        public string? Artifact, Scene, Project, RuntimeHost, Font, Report;
        public string SceneFormat = "world", Title = "Aether Player";
        public int Width = 960, Height = 540;
        public Data Copy() => (Data)MemberwiseClone();
    }
    private static readonly IConfigurationSchema<Data> Schema = ConfigurationSchemaDefinitions.Create("AetherLaunch", () => new Data(), value => value.Copy(), _ => { }, fields =>
    {
        fields.Field("Editor", x => x.Editor, (x,v) => x.Editor=v, new(Aliases:["--editor"],Argument:ConfigurationArgumentKind.Flag));
        fields.Field("Player", x => x.Player, (x,v) => x.Player=v, new(Aliases:["--player"],Argument:ConfigurationArgumentKind.Flag));
        fields.Field("Runtime", x => x.Runtime, (x,v) => x.Runtime=v, new(Aliases:["--runtime"],Argument:ConfigurationArgumentKind.Flag));
        fields.Field("Help", x => x.Help, (x,v) => x.Help=v, new(Aliases:["--help"],Argument:ConfigurationArgumentKind.Flag));
        fields.Field("Artifact", x => x.Artifact, (x,v) => x.Artifact=v, new(Aliases:["--artifact"]));
        fields.Field("Scene", x => x.Scene, (x,v) => x.Scene=v, new(Aliases:["--scene"]));
        fields.Field("SceneFormat", x => x.SceneFormat, (x,v) => x.SceneFormat=v, new(Aliases:["--scene-format"]));
        fields.Field("Project", x => x.Project, (x,v) => x.Project=v, new(Aliases:["--project"]));
        fields.Field("RuntimeHost", x => x.RuntimeHost, (x,v) => x.RuntimeHost=v, new(Aliases:["--runtime-host"]));
        fields.Field("Font", x => x.Font, (x,v) => x.Font=v, new(Aliases:["--font"]));
        fields.Field("Report", x => x.Report, (x,v) => x.Report=v, new(Aliases:["--report"]));
        fields.Field("Width", x => x.Width, (x,v) => x.Width=v, new(Aliases:["--width"]));
        fields.Field("Height", x => x.Height, (x,v) => x.Height=v, new(Aliases:["--height"]));
        fields.Field("Title", x => x.Title, (x,v) => x.Title=v, new(Aliases:["--title"]));
        fields.Field("Validation", x => x.Validation, (x,v) => x.Validation=v, new(Aliases:["--validation"],Argument:ConfigurationArgumentKind.Flag));
        fields.Field("ConsoleDiagnostics", x => x.ConsoleDiagnostics, (x,v) => x.ConsoleDiagnostics=v, new(Aliases:["--console-diagnostics"],Argument:ConfigurationArgumentKind.Flag));
    });
    /// <summary>Uses the standard single-use CLI parser/schema. Reserved bootstrap presence prevents user-role fallback.</summary>
    /// <param name="arguments">Explicit tokens excluding executable.</param><param name="defaultRole">Editor for combined hosts, Player for Player-only hosts.</param>
    /// <param name="hasRuntimeBootstrap">Presence of captured protected child identity, including malformed/empty values.</param>
    /// <param name="token">Cancellation before publication.</param><returns>Validated detached options.</returns>
    public static async Task<AetherLaunchOptions> ParseAsync(IReadOnlyList<string> arguments, AetherLaunchRole defaultRole = AetherLaunchRole.Editor,
        bool hasRuntimeBootstrap = false, CancellationToken token = default)
    {
        if (defaultRole is not (AetherLaunchRole.Editor or AetherLaunchRole.Player)) throw new ArgumentOutOfRangeException(nameof(defaultRole));
        var parsed = ConfigurationCommandLine.ParseSingleUse(arguments, Schema.Keys);
        if (parsed.Positionals.Count != 0) throw new ArgumentException("Unexpected launch arguments.", nameof(arguments));
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(Schema); builder.AddValues("launch-arguments", ConfigurationSourcePriority.Arguments, parsed.Entries); });
        var data = Schema.Bind(await profile.LoadAsync(token).ConfigureAwait(false));
        if (new[] { data.Editor, data.Player, data.Runtime }.Count(value => value) > 1) throw new ArgumentException("Choose one launch role.", nameof(arguments));
        foreach (var entry in parsed.Entries.Where(entry => entry.Path is "AetherLaunch:Editor" or "AetherLaunch:Player" or "AetherLaunch:Runtime"))
            if (entry.Value.ToString().Equals("false", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Role selectors cannot be false.", nameof(arguments));
        var role = data.Runtime ? AetherLaunchRole.RuntimeChild : data.Player ? AetherLaunchRole.Player : data.Editor ? AetherLaunchRole.Editor : defaultRole;
        bool Has(string key) => parsed.Entries.Any(entry => entry.Path == "AetherLaunch:" + key);
        if (hasRuntimeBootstrap && role != AetherLaunchRole.RuntimeChild) throw new InvalidDataException("Protected runtime identity cannot launch a user role.");
        if (role == AetherLaunchRole.RuntimeChild && !hasRuntimeBootstrap && !data.Help) throw new InvalidDataException("Runtime child requires a protected bootstrap.");
        if (role == AetherLaunchRole.RuntimeChild && parsed.Entries.Any(entry => entry.Path is not ("AetherLaunch:Runtime" or "AetherLaunch:Help" or "AetherLaunch:ConsoleDiagnostics")))
            throw new ArgumentException("Runtime child accepts only protected bootstrap and diagnostic flags.", nameof(arguments));
        if (role != AetherLaunchRole.RuntimeChild && data.ConsoleDiagnostics) throw new ArgumentException("Console diagnostics is a runtime-child flag.", nameof(arguments));
        if (role == AetherLaunchRole.Editor && new[] { "Artifact", "Scene", "SceneFormat", "Width", "Height", "Title", "Validation" }.Any(Has))
            throw new ArgumentException("Player inputs cannot configure the Editor role.", nameof(arguments));
        if (role == AetherLaunchRole.Player && (data.Project != null || data.RuntimeHost != null)) throw new ArgumentException("Player does not accept Editor/project/runtime-child inputs.", nameof(arguments));
        if (role == AetherLaunchRole.Player && !data.Help && (string.IsNullOrWhiteSpace(data.Artifact) || string.IsNullOrWhiteSpace(data.Scene)))
            throw new ArgumentException("Player requires --artifact and --scene.", nameof(arguments));
        if (data.SceneFormat is not ("world" or "snapshot")) throw new ArgumentException("Scene format must be world or snapshot.", nameof(arguments));
        if (data.Width is < 16 or > 1920 || data.Height is < 16 or > 1080 || string.IsNullOrWhiteSpace(data.Title) || data.Title.Length > 256)
            throw new ArgumentException("Invalid Player window dimensions/title.", nameof(arguments));
        string? PathValue(string? value) => value == null ? null : !string.IsNullOrWhiteSpace(value) && !value.Contains('\0') ? Path.GetFullPath(value) : throw new ArgumentException("Invalid launch path.");
        return new(role, data.Help, PathValue(data.Artifact), PathValue(data.Scene), data.SceneFormat == "snapshot", PathValue(data.Project), PathValue(data.RuntimeHost),
            PathValue(data.Font), PathValue(data.Report), data.Width, data.Height, data.Title, data.Validation, data.ConsoleDiagnostics);
    }
}
