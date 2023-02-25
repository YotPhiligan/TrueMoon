namespace TrueMoon.Thorium;

public class ThoriumConfiguration
{
    public string? Name { get; set; }
    public byte SmallSignals { get; set; } = 32;
    public int SmallSignalSize { get; set; } = 100 * 1024;

    public byte LargeSignals { get; set; } = 6;
    public int LargeSignalSize { get; set; } = 3 * 1024 * 1024;
    
    public int ReadThreads { get; set; } = 4;
    public int WriteThreads { get; set; } = 4;
}