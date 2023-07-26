using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public class ClientTestService : ITestService
{
    private readonly ISignalClient<ITestService> _signalClient;

    public ClientTestService(ISignalClient<ITestService> signalClient)
    {
        _signalClient = signalClient;
    }
    
    public Task FooAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task Foo1Async(int a, string b, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetTextAsync(int value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task Foo2Async(TestPoco poco, CancellationToken cancellationToken = default)
    {
        return _signalClient.InvokeAsync(3, 
            writer =>
            {
                poco.Serialize(writer);
            }, cancellationToken);
    }

    public Task Foo3Async(TestPoco2 poco, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    
    public Task<TestPoco> Foo4Async(TestPoco2 poco, CancellationToken cancellationToken = default)
    {
        return _signalClient.InvokeAsync(5, 
            writer =>
            {
                poco.Serialize(writer);
            }, 
            handle =>
        {
            var offset = 0;
            TestPoco? result = default;
            return result.Deserialize(handle.GetData(), ref offset);
        }, cancellationToken);
    }

    public void VoidMethod()
    {
        throw new NotImplementedException();
    }

    public Task MethodAsync()
    {
        throw new NotImplementedException();
    }
}