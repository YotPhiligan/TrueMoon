namespace TrueMoon.Aether.Nom;

/// <summary>Optional authoring App composition for Aether model assets in Nom.</summary>
public static class NomContentAppConfigurationExtensions
{
    /// <summary>Registers the stateless bridge. The caller supplies an open Nom store per operation.</summary>
    /// <param name="context">Authoring composition root.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherNomContent(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<NomModelContent>()) throw new InvalidOperationException("Aether Nom content is already registered.");
            services.Singleton<NomModelContent>(_ => new());
        });
        return context;
    }
}
