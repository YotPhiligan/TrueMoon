namespace TrueMoon.Alloy;

/// <summary>Runs owner-thread cleanup completely while preserving the operation error ahead of cleanup errors.</summary>
public static class UiCleanup
{
    /// <summary>Attempts every cleanup action, then rethrows the original error or aggregates all failures.</summary>
    /// <param name="failure">Original operation error, or null on a successful path.</param>
    /// <param name="cleanup">Ordered owner-thread cleanup actions.</param>
    public static void Complete(Exception? failure, params Action[] cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        if (cleanup.Any(action => action == null)) throw new ArgumentException("Cleanup actions must not be null.", nameof(cleanup));
        var errors = new List<Exception>();
        if (failure != null) errors.Add(failure);
        foreach (var action in cleanup) try { action(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("UI operation/cleanup failed.", errors);
    }
}
