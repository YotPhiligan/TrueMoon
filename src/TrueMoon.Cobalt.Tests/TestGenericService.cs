namespace TrueMoon.Cobalt.Tests;

public class TestGenericService<T>(IService1 service1, SubService2 subService2) : TestGClassBase("str"), ITestGenericService<T>;