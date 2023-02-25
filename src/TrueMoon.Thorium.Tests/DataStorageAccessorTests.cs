using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.Tests;

public class DataStorageAccessorTests
{
    [Fact]
    public void Test1()
    {
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name},listener);
        using var accesor = new SignalStorageAccessor(name);
        
        Span<byte> data = stackalloc byte[] {0,1,2,3 };
        
        accesor.Write(data);
        
        Span<byte> dataOut = stackalloc byte[4];

        accesor.Read(dataOut);
        
        Assert.True(data[0] == dataOut[0] 
                    && data[1] == dataOut[1]
                    && data[2] == dataOut[2]
                    && data[3] == dataOut[3]
                    );
    }
    
    [Fact]
    public void Offset()
    {
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration { Name = name },listener);
        using var accesor = new SignalStorageAccessor(name);
        
        Span<byte> data = stackalloc byte[] {0,1,2,3 };
        
        accesor.Write(data, 10);
        
        Span<byte> dataOut = stackalloc byte[4];

        accesor.Read(dataOut, 10);
        
        Assert.True(data[0] == dataOut[0] 
                    && data[1] == dataOut[1]
                    && data[2] == dataOut[2]
                    && data[3] == dataOut[3]
        );
    }

}