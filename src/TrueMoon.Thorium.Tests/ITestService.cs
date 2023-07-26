namespace TrueMoon.Thorium.Tests;

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