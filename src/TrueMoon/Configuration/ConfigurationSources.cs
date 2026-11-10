namespace TrueMoon.Configuration;

/// <summary>Adds standard sources to an opt-in unified profile.</summary>
public static class ConfigurationSources
{
    /// <summary>Adds a detached, explicit adapter snapshot without reading the OS or a legacy object bag on reload.</summary>
    /// <param name="builder">Profile builder.</param><param name="id">Safe source identifier.</param>
    /// <param name="priority">Explicit source level.</param><param name="entries">Values to copy.</param>
    public static void AddValues(this IConfigurationProfileBuilder builder, string id, ConfigurationSourcePriority priority,
        IReadOnlyList<ConfigurationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(entries);
        var copied = Array.AsReadOnly(entries.Select(entry => new ConfigurationEntry(entry.Path, entry.Value)).ToArray());
        builder.AddSource(id, priority, new ValuesSource(copied));
    }

    private sealed class ValuesSource(IReadOnlyList<ConfigurationEntry> entries) : IConfigurationSource
    {
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(entries); }
    }

    /// <summary>Adds one explicit JSON file at file priority.</summary>
    /// <param name="builder">Profile builder.</param><param name="id">Safe source identifier.</param>
    /// <param name="absolutePath">Absolute file path.</param><param name="optional">Skip only absence.</param>
    public static void AddJsonFile(this IConfigurationProfileBuilder builder, string id, string absolutePath, bool optional = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddSource(id, ConfigurationSourcePriority.Files, new ConfigurationJsonSource(absolutePath, optional));
    }

    /// <summary>Adds prefixed environment values at environment priority.</summary>
    /// <param name="builder">Profile builder.</param><param name="id">Safe source identifier.</param>
    /// <param name="prefix">Nonempty application prefix.</param><param name="allowlist">Optional logical key allowlist.</param>
    public static void AddEnvironment(this IConfigurationProfileBuilder builder, string id, string prefix, IReadOnlyList<string>? allowlist = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddSource(id, ConfigurationSourcePriority.Environment, new ConfigurationEnvironmentSource(prefix, allowlist));
    }

    /// <summary>Adds copied settings-only arguments at argument priority.</summary>
    /// <param name="builder">Profile builder.</param><param name="id">Safe source identifier.</param>
    /// <param name="arguments">Arguments without the executable. Positionals require standalone Parse.</param>
    public static void AddArguments(this IConfigurationProfileBuilder builder, string id, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(arguments);
        builder.AddSource(id, ConfigurationSourcePriority.Arguments, new ArgumentsSource(arguments.ToArray()));
    }

    private sealed class ArgumentsSource(string[] arguments) : IConfigurationSource
    {
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = ConfigurationCommandLine.Parse(arguments, keys);
            if (parsed.Positionals.Count != 0) throw ConfigurationMetadata.Error("UnexpectedPositional");
            return ValueTask.FromResult(parsed.Entries);
        }
    }
}
