using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Nom;

namespace TrueMoon.Aether.Nom;

/// <summary>Optional authoring App composition for Aether model assets in Nom.</summary>
public static class NomContentAppConfigurationExtensions
{
    /// <summary>Registers the stateless bridge and a model publisher factory that borrows a caller-owned Nom store.</summary>
    /// <param name="context">Authoring composition root.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherNomContent(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<NomModelContent>()) throw new InvalidOperationException("Aether Nom content is already registered.");
            services.Singleton<NomModelContent>(_ => new());
            if (!services.Exist<IFactory<IModelImportContentPublisher, IProjectAssetStore>>())
                services.Singleton<IFactory<IModelImportContentPublisher, IProjectAssetStore>>(resolver =>
                {
                    var bridge = resolver.Resolve<NomModelContent>();
                    return new Factory<IModelImportContentPublisher, IProjectAssetStore>(store =>
                        new NomModelImportContentPublisher(bridge, store ?? throw new ArgumentNullException(nameof(store))));
                });
        });
        return context;
    }
}
