using Xunit;

namespace TrueMoon.Cobalt.Generator.Tests;

public class GeneratorTests
{
    [Fact]
    public void GeneratesCorrectly()
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
        var result = TestHelper.Verify(source);
    }
    
    [Fact]
    public void GeneratesCorrectly2()
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
        var result = TestHelper.Verify(source);
    }
    
    [Fact]
    public void GeneratesCorrectly3()
    {
        var source = @"
using System;

public interface ITestInterface
{
ITestInterface Singleton(Type s, Type t);
}

public class TestInterface : ITestInterface
{
public ITestInterface Singleton(Type s, Type t)
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

public class TestGClass
{
public TestGClass(string a)
{
}
}

public class TestGClass<T> : TestGClass
{
public TestGClass() : base(""str"") {}
}

public class Program
{
public static void Main(string[] args)
{
var test2 = new Test2();
test2.Services(c => c
    .Singleton(typeof(TestGClass<>),typeof(TestGClass<>))
);

}
}
";
        // Pass the source code to our helper and snapshot test the output
        var result = TestHelper.Verify(source);
    }
    
    [Fact]
    public void GeneratesCorrectly4()
    {
        var source = @"
using System;

public interface ITestInterface
{
ITestInterface Instance<T>(T s);
}

public class TestInterface : ITestInterface
{
public ITestInterface Instance<T>(T s)
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

public interface ITestGClass;

public class TestGClass : ITestGClass;

public class Program
{
public static void Main(string[] args)
{
var test2 = new Test2();
test2.Services(c => c
    .Instance(new TestGClass())
);

}
}
";
        // Pass the source code to our helper and snapshot test the output
        var result = TestHelper.Verify(source);
    }
}