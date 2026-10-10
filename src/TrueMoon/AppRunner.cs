using System.Runtime.ExceptionServices;
using TrueMoon.Dependencies;
using TrueMoon.Exceptions;

namespace TrueMoon;

internal static class AppRunner
{
    internal static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    public static async Task RunAsync(IApp app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var errors = new List<Exception>();
        IAppLifetimeHandler? lifetime = null;
        var startAttempted = false;
        try
        {
            lifetime = app.Services.Resolve<IAppLifetimeHandler>()
                ?? throw new AppCreationException($"{nameof(IAppLifetimeHandler)} is missing");
            cancellationToken.ThrowIfCancellationRequested();
            startAttempted = true;
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            await lifetime.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            errors.Add(error);
        }

        using var shutdown = new CancellationTokenSource(ShutdownTimeout);
        if (lifetime != null) TryCleanup(lifetime.Stopping, errors);
        if (startAttempted)
        {
            try { await app.StopAsync(shutdown.Token).ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
        }
        if (lifetime != null) TryCleanup(lifetime.Stopped, errors);
        try { await app.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        ThrowErrors(errors);
    }

    internal static void TryCleanup(Action action, List<Exception> errors)
    {
        try { action(); }
        catch (Exception error) { errors.Add(error); }
    }

    internal static void ThrowErrors(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException("Application operation and/or cleanup failed.", errors);
    }
}
