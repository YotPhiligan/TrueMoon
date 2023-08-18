namespace TrueMoon.Thorium.IO.SharedMemory;

public static class SignalsMemoryConfigurationExtensions
{
    public static SignalsMemoryConfiguration Threads(this SignalsMemoryConfiguration configuration, int value)
    {
        var threads = Math.Max(1, Math.Min(32, value));
        
        configuration.Threads = threads;
        return configuration;
    }
}