namespace TrueMoon.Thorium.Generator.Tests;

[UsesVerify]
public class ServicesGeneratorSnapshotTests
{
    [Fact]
    public Task GeneratesCorrectly()
    {
        var source = $@"
using System;
using TrueMoon;
using TrueMoon.Thorium;
using TrueMoon.Thorium.Tests;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class TestPoco
{{
    public int IntValue {{ get; set; }}
    public string Text {{ get; set; }}
    public float FloatValue {{ get; set; }}
    public Memory<byte> Memory {{ get; set; }}
}}

public class TestPoco2
{{
    public int IntValue2 {{ get; set; }}
    public string Text2 {{ get; set; }}
    public TestPoco Poco {{ get; set; }}
    public float M {{ get; set; }}
}}

public class TestPoco3
{{
    public int IntValue2 {{ get; set; }}
    public string Text2 {{ get; set; }}
    public TestPoco Poco {{ get; set; }}
}}

public enum TestEnum
{{
    None,
    Test1,
    Test2,
    Test3,
}}

public class TestPoco4
{{
    public List<TestPoco3> List {{ get; set; }}
    public TestPoco3[] Array {{ get; set; }}
    public TestEnum EnumValue {{ get; set; }}
    public int? NulInt {{ get; set; }}
}}

public class TestPoco5
{{
    public TestPoco5(bool boolValue)
    {{
        BoolValue = boolValue;
    }}

    public int InitValue {{ get; init; }}
    public bool BoolValue {{ get; set; }}
    public int? NulInt {{ get; set; }}
}}

public record TestRecord1(int a, bool b, TestPoco c)
{{
    public int InitValue {{ get; init; }}
    public Guid GuidValue {{ get; set; }}
    public TestEnum TestEnumValue {{ get; set; }}
}}

public record TestRecord2
{{
    public int InitValue {{ get; init; }}
    public Guid GuidValue {{ get; set; }}
    public TestEnum TestEnumValue {{ get; set; }}
}}

public interface ITestService
{{
    Task FooAsync(System.Threading.CancellationToken cancellationToken = default);
    Task Foo1Async(int a, string b, CancellationToken cancellationToken = default);
    Task<string> GetTextAsync(int value, CancellationToken cancellationToken = default);
    Task Foo2Async(TestPoco poco, CancellationToken cancellationToken = default);
    Task<TestPoco> Foo3Async(TestPoco2 poco, CancellationToken cancellationToken = default);
    Task Foo4Async(TestPoco4 poco, CancellationToken cancellationToken = default);
    void Foo5();
    Task<TestEnum?> Foo6Async(TestEnum v1);
    Task<TestPoco?> Foo7Async(TestEnum v1);
    Task<IReadOnlyList<TestPoco>> Foo8Async(TestRecord1 v1);
    Task Foo9Async(TestPoco5 v1);
    Task Foo10Async(TestRecord2 v1);
}}

public class Program
{{
public async Task Test()
{{
await App.RunAsync(context => context.UseSignalService<ITestService>()); 
}}
}}
";

        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
}