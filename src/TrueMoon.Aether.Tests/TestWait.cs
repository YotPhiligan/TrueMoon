namespace TrueMoon.Aether.Tests;

internal static class TestWait
{
    // Same bounded polling used by the original probe tests, independent of its executable.
    internal static async Task UntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var limit = new CancellationTokenSource(timeout);
        while (!condition()) await Task.Delay(10, limit.Token);
    }
}
