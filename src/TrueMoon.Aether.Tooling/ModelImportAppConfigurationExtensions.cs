using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Tooling;

/// <summary>Opt-in model-source preparation without activation of a parser, Runtime, Nom or Editor UI.</summary>
public static class ModelImportAppConfigurationExtensions
{
    /// <summary>Registers the single-admission model import/publication host and caller-owned typed factories.</summary>
    /// <param name="context">Application composition root.</param><returns>The same context.</returns>
    /// <remarks>Opt-in and IO-free registration. Stop reaps active workers and drains publication IO; idle products and snapshots belong to callers.</remarks>
    public static IAppConfigurationContext UseAetherModelImport(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<ModelImportHost>() || services.Exist<IFactory<ModelImportOperation, ModelImportArguments>>() ||
                services.Exist<IFactory<ModelImportLedgerSession,ModelImportLedgerArguments>>())
                throw new InvalidOperationException("Model import is already registered.");
            services.Singleton<ModelImportHost>(_ => new())
                .Singleton<IFactory<ModelImportOperation, ModelImportArguments>>(resolver =>
                {
                    var host = resolver.Resolve<ModelImportHost>();
                    return new Factory<ModelImportOperation, ModelImportArguments>(arguments => new ModelImportOperation(host, arguments));
                })
                .Singleton<ModelImportLifetime>(resolver => new(resolver.Resolve<ModelImportHost>()))
                .Singleton<IStoppable>(resolver => resolver.Resolve<ModelImportLifetime>());
            if (!services.Exist<IModelImportLedgerStore>()) services.Singleton<IModelImportLedgerStore>(_ => new FileModelImportLedgerStore());
            services.Singleton<IFactory<ModelImportLedgerSession,ModelImportLedgerArguments>>(resolver =>
            {
                var host=resolver.Resolve<ModelImportHost>(); var store=resolver.Resolve<IModelImportLedgerStore>();
                return new Factory<ModelImportLedgerSession,ModelImportLedgerArguments>(arguments=>new ModelImportLedgerSession(host,store,arguments));
            });
        });
        return context;
    }

    /// <summary>Registers a typed factory for caller-owned, single-use source capture operations.</summary>
    /// <param name="context">Application composition root.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Registration performs no file IO. Dispose each factory product before App shutdown.
    /// Limits and source identities are explicit per-operation arguments; the factory owns no products.</remarks>
    public static IAppConfigurationContext UseAetherModelSourceCapture(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>())
                throw new InvalidOperationException("Model source capture is already registered.");
            services.Singleton<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>(_ =>
                new Factory<ModelSourceCapture, ModelSourceCaptureArguments>(arguments => new ModelSourceCapture(arguments)));
        });
        return context;
    }
}
