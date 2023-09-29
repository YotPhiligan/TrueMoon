using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO.Pipes;

namespace TrueMoon.Thorium.Tests.Pipes;

public class PipesTests
{
    [Fact]
    public async Task Test0()
    {
        var tcs = new TaskCompletionSource();
        
        var pipeName = "tm_test_pipes1";
        Task.Factory.StartNew(async () =>
        {
            var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            
            await stream.ConnectAsync();

            var sizeMem = MemoryPoolUtils.Create(4);
            
            await stream.ReadAsync(sizeMem);

            var len = MemoryMarshal.Read<int>(sizeMem.Span);
            
            var mem = MemoryPoolUtils.Create(len);
            
            await stream.ReadAsync(mem);

            var result = 0;

            tcs.TrySetResult();
        });
        
        
        var stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 64, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await stream.WaitForConnectionAsync();

        var mem = MemoryPoolUtils.Create(12);
        for (int i = 0; i < mem.Length; i++)
        {
            mem.Span[i] = (byte)i;
        }

        var sizeMem = MemoryPoolUtils.Create(4);
        var memLength = mem.Length;
        MemoryMarshal.Write(sizeMem.Span, ref memLength);
        await stream.WriteAsync(sizeMem);
        await stream.WriteAsync(mem);

        await tcs.Task;
    }
    
    [Fact]
    public async Task Test1()
    {
        var thoriumConfiguration = new ThoriumConfiguration();
        
        var listenService = new ListenTestService();
        var serverHandler = new TestInvocationServerHandler(listenService, new EventsSource<TestInvocationServerHandler>());
        using var server = new PipesInvocationServer<ITestService>(new EventsSource<PipesInvocationServer<ITestService>>(), serverHandler);

        using var client = new PipesInvocationClient<ITestService>(new EventsSource<PipesInvocationClient<ITestService>>());

        var service = new ClientTestService(client);

        while (!client.IsConnected)
        {
            await Task.Delay(1);
        }
        
        var testPoco2 = new TestPoco2();
        var sw = Stopwatch.StartNew();
        var result = await service.Foo4Async(testPoco2);

        sw.Stop();
        var ms = sw.ElapsedMilliseconds;
        
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task Test2()
    {
        var thoriumConfiguration = new ThoriumConfiguration();
        
        var listenService = new ListenTestService();
        var serverHandler = new TestInvocationServerHandler(listenService, new EventsSource<TestInvocationServerHandler>());
        using var server = new PipesInvocationServer<ITestService>(new EventsSource<PipesInvocationServer<ITestService>>(), serverHandler);

        using var client = new PipesInvocationClient<ITestService>(new EventsSource<PipesInvocationClient<ITestService>>());

        var service = new ClientTestService(client);

        while (!client.IsConnected)
        {
            await Task.Delay(1);
        }
        
        var tcs = new TaskCompletionSource<TestPoco>();
        var tcs2 = new TaskCompletionSource<TestPoco>();
        
        Task.Run(async () =>
        {
            var testPoco2 = new TestPoco2();
            var result = await service.Foo4Async(testPoco2);
            tcs.SetResult(result);
        });
        
        Task.Run(async () =>
        {
            var testPoco2 = new TestPoco2();
            var result = await service.Foo4Async(testPoco2);
            tcs2.SetResult(result);
        });

        var t1= tcs.Task.WaitAsync(CancellationToken.None);
        var t2= tcs2.Task.WaitAsync(CancellationToken.None);

        var results = await Task.WhenAll(t1, t2);
        
        Assert.NotNull(results[0]);
        Assert.NotNull(results[1]);
    }
}