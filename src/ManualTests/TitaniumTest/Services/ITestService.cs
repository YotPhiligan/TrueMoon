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
    Task<bool?> Foo5Async(TestPoco3 poco, CancellationToken cancellationToken = default);

    Task<bool?> Foo6Async(TestRecordPoco1 poco, CancellationToken cancellationToken = default);
    Task<TestEnum1?> Foo7Async(TestEnum1 v1, TestEnum2? NulV2, TestRecordPoco1 poco, TestPoco? nulTestPoco, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> Foo8Async(TestPoco4 testPoco, CancellationToken cancellationToken = default);
    Task<(bool, int?, TestPoco)> Foo9Async((int v1, bool? v2, TestPoco v3, TestPoco2? v4) inputValue, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestPoco>> Foo10Async(IReadOnlyDictionary<string,TestPoco2> dictionary, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TestPoco>, long)> Foo11Async(int a, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TestStruct1> structs, bool)> Foo12Async(TestStruct1 struct1, CancellationToken cancellationToken = default);
    
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

    public async Task<bool?> Foo5Async(TestPoco3 poco, CancellationToken cancellationToken = default)
    {
        return poco.NulBoolValue;
    }    
    
    public async Task<bool?> Foo6Async(TestRecordPoco1 poco, CancellationToken cancellationToken = default)
    {
        return poco.BoolValue;
    }

    public async Task<TestEnum1?> Foo7Async(TestEnum1 v1, TestEnum2? NulV2, TestRecordPoco1 poco, TestPoco? nulTestPoco, CancellationToken cancellationToken = default)
    {
        return v1;
    }

    public async Task<IReadOnlyDictionary<string, string>> Foo8Async(TestPoco4 testPoco, CancellationToken cancellationToken = default)
    {
        return new Dictionary<string, string>();
    }

    public async Task<(bool, int?, TestPoco)> Foo9Async((int v1, bool? v2, TestPoco v3, TestPoco2? v4) inputValue,
        CancellationToken cancellationToken = default)
    {
        return default;
    }

    public async Task<IReadOnlyList<TestPoco>> Foo10Async(IReadOnlyDictionary<string, TestPoco2> dictionary, CancellationToken cancellationToken = default)
    {
        return default;
    }

    public async Task<(IReadOnlyList<TestPoco>, long)> Foo11Async(int a, CancellationToken cancellationToken = default)
    {
        return default;
    }

    public async Task<(IReadOnlyList<TestStruct1> structs, bool)> Foo12Async(TestStruct1 struct1, CancellationToken cancellationToken = default)
    {
        return default;
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