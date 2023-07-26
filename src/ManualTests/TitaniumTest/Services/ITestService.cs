using TitaniumTest.Models;

namespace TitaniumTest.Services;

public interface ITestService
{
    Task FooAsync(CancellationToken cancellationToken = default);
    Task Foo1Async(int a, string b, CancellationToken cancellationToken = default);
    Task<string> GetTextAsync(int value, CancellationToken cancellationToken = default);
    
    Task Foo2Async(TestPoco poco, CancellationToken cancellationToken = default);
    Task Foo3Async(TestPoco2 poco, CancellationToken cancellationToken = default);

    Task<TestPoco> Foo4Async(TestPoco2 poco, CancellationToken cancellationToken = default);

    public void VoidMethod();
    public Task MethodAsync();
}

public class ListenTestService : ITestService
{
    public Task FooAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Foo1Async(int a, string b, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"a: {a}, b: {b}");
        return Task.CompletedTask;
    }

    public Task<string> GetTextAsync(int value, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"new {value}");
    }

    public Task Foo2Async(TestPoco poco, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Foo2Async: {poco.FloatValue}");
        return Task.CompletedTask;
    }

    public Task Foo3Async(TestPoco2 poco, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Foo3Async: {poco.Text2}");
        return Task.CompletedTask;
    }

    public Task<TestPoco> Foo4Async(TestPoco2 poco, CancellationToken cancellationToken = default)
    {
        var result = poco.Poco;
        result.Text = "new text";
        return Task.FromResult(result);
    }

    public void VoidMethod()
    {
        Console.WriteLine("void method");
    }

    public Task MethodAsync()
    {
        return Task.CompletedTask;
    }
}