using System.Threading.Channels;
using TitaniumTest.Models;
using TrueMoon;
using TrueMoon.Thorium;

namespace TitaniumTest.Services;

public class Sender : IStartable, IStoppable
{
    private readonly ITestService _service;

    public Sender(ITestService service)
    {
        _service = service;
    }
    
    public class Test
    {
        public string Value { get; set; }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Sender started");

        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                await _service.Foo1Async(42, "abc", cancellationToken);
                await Task.Delay(1000);
                Console.WriteLine("sent");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                var testPoco = new TestPoco
                {
                    Text = "Text test1",
                    FloatValue = 0.2f,
                    IntValue = 42,
                    Memory = new byte[] {0,1,2,3}
                };
                await _service.Foo2Async(testPoco, cancellationToken);
                await Task.Delay(1000);
                Console.WriteLine("sent 2");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                var testPoco = new TestPoco2
                {
                    Poco = new TestPoco
                    {
                        Text = "Text test1",
                        FloatValue = 0.2f,
                        IntValue = 42,
                        Memory = new byte[] {0,1,2,3}
                    },
                    M = 0.43f,
                    Text2 = "teste32",
                    IntValue2 = 12
                };
                await _service.Foo3Async(testPoco, cancellationToken);
                await Task.Delay(1000);
                Console.WriteLine("sent 3");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                var testPoco = new TestPoco2
                {
                    Poco = new TestPoco
                    {
                        Text = "Text test1",
                        FloatValue = 0.2f,
                        IntValue = 42,
                        Memory = new byte[] {0,1,2,3}
                    },
                    M = 0.43f,
                    Text2 = "teste32",
                    IntValue2 = 12
                };
                var res = await _service.Foo4Async(testPoco, cancellationToken);
                Console.WriteLine($"{res?.Text}");
                await Task.Delay(1000);
                Console.WriteLine("sent 4");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                _service.VoidMethod();
                await Task.Delay(1000);
                Console.WriteLine("sent 5");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                var result = await _service.GetTextAsync(Random.Shared.Next(), cancellationToken);
                Console.WriteLine($"GetTextAsync:  {result}");
                await Task.Delay(1000);
                Console.WriteLine("sent 6");
            }
        });
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}