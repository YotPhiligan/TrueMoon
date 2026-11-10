namespace TrueMoon.Configuration;

/// <summary>Connects opt-in configuration to App composition.</summary>
public static class ConfigurationAppExtensions
{
    /// <summary>Selects one profile without loading sources or activating services.</summary>
    /// <param name="context">App setup context with stage support.</param><param name="profile">Profile to load after setup.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguration(this IAppConfigurationContext context, IConfigurationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(profile);
        if (context is not IAppConfigurationStage stage) throw new NotSupportedException("This context has no configuration stage.");
        stage.UseConfiguration(profile);
        return context;
    }

    /// <summary>Registers a module only after all schemas have valid options.</summary>
    /// <param name="context">App setup context with stage support.</param><param name="schema">Registered schema instance.</param>
    /// <param name="register">Composition callback receiving a copy.</param><param name="configure">Pure options override.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext ConfigureModule<T>(this IAppConfigurationContext context, IConfigurationSchema<T> schema,
        Action<IAppConfigurationContext, T> register, Action<T>? configure = null) where T : class
        => ConfigurationModuleBinding.Configure(context, schema, register, configure);
}
