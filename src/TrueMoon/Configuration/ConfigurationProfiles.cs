using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>Creates explicitly selected unified configuration profiles.</summary>
public static class ConfigurationProfiles
{
    /// <summary>Freezes source order, schemas, defaults and overrides.</summary>
    /// <param name="configure">Composition callback.</param><returns>Data-only profile.</returns>
    public static IConfigurationProfile Create(Action<IConfigurationProfileBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ProfileBuilder();
        try { configure(builder); return builder.Build(); }
        finally { builder.Frozen = true; }
    }

    private sealed record Source(string Id, ConfigurationSourcePriority Priority, int Order, IConfigurationSource Input);

    internal interface ITrustedProfile { }

    private sealed class ProfileBuilder : IConfigurationProfileBuilder
    {
        internal bool Frozen;
        private readonly List<ConfigurationKey> _keys = [];
        private readonly List<ConfigurationEntry> _defaults = [];
        private readonly List<Source> _sources = [];
        private readonly Dictionary<string, ConfigurationEntry> _overrides = new(ConfigurationMetadata.Names);
        private readonly HashSet<string> _sections = new(ConfigurationMetadata.Names);
        private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
        private readonly List<Action<IConfigurationSchemaVisitor>> _schemas = [];

        private void Mutable() { if (Frozen) throw new InvalidOperationException("The profile is frozen."); }
        public void AddSchema<T>(IConfigurationSchema<T> schema) where T : class
        {
            Mutable(); ArgumentNullException.ThrowIfNull(schema);
            ConfigurationMetadata.Path(schema.Section);
            if (_sections.Contains(schema.Section)) throw new ArgumentException("Duplicate schema section.", nameof(schema));
            var candidates = _keys.Concat(schema.Keys).ToArray();
            ConfigurationMetadata.Keys(candidates);
            var defaults = schema.GetDefaults().Select(entry => new ConfigurationEntry(entry.Path, entry.Value)).ToArray();
            _sections.Add(schema.Section);
            _keys.AddRange(schema.Keys);
            _defaults.AddRange(defaults);
            _schemas.Add(visitor => visitor.Visit(schema));
        }
        public void AddSource(string id, ConfigurationSourcePriority priority, IConfigurationSource source)
        {
            Mutable(); ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentNullException.ThrowIfNull(source);
            if (id.StartsWith('$') || id.Any(char.IsControl) || _ids.Contains(id)) throw new ArgumentException("Invalid or duplicate source identifier.", nameof(id));
            if (!Enum.IsDefined(priority)) throw new ArgumentException("Unknown source level.", nameof(priority));
            _ids.Add(id);
            _sources.Add(new(id, priority, _sources.Count, source));
        }
        public void Override<T>(string path, T value, IConfigurationValueCodec<T>? codec = null)
        {
            Mutable(); ConfigurationMetadata.Path(path);
            try { _overrides[path] = new(path, codec is null ? JsonSerializer.SerializeToElement(value) : codec.Write(value)); }
            catch (Exception)
            { throw ConfigurationMetadata.Error("InvalidOverride"); }
        }
        internal IConfigurationProfile Build()
        {
            var keys = Array.AsReadOnly(_keys.ToArray());
            var map = ConfigurationMetadata.Keys(keys);
            var sources = new[] { new Source("$defaults", ConfigurationSourcePriority.Defaults, -1, new EntriesSource(_defaults.ToArray())) }
                .Concat(_sources)
                .Append(new("$overrides", ConfigurationSourcePriority.Overrides, int.MaxValue, new EntriesSource(_overrides.Values.ToArray())))
                .OrderBy(source => source.Priority).ThenBy(source => source.Order).ToArray();
            return new Profile(keys, map, sources, _schemas.ToArray());
        }
    }

    private sealed class EntriesSource(ConfigurationEntry[] entries) : IConfigurationSource
    {
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(entries); }
    }

    private sealed class Profile(IReadOnlyList<ConfigurationKey> keys, Dictionary<string, ConfigurationKey> map,
        Source[] sources, Action<IConfigurationSchemaVisitor>[] schemas) : IConfigurationProfile, IConfigurationProfileSchemas, IConfigurationProfileWriter, ITrustedProfile
    {
        public async ValueTask WriteAsync(string sourceId, IReadOnlyList<ConfigurationEdit> edits, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sourceId);
            var source = sources.FirstOrDefault(source => StringComparer.Ordinal.Equals(source.Id, sourceId));
            if (source is null) throw ConfigurationMetadata.Error("UnknownSource");
            if (source.Input is not IConfigurationMutableSource mutable) throw ConfigurationMetadata.Error("ReadOnlySource", source: source.Id);
            try
            {
                var patch = ConfigurationPatches.Validate(map, ConfigurationPatches.Copy(edits));
                cancellationToken.ThrowIfCancellationRequested();
                await mutable.WriteAsync(keys, patch, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
            catch (ConfigurationException error)
            {
                string[] codes = ["UnknownKey", "DuplicateKey", "InvalidValue", "UndefinedValue", "InvalidEdit"];
                throw new ConfigurationException(error.Issues.Select(issue => new ConfigurationIssue(
                    codes.Contains(issue.Code, StringComparer.Ordinal) ? issue.Code : "SourceWrite",
                    issue.Path is { } path && map.TryGetValue(path, out var key) ? key.Path : null,
                    source.Id, "Configuration source could not be written.")).ToArray());
            }
            catch (Exception) { throw ConfigurationMetadata.Error("SourceWrite", source: source.Id); }
        }

        public void VisitSchemas(IConfigurationSchemaVisitor visitor)
        {
            ArgumentNullException.ThrowIfNull(visitor);
            foreach (var schema in schemas) schema(visitor);
        }
        public async ValueTask<IConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            var values = new Dictionary<string, (JsonElement Value, ConfigurationOrigin Origin)>(ConfigurationMetadata.Names);
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var entries = await source.Input.ReadAsync(keys, cancellationToken).ConfigureAwait(false);
                    var seen = new HashSet<string>(ConfigurationMetadata.Names);
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (entry is null || entry.Path is null || !map.TryGetValue(entry.Path, out var key))
                            throw ConfigurationMetadata.Error("UnknownKey");
                        if (!seen.Add(key.Path)) throw ConfigurationMetadata.Error("DuplicateKey", key.Path);
                        if (entry.Value.ValueKind == JsonValueKind.Undefined) throw ConfigurationMetadata.Error("UndefinedValue", key.Path);
                        values[key.Path] = (entry.Value.Clone(), new(source.Id, source.Priority, source.Order));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                { throw new OperationCanceledException(cancellationToken); }
                catch (ConfigurationException error) { throw ConfigurationMetadata.SourceError(error, map, source.Id); }
                catch (Exception) { throw ConfigurationMetadata.Error("SourceRead", source: source.Id); }
            }
            foreach (var (path, entry) in values)
            {
                try { ConfigurationMetadata.ValidateValue(map[path], entry.Value); }
                catch (ConfigurationException error)
                { throw new ConfigurationException(error.Issues.Select(issue => issue with { SourceId = entry.Origin.SourceId }).ToArray()); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new ConfigurationSnapshot(map, values);
        }
    }
}
