namespace TrueMoon.Configuration;

internal sealed class ConfigurationStage
{
    private IConfigurationProfile? _profile;
    private readonly List<IModule> _modules = [];
    private bool _frozen;
    private Catalog? _catalog;
    private Func<IConfigurationSnapshot, ConfigurationObservation>[] _observations = [];
    internal bool HasRequests => _profile is not null || _modules.Count != 0;

    private void Mutable() { if (_frozen) throw new InvalidOperationException("The App configuration stage is frozen."); }
    internal void Use(IConfigurationProfile profile)
    {
        Mutable(); ArgumentNullException.ThrowIfNull(profile);
        if (_profile is not null) throw new InvalidOperationException("One configuration profile is allowed per App.");
        if (profile is not IConfigurationProfileSchemas) throw new NotSupportedException("The profile has no schema catalog for App composition.");
        _profile = profile;
    }
    internal void Add<T>(IConfigurationSchema<T> schema, Action<IAppConfigurationContext, T> register, Action<T>? configure) where T : class
    {
        Mutable(); ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(register);
        if (_modules.Any(module => ConfigurationMetadata.Names.Equals(module.Section, schema.Section)))
            throw new InvalidOperationException("A schema section has already been configured.");
        _modules.Add(new Module<T>(schema, register, configure));
    }

    internal void Freeze() => _frozen = true;

    internal (BoundConfigurationSnapshot Snapshot, Action<IAppConfigurationContext>[] Registrations) Prepare()
    {
        Freeze();
        var profile = _profile ?? throw new InvalidOperationException("ConfigureModule requires UseConfiguration.");
        var catalog = new Catalog();
        ((IConfigurationProfileSchemas)profile).VisitSchemas(catalog);
        ConfigurationMetadata.Keys(catalog.Keys.ToArray());
        foreach (var module in _modules)
            if (!catalog.Schemas.Any(schema => ReferenceEquals(schema.Identity, module.Identity) && schema.ValueType == module.ValueType))
                throw new ArgumentException("ConfigureModule must use the exact registered schema instance.", "schema");

        _catalog = catalog;
        _observations = catalog.Keys.Select(ConfigurationObservation.Reader).ToArray();

        IConfigurationSnapshot raw;
        try
        {
            // Only data loading moves off the caller context; composition stays on its owner thread.
            raw = Task.Run(async () => await profile.LoadAsync().ConfigureAwait(false)).GetAwaiter().GetResult()
                ?? throw ConfigurationMetadata.Error("ProfileLoad");
            if (raw.Generation != 1) throw ConfigurationMetadata.Error("ProfileSnapshot");
        }
        catch (Exception) when (profile is not ConfigurationProfiles.ITrustedProfile)
        { throw ConfigurationMetadata.Error("ProfileLoad"); }

        var snapshot = Bind(raw);
        // Prepare every registration copy before invoking any caller composition callback.
        var registrations = _modules.Select(module => module.PrepareRegistration(snapshot.Options[module.Identity])).ToArray();
        return (snapshot, registrations);
    }

    internal IReadOnlyList<ConfigurationKey> Keys => _catalog!.Keys;

    internal async ValueTask<BoundConfigurationSnapshot> ReloadAsync(CancellationToken token)
    {
        IConfigurationSnapshot raw;
        try
        {
            raw = await _profile!.LoadAsync(token).ConfigureAwait(false) ?? throw ConfigurationMetadata.Error("ProfileLoad");
            if (raw.Generation != 1) throw ConfigurationMetadata.Error("ProfileSnapshot");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception) when (_profile is not ConfigurationProfiles.ITrustedProfile) { throw ConfigurationMetadata.Error("ProfileLoad"); }
        token.ThrowIfCancellationRequested();
        return Bind(raw);
    }

    internal ValueTask WriteAsync(string sourceId, IReadOnlyList<ConfigurationEdit> edits, CancellationToken token)
    {
        if (_profile is not IConfigurationProfileWriter writer) throw ConfigurationMetadata.Error("ReadOnlyProfile");
        return WriteCoreAsync(writer, sourceId, edits, token);
    }

