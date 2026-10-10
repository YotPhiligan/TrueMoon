using TrueMoon.Dependencies;
using TrueMoon.Exceptions;

namespace TrueMoon;

public static class AppExtensions
{
    /// <summary>Runs and owns the application through startup, shutdown and disposal.</summary>
    /// <param name="app">The application whose ownership is transferred to the runner.</param>
    /// <param name="cancellationToken">Cancels the run; cleanup uses a separate shutdown token.</param>
    /// <returns>The run operation, retaining any startup, cancellation or cleanup failure.</returns>
    public static Task RunAsync(this IApp app, CancellationToken cancellationToken = default)
        => AppRunner.RunAsync(app, cancellationToken);
}
