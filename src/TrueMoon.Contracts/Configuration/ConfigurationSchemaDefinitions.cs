using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>Creates provider-neutral data schemas without an App/bootstrap dependency.</summary>
public static class ConfigurationSchemaDefinitions
{
    /// <summary>Freezes a schema and its default template.</summary>
    /// <param name="section">Section path.</param><param name="create">Creates default data.</param>
    /// <param name="copy">Deep copy function.</param><param name="validate">Pure semantic validator.</param>
    /// <param name="fields">Field declarations.</param><returns>An independent schema.</returns>
    public static IConfigurationSchema<T> Create<T>(string section, Func<T> create, Func<T, T> copy,
        Action<T> validate, Action<IConfigurationSchemaBuilder<T>> fields) where T : class
    {
        ConfigurationMetadata.Path(section, nameof(section));
        ArgumentNullException.ThrowIfNull(create); ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(validate); ArgumentNullException.ThrowIfNull(fields);
        if (typeof(IDisposable).IsAssignableFrom(typeof(T)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(T)))
            throw new ArgumentException("Options cannot own disposable resources.", nameof(T));
        var builder = new SchemaBuilder<T>(section);
        try { fields(builder); }
        finally { builder.Frozen = true; }
        var initial = create() ?? throw new ArgumentException("Defaults cannot be null.", nameof(create));
        var template = copy(initial)
            ?? throw new ArgumentException("Copy cannot return null.", nameof(copy));
        if (ReferenceEquals(initial, template)) throw new ArgumentException("Copy must detach mutable options.", nameof(copy));
        var definitions = builder.Fields.ToArray();
        var keys = Array.AsReadOnly(definitions.Select(field => field.Key).ToArray());
        ConfigurationMetadata.Keys(keys);
        var defaults = Array.AsReadOnly(definitions.Select(field => new ConfigurationEntry(field.Key.Path, field.Default(template))).ToArray());
        return new Schema<T>(section, template, copy, validate, definitions, keys, defaults);
    }

    private sealed record SchemaField<T>(ConfigurationKey Key, Func<T, JsonElement> Default,
        Action<T, IConfigurationSnapshot> Assign, Func<T, bool> Empty);

    private sealed class SchemaBuilder<T>(string section) : IConfigurationSchemaBuilder<T> where T : class
    {
        internal bool Frozen;
        internal readonly List<SchemaField<T>> Fields = [];
        public void Field<TValue>(string relativePath, Func<T, TValue> get, Action<T, TValue> set,
            ConfigurationField? field = null, IConfigurationValueCodec<TValue>? codec = null)
        {
            if (Frozen) throw new InvalidOperationException("The schema is frozen.");
            ConfigurationMetadata.Path(relativePath, nameof(relativePath));
            ArgumentNullException.ThrowIfNull(get); ArgumentNullException.ThrowIfNull(set);
            if (typeof(Delegate).IsAssignableFrom(typeof(TValue)) || typeof(IDisposable).IsAssignableFrom(typeof(TValue)) ||
                typeof(IAsyncDisposable).IsAssignableFrom(typeof(TValue)))
                throw new ArgumentException("A field cannot contain services or delegates.", nameof(TValue));
            if (codec is null && !ConfigurationCodecs.Supported(typeof(TValue)))
                throw new ArgumentException("An explicit codec is required for this field type.", nameof(codec));
            var boxed = ConfigurationCodecs.Box(codec);
            var key = new ConfigurationKey(section + ":" + relativePath, typeof(TValue), field ?? new()) { Codec = boxed };
            Fields.Add(new(key, value => boxed.Write(get(value)), (value, snapshot) =>
            {
                var read = snapshot.Read<TValue>(key.Path);
                if (read.State != ConfigurationReadState.Missing) set(value, read.Value!);
            }, value => get(value) is null || get(value) is string text && string.IsNullOrWhiteSpace(text)));
        }
    }

    private sealed class Schema<T>(string section, T template, Func<T, T> copy, Action<T> validate,
        SchemaField<T>[] fields, IReadOnlyList<ConfigurationKey> keys, IReadOnlyList<ConfigurationEntry> defaults) : IConfigurationSchema<T> where T : class
    {
        public string Section => section;
        public IReadOnlyList<ConfigurationKey> Keys => keys;
        public IReadOnlyList<ConfigurationEntry> GetDefaults() => defaults;
        public T Copy(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var result = copy(value) ?? throw new InvalidOperationException("Copy returned null.");
            if (ReferenceEquals(result, value)) throw new InvalidOperationException("Copy must detach mutable options.");
            return result;
        }
        public T Bind(IConfigurationSnapshot snapshot, Action<T>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            try
            {
                var value = Copy(template);
                foreach (var field in fields) field.Assign(value, snapshot);
                try { configure?.Invoke(value); }
                catch (Exception)
                { throw ConfigurationMetadata.Error("OptionsValidation", section); }
                foreach (var field in fields)
                    if (field.Key.Field.Required && field.Empty(value)) throw ConfigurationMetadata.Error("RequiredValue", field.Key.Path);
                try { validate(value); }
                catch (Exception)
                { throw ConfigurationMetadata.Error("OptionsValidation", section); }
                return Copy(value);
            }
            catch (ConfigurationException error) { throw ConfigurationMetadata.BindingError(error, keys, section); }
            catch (Exception)
            { throw ConfigurationMetadata.Error("OptionsValidation", section); }
        }
    }
}