    private async ValueTask WriteCoreAsync(IConfigurationProfileWriter writer, string sourceId,
        IReadOnlyList<ConfigurationEdit> edits, CancellationToken token)
    {
        try
        {
            var validated = _profile is ConfigurationProfiles.ITrustedProfile ? edits : ConfigurationPatches.Validate(ConfigurationMetadata.Keys(Keys), edits);
            await writer.WriteAsync(sourceId, validated, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception) when (_profile is not ConfigurationProfiles.ITrustedProfile) { throw ConfigurationMetadata.Error("SourceWrite"); }
    }

    private BoundConfigurationSnapshot Bind(IConfigurationSnapshot raw)
    {
        var options = new Dictionary<object, BoundConfigurationOptions>(ReferenceEqualityComparer.Instance);
        foreach (var schema in _catalog!.Schemas)
        {
            var module = _modules.FirstOrDefault(module => ReferenceEquals(module.Identity, schema.Identity));
            options.Add(schema.Identity, schema.Bind(raw, module));
        }
        return new BoundConfigurationSnapshot(raw, options, _observations.Select(read => read(raw)).ToArray());
    }

    private interface IModule
    {
        object Identity { get; }
        Type ValueType { get; }
        string Section { get; }
        Action<IAppConfigurationContext> PrepareRegistration(BoundConfigurationOptions options);
    }

    private sealed class Module<T>(IConfigurationSchema<T> schema, Action<IAppConfigurationContext, T> register,
        Action<T>? configure) : IModule where T : class
    {
        public object Identity => schema;
        public Type ValueType => typeof(T);
        public string Section => schema.Section;
        internal Action<T>? Configure => configure;
        public Action<IAppConfigurationContext> PrepareRegistration(BoundConfigurationOptions options)
        {
            var copy = (T)options.Copy();
            return context => register(context, copy);
        }
    }

    private interface ISchema
    {
        object Identity { get; }
        Type ValueType { get; }
        BoundConfigurationOptions Bind(IConfigurationSnapshot snapshot, IModule? module);
    }

    private sealed class Schema<T>(IConfigurationSchema<T> schema) : ISchema where T : class
    {
        public object Identity => schema;
        public Type ValueType => typeof(T);
        public BoundConfigurationOptions Bind(IConfigurationSnapshot snapshot, IModule? module)
        {
            try
            {
                var value = schema.Bind(snapshot, (module as Module<T>)?.Configure)
                    ?? throw ConfigurationMetadata.Error("OptionsValidation", schema.Section);
                var owned = new BoundConfigurationOptions<T>(schema, value);
                // Detach a custom schema's returned value before storing it privately.
                return new BoundConfigurationOptions<T>(schema, (T)owned.Copy());
            }
            catch (ConfigurationException error) { throw ConfigurationMetadata.BindingError(error, schema.Keys, schema.Section); }
            catch (Exception) { throw ConfigurationMetadata.Error("OptionsValidation", schema.Section); }
        }
    }

    private sealed class Catalog : IConfigurationSchemaVisitor
    {
        internal readonly List<ISchema> Schemas = [];
        internal readonly List<ConfigurationKey> Keys = [];
        private readonly HashSet<string> _sections = new(ConfigurationMetadata.Names);
        public void Visit<T>(IConfigurationSchema<T> schema) where T : class
        {
            ArgumentNullException.ThrowIfNull(schema);
            ConfigurationMetadata.Path(schema.Section);
            if (typeof(IDisposable).IsAssignableFrom(typeof(T)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(T)))
                throw new ArgumentException("Options cannot own disposable resources.", nameof(schema));
            if (!_sections.Add(schema.Section) || Schemas.Any(item => ReferenceEquals(item.Identity, schema)))
                throw new ArgumentException("Duplicate schema in profile catalog.", nameof(schema));
            Schemas.Add(new Schema<T>(schema)); Keys.AddRange(schema.Keys);
        }
    }
}
