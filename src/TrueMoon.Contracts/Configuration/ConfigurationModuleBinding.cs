namespace TrueMoon.Configuration;

/// <summary>Provider-neutral optional App-stage binding for modules that depend only on common contracts.</summary>
public static class ConfigurationModuleBinding
{
    /// <summary>Declares data binding and deferred registration without choosing a bootstrap or DI implementation.</summary>
    /// <param name="context">App context supporting the optional stage.</param><param name="schema">Exact registered schema.</param>
    /// <param name="register">Composition callback.</param><param name="configure">Pure explicit overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext Configure<T>(IAppConfigurationContext context, IConfigurationSchema<T> schema,
        Action<IAppConfigurationContext, T> register, Action<T>? configure = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(register);
        if (context is not IAppConfigurationStage stage) throw new NotSupportedException("This context has no configuration stage.");
        stage.ConfigureModule(schema, register, configure); return context;
    }
}
