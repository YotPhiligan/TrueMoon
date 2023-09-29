// using System.Diagnostics;
// using TrueMoon.Diagnostics;
// using TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// namespace TrueMoon.Thorium.Tests.MemoryMappedFiles;
//
// public class MemoryMappedFileTests
// {
//     [Fact]
//     public async Task Test1()
//     {
//         var thoriumConfiguration = new ThoriumConfiguration();
//         using var handle = new MemoryMappedFileSignalsHandle<ITestService>(thoriumConfiguration, new EventsSource<MemoryMappedFileSignalsHandle<ITestService>>());
//         var listenService = new ListenTestService();
//         var serverHandler = new TestInvocationServerHandler(listenService, new EventsSource<TestInvocationServerHandler>());
//         using var server = new MemoryMappedFileInvocationServer<ITestService>(new EventsSource<MemoryMappedFileInvocationServer<ITestService>>(), serverHandler, thoriumConfiguration);
//
//         using var client = new MemoryMappedFileInvocationClient<ITestService>(new EventsSource<MemoryMappedFileInvocationClient<ITestService>>(), thoriumConfiguration);
//
//         var service = new ClientTestService(client);
//
//         var testPoco2 = new TestPoco2();
//         var sw = Stopwatch.StartNew();
//         var result = await service.Foo4Async(testPoco2);
//
//         sw.Stop();
//         var ms = sw.ElapsedMilliseconds;
//         
//         Assert.NotNull(result);
//     }
//     
//     [Fact]
//     public async Task Test2()
//     {
//         var thoriumConfiguration = new ThoriumConfiguration();
//         using var handle = new MemoryMappedFileSignalsHandle<ITestService>(thoriumConfiguration, new EventsSource<MemoryMappedFileSignalsHandle<ITestService>>());
//         var listenService = new ListenTestService();
//         var serverHandler = new TestInvocationServerHandler(listenService, new EventsSource<TestInvocationServerHandler>());
//         using var server = new MemoryMappedFileInvocationServer<ITestService>(new EventsSource<MemoryMappedFileInvocationServer<ITestService>>(), serverHandler, thoriumConfiguration);
//
//         using var client = new MemoryMappedFileInvocationClient<ITestService>(new EventsSource<MemoryMappedFileInvocationClient<ITestService>>(), thoriumConfiguration);
//
//         var service = new ClientTestService(client);
//
//         var tcs = new TaskCompletionSource<TestPoco>();
//         var tcs2 = new TaskCompletionSource<TestPoco>();
//         
//         Task.Run(async () =>
//         {
//             var testPoco2 = new TestPoco2();
//             var result = await service.Foo4Async(testPoco2);
//             tcs.SetResult(result);
//         });
//         
//         Task.Run(async () =>
//         {
//             var testPoco2 = new TestPoco2();
//             var result = await service.Foo4Async(testPoco2);
//             tcs2.SetResult(result);
//         });
//
//         var t1= tcs.Task.WaitAsync(CancellationToken.None);
//         var t2= tcs2.Task.WaitAsync(CancellationToken.None);
//
//         var results = await Task.WhenAll(t1, t2);
//         
//         Assert.NotNull(results[0]);
//         Assert.NotNull(results[1]);
//     }
// }