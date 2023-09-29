namespace TrueMoon.Thorium.Tests;

public class ListenTestService : ITestService
{
    public Task FooAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Foo1Async(int a, string b, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<string> GetTextAsync(int value, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task Foo2Async(TestPoco poco, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Foo3Async(TestPoco2 poco, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<TestPoco> Foo4Async(TestPoco2 poco, CancellationToken cancellationToken = default)
    {
        var testPoco = new TestPoco
        {
            Text = "42 text",// + $" {DateTime.Now.TimeOfDay}",
            FloatValue = .2f,
            IntValue = 42,
            Memory = new byte[]{0,1,2,3,}
        };
        return Task.FromResult(testPoco);
    }

    public void VoidMethod()
    {
        
    }

    public Task MethodAsync()
    {
        return Task.CompletedTask;
    }
}