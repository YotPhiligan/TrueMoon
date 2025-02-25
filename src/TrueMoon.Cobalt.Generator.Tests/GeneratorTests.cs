namespace TrueMoon.Cobalt.Generator.Tests;

public class GeneratorTests
{
    [Fact]
    public Task GeneratesCorrectly()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using TrueMoon;
using TrueMoon.Cobalt;

public class Program
{
public static async Task Main(string[] args)
{
await App.RunAsync(t => t
    .Services(context => context
        .Singleton<object,object>()
        .Transient<object,object>()
        .Singleton<object>(f=>f.Resolve<string>())
    )
);
}
}
";
        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
    
    [Fact]
    public Task GeneratesCorrectly2()
    {
        var source = @"
using System;

public interface ITestInterface
{
ITestInterface Singleton<T,T2>() where T2 : T;
ITestInterface Transient<T,T2>() where T2 : T;
ITestInterface Singleton<T>(Func<T> a);
ITestInterface Composite<T,T1,T2>() where T : T1 where T : T2;
}

public class TestInterface : ITestInterface
{
public ITestInterface Singleton<T,T2>() where T2 : T
{
return this;
}
public ITestInterface Transient<T,T2>() where T2 : T
{
return this;
}
public ITestInterface Singleton<T>(Func<T> a)
{
return this;
}
public ITestInterface Composite<T,T1,T2>() where T : T1 where T : T2
{
return this;
}
}

public class Test2
{
public void Services(Action<ITestInterface> action)
{
}
}

public class Program
{
public static void Main(string[] args)
{
//var c = new TestInterface();
//c.Singleton<ITestInterface, TestInterface>()
//.Transient<ITestInterface, TestInterface>()
//.Singleton<object>(() => 42);

var test2 = new Test2();
test2.Services(c => c
    .Singleton<ITestInterface, TestInterface>()
    .Transient<ITestInterface, TestInterface>()
    .Singleton<object>(() => 42)
    .Composite<TestInterface,ITestInterface,object>()
);

}
}
";
        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
}