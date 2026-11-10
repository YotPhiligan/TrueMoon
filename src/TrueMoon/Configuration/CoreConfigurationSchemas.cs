using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>Standard Core schemas and bounded compatibility imports.</summary>
public static class CoreConfigurationSchemas
{
    /// <summary>Application name. Use this exact schema instance with UseCoreConfiguration.</summary>
    public static IConfigurationSchema<AppSettings> App { get; } = ConfigurationSchemas.Create("App", () => new AppSettings(),
        value => value with { }, value =>
        {
            if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Any(char.IsControl)) throw new ArgumentException("Invalid application name.");
        }, fields => fields.Field("Name", value => value.Name, (value, name) => value.Name = name, new(Required: true)));

    /// <summary>Product root; OS metadata and the secrets folder remain system facts.</summary>
    public static IConfigurationSchema<PathSettings> Paths { get; } = ConfigurationSchemas.Create("Paths", () => new PathSettings(),
        value => value with { }, value =>
        {
            if (value.Root is not null)
            {
                if (string.IsNullOrWhiteSpace(value.Root)) throw new ArgumentException("Invalid product root.");
                value.Root = Path.GetFullPath(value.Root);
            }
        }, fields => fields.Field("Root", value => value.Root, (value, root) => value.Root = root, new(Aliases: ["-root"])));

    /// <summary>Adds both Core schemas without selecting any implicit source.</summary>
    /// <param name="builder">Profile builder.</param>
    public static void AddSchemas(this IConfigurationProfileBuilder builder)
    { ArgumentNullException.ThrowIfNull(builder); builder.AddSchema(App); builder.AddSchema(Paths); }

    /// <summary>Captures only legacy appName and command-line -root with standard source levels.</summary>
    /// <param name="builder">Profile builder containing both Core schemas.</param><param name="legacy">Already captured legacy inputs.</param>
    public static void AddLegacySources(this IConfigurationProfileBuilder builder, IConfiguration legacy)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(legacy);
        Add(ConfigurationSectionNames.Default, ConfigurationSourcePriority.Defaults);
        Add(ConfigurationSectionNames.EnvironmentVariables, ConfigurationSourcePriority.Environment);
        Add(ConfigurationSectionNames.CommandLineArguments, ConfigurationSourcePriority.Arguments);
        void Add(string section, ConfigurationSourcePriority priority)
        {
            var entries = new List<ConfigurationEntry>();
            var input = legacy.GetSection(section);
            var original = input as CommandLineArgsSection ?? (legacy as IConfigurationBootstrapInputs)?.GetBootstrapSection(section) as CommandLineArgsSection;
            if (section == ConfigurationSectionNames.CommandLineArguments && original is { Arguments: { } captured })
            {
                var tokens = captured.Select(argument => argument.StartsWith("appName=", StringComparison.Ordinal)
                    ? "--App:Name=" + argument[8..] : argument).ToArray();
                var parsed = ConfigurationCommandLine.ParseKnown(tokens, App.Keys.Concat(Paths.Keys).ToArray());
                var values = parsed.Entries.ToDictionary(entry => entry.Path, ConfigurationMetadata.Names);
                foreach (var (alias, path) in new[] { ("appName", "App:Name"), ("--App:Name", "App:Name"), ("-root", "Paths:Root"), ("--Paths:Root", "Paths:Root") })
                    if (input?.Exist(alias) == true && (original.IsArgumentOverridden(alias) || !Equals(input.Get<object>(alias), original.Get<object>(alias))))
                        values[path] = new(path, JsonSerializer.SerializeToElement(input.Get<string>(alias)));
                entries.AddRange(values.Values);
            }
            else
            {
                if (input?.Exist("appName") == true) entries.Add(new("App:Name", JsonSerializer.SerializeToElement(input.Get<string>("appName"))));
                if (section == ConfigurationSectionNames.CommandLineArguments && input?.Exist("-root") == true)
                    entries.Add(new("Paths:Root", JsonSerializer.SerializeToElement(input.Get<string>("-root"))));
            }
            builder.AddValues("core-legacy-" + section, priority, entries);
        }
    }
}
