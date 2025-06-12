namespace TrueMoon.Cobalt.Tests;

public class TestGenericService2
{
    public TestGenericService2(string name)
    {
        
    }
}
public class TestGenericService2<T>() : TestGenericService2("str"), ITestGenericService2<T>;