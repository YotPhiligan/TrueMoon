using System.Text.Json;
using TrueMoon.Configuration;

namespace TrueMoon.Mithril;

/// <summary>Minimal pre-App reader using the common parser, profile, conversion and validation.</summary>
public static class UnitBootstrap
{
    private static readonly string[] IdentityNames = ["-u", "-p", UnitParentProcessEventsHandler.ControlPipeKey];
    private static readonly string[] IdentityPaths = ["Mithril:UnitId", "Mithril:ParentProcessId", "Mithril:ControlPipe"];
    private const string DisabledEnvironment = "TRUEMOON_MITHRIL__DISABLED";
    private static readonly IConfigurationSchema<Draft> Schema = ConfigurationSchemas.Create("Mithril", () => new Draft(),
        value => value with { }, value => { _ = value.Freeze(); }, fields =>
        {
            fields.Field("UnitId", value => value.UnitId, (value, next) => value.UnitId = next, new(Aliases: ["-u"]));
            fields.Field("ParentProcessId", value => value.ParentProcessId, (value, next) => value.ParentProcessId = next, new(Aliases: ["-p"]));
            fields.Field("ControlPipe", value => value.ControlPipe, (value, next) => value.ControlPipe = next, new(Sensitive: true));
            fields.Field("Disabled", value => value.Disabled, (value, next) => value.Disabled = next,
                new(Aliases: ["-ud"], Argument: ConfigurationArgumentKind.Flag));
        });

    /// <summary>Parses selected launch fields without building App/DI or reading the OS.</summary>
    /// <param name="arguments">Arguments excluding the executable; unrelated arguments remain unconsumed.</param>
    /// <param name="environment">Explicit selected environment snapshot; null values mean absent.</param>
    /// <returns>A validated immutable launch snapshot. An environment identity group excludes all CLI identity fields.</returns>
    public static UnitLaunchSettings Parse(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(environment);
        return Bind(ParseEntries(arguments), environment);
    }

    private static IReadOnlyList<ConfigurationEntry> ParseEntries(IReadOnlyList<string> arguments)
    {
        var normalized = arguments.Select(argument => argument is "-ud=1" ? "-ud=true" : argument is "-ud=0" ? "-ud=false" :
            argument?.StartsWith(UnitParentProcessEventsHandler.ControlPipeKey + "=", StringComparison.Ordinal) == true
                ? "--Mithril:ControlPipe=" + argument[(UnitParentProcessEventsHandler.ControlPipeKey.Length + 1)..] : argument!).ToArray();
        return ConfigurationCommandLine.ParseKnown(normalized, Schema.Keys).Entries;
    }

    private static UnitLaunchSettings Bind(IReadOnlyList<ConfigurationEntry> entries, IReadOnlyDictionary<string, string?> environment)
    {
        var protectedGroup = IdentityNames.Any(name => environment.TryGetValue(name, out var value) && value is not null);
        var input = entries.Where(entry => !protectedGroup || !IdentityPaths.Contains(entry.Path, StringComparer.Ordinal)).ToArray();
        var identity = new List<ConfigurationEntry>();
        for (var index = 0; index < IdentityNames.Length; index++)
            if (environment.TryGetValue(IdentityNames[index], out var value) && value is not null)
                identity.Add(new(IdentityPaths[index], JsonSerializer.SerializeToElement(value)));
        var controls = new List<ConfigurationEntry>();
        if (environment.TryGetValue(DisabledEnvironment, out var disabled) && disabled is not null)
            controls.Add(new("Mithril:Disabled", JsonSerializer.SerializeToElement(disabled)));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema);
            builder.AddValues("mithril-environment", ConfigurationSourcePriority.Environment, controls);
            builder.AddValues("mithril-arguments", ConfigurationSourcePriority.Arguments, input);
            // This is a separate protected profile, never mixed with normal App JSON/env/CLI sources.
            builder.AddValues("mithril-launch", ConfigurationSourcePriority.Overrides, identity);
        });
        return Schema.Bind(profile.LoadAsync().GetAwaiter().GetResult()).Freeze();
    }

    /// <summary>Captures only supported legacy aliases from env/args/default sections; no arbitrary object-bag import.</summary>
    /// <param name="configuration">Captured legacy inputs. Explicit default identity remains a composition compatibility adapter.</param>
    /// <returns>A detached launch snapshot; later Set/Refresh does not affect it.</returns>
    public static UnitLaunchSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        try
        {
            var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
            var env = Input(ConfigurationSectionNames.EnvironmentVariables);
            foreach (var name in IdentityNames.Append(DisabledEnvironment))
                if (env?.Exist(name) == true) environment[name] = env.Get<string>(name);
            var args = Input(ConfigurationSectionNames.CommandLineArguments);
            var defaults = Input(ConfigurationSectionNames.Default);
            var values = new Dictionary<string, ConfigurationEntry>(StringComparer.OrdinalIgnoreCase);
            AppendIdentity(defaults);
            if (args is CommandLineArgsSection { Arguments: { } captured })
                foreach (var entry in ParseEntries(captured)) values[entry.Path] = entry;
            AppendIdentity(args);
            foreach (var name in new[] { "-ud", "--Mithril:Disabled" })
                if (args?.Exist(name) == true && IsOverride(args, name))
                {
                    var value = args.Get<string>(name);
                    values["Mithril:Disabled"] = new("Mithril:Disabled", JsonSerializer.SerializeToElement(value is "1" ? "true" : value is "0" or null ? "false" : value));
                }
            return Bind(values.Values.ToArray(), environment);
            void AppendIdentity(IConfigurationSection? source)
            {
                foreach (var (name, path) in IdentityNames.Zip(IdentityPaths).Concat(IdentityPaths.Select(path => ("--" + path, path))))
                    if (source?.Exist(name) == true && IsOverride(source, name))
                    {
                        var value = source.Get<string>(name);
                        values[path] = new(path, JsonSerializer.SerializeToElement(name == "-u" && string.IsNullOrWhiteSpace(value) ? null : value));
                    }
            }
            bool IsOverride(IConfigurationSection source, string name) => source is not CommandLineArgsSection { Arguments: not null } capturedSection || capturedSection.IsArgumentOverridden(name);
            IConfigurationSection? Input(string section) => configuration is IConfigurationBootstrapInputs bootstrap
                ? bootstrap.GetBootstrapSection(section) : configuration.GetSection(section);
        }
        catch (ConfigurationException) { throw; }
        catch (Exception) { throw new ConfigurationException([new("BootstrapInput", null, "mithril-launch", "Unit launch input is invalid.")]); }
    }

    private sealed record Draft
    {
        public int? UnitId { get; set; }
        public int? ParentProcessId { get; set; }
        public string? ControlPipe { get; set; }
        public bool Disabled { get; set; }
        internal UnitLaunchSettings Freeze() => new(UnitId, ParentProcessId, ControlPipe, Disabled);
    }
}
