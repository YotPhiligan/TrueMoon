namespace TrueMoon.Cobalt.Tests;

public class CobaltGeneratorSnapshotTests
{
    [Fact]
    public Task GeneratesCorrectly()
    {
        var source = $@"
using System;
using TrueMoon;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public interface IMainService : IDisposable;
public class MainService(IService1 service1, IService2 service2) : IMainService;
public interface IService1;
public class Service1(SubService1 subService1) : IService1;
public interface IService2;
public class Service2 : IService2;

public class SubService1;

public class Program
{{
public async Task Test()
{{
await App.RunAsync(context => context
        .AddDependencies(ctx => ctx
            .AddSingleton<IMainService, MainService>()
            .Add<IService1, Service1>()
            .AddTransient<IService2, Service2>()
        )
    ); 
}}
}}
";

        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
}