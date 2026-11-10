namespace TrueMoon.Configuration;

/// <summary>Optional App composition capability for deferred options validation.</summary>
public interface IAppConfigurationStage
{
    /// <summary>Selects one opt-in profile for this App build.</summary>
    /// <param name="profile">Profile with an IConfigurationProfileSchemas catalog.</param>
    void UseConfiguration(IConfigurationProfile profile);

    /// <summary>Defers binding, validation and module registration until all setup callbacks complete.</summary>
    /// <param name="schema">The exact registered schema instance.</param>
    /// <param name="register">Composition callback receiving an independent validated options copy.</param>
    /// <param name="configure">Optional pure options override before final validation.</param>
    void ConfigureModule<T>(IConfigurationSchema<T> schema, Action<IAppConfigurationContext, T> register,
        Action<T>? configure = null) where T : class;
}
