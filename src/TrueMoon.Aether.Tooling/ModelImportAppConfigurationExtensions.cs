using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Services;

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

    /// <summary>Registers typed factories for caller-owned source captures and model source watches.</summary>
    /// <param name="context">Application composition root.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Registration performs no file IO. Callers own captures and can close their watches early.
    /// App Stop closes watch admission and drains active watches.</remarks>
    public static IAppConfigurationContext UseAetherModelSourceCapture(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>() ||
                services.Exist<IFactory<ModelSourceWatch, ModelSourceWatchArguments>>())
                throw new InvalidOperationException("Model source capture is already registered.");
            services.Singleton<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>(_ =>
                new Factory<ModelSourceCapture, ModelSourceCaptureArguments>(arguments => new ModelSourceCapture(arguments)))
                .Singleton<ModelSourceWatchHost>(resolver => new(
                    resolver.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>(),
                    resolver.TryResolve<TimeProvider>() ?? TimeProvider.System))
                .Singleton<IFactory<ModelSourceWatch, ModelSourceWatchArguments>>(resolver =>
                    new Factory<ModelSourceWatch, ModelSourceWatchArguments>(resolver.Resolve<ModelSourceWatchHost>().Create))
                .Singleton<ModelSourceWatchLifetime>(resolver => new(resolver.Resolve<ModelSourceWatchHost>()))
                .Singleton<IStoppable>(resolver => resolver.Resolve<ModelSourceWatchLifetime>());
        });
        return context;
    }
}
