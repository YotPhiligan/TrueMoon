namespace TrueMoon.Thorium.IO.SharedMemory;

public class SignalsMemoryConfiguration
{
    public SignalsMemoryConfiguration()
    {
        Threads = 8;
    }
    
    public int Threads { get; set; }
}