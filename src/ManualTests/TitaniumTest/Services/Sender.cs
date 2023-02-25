using System.Threading.Channels;
using TitaniumTest.Models;
using TrueMoon;
using TrueMoon.Thorium;

namespace TitaniumTest.Services;

public class Sender : IStartable, IStoppable
{
    private readonly ISignalInvoker _signalInvoker;

    public Sender(ISignalInvoker signalInvoker)
    {
        _signalInvoker = signalInvoker;
    }
    
    public class Test
    {
        public string Value { get; set; }
    }
    
    private Channel<Test> channel = Channel.CreateUnbounded<Test>();
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Sender started");

        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (await channel.Reader.WaitToReadAsync(cancellationToken))
                {
                    var value = await channel.Reader.ReadAsync(cancellationToken);
                
                    Console.WriteLine(value.Value);
                }
            }
        });

        Task.Factory.StartNew(async () =>
        {
            try
            {
                var t = 0;
                for (int i = 0; i < 100; i++)
                {
                    var nowTimeOfDay = DateTime.Now.TimeOfDay;
                    var data = $"request1: {nowTimeOfDay} - {Guid.NewGuid()}";
                    
                    // var nowTimeOfDay = new TimeSpan(500);
                    // var data = $"t_1";
                
                    var response = await _signalInvoker.RequestAsync<TestMessage,TestMessageResponse>(new TestMessage{Data = data, Time = nowTimeOfDay}, cancellationToken);
                
                    //Console.WriteLine($"response1: {response?.Data}");
                    if (!string.IsNullOrWhiteSpace(response?.Data))
                    {
                        t++;
                    }
                    await channel.Writer.WriteAsync(new Test{Value = $"response1: {response?.Data}"}, cancellationToken);
                
                    await Task.Delay(0);
                }
            
                await channel.Writer.WriteAsync(new Test{Value = $"response1 count: {t}"}, cancellationToken);
                Console.WriteLine("1 done");
            }
            catch (Exception e)
            {
                Console.WriteLine($"1:  {e}");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            try
            {
                var t = 0;
                for (int i = 0; i < 100; i++)
                {
                    var nowTimeOfDay = DateTime.Now.TimeOfDay;
                    var data = $"request2: {nowTimeOfDay} - {Guid.NewGuid()}";
                
                    // var nowTimeOfDay = new TimeSpan(500);
                    // var data = $"t_2";
                    
                    var response = await _signalInvoker.RequestAsync<TestMessage2,TestMessageResponse2>(new TestMessage2{Data = data, Time = nowTimeOfDay}, cancellationToken);
                
                    //Console.WriteLine($"response2: {response?.Data}");
                    if (!string.IsNullOrWhiteSpace(response?.Data))
                    {
                        t++;
                    }
                    await channel.Writer.WriteAsync(new Test{Value = $"response2: {response?.Data}"}, cancellationToken);
                
                    await Task.Delay(0);
                }
                await channel.Writer.WriteAsync(new Test{Value = $"response2 count: {t}"}, cancellationToken);
                Console.WriteLine("2 done");
            }
            catch (Exception e)
            {
                Console.WriteLine($"2:  {e}");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            try
            {
                var t = 0;
                for (int i = 0; i < 50; i++)
                {
                    var bytes = new byte[102 * 1024];
                    bytes[0] = 255;
                    bytes[1] = 254;
                    bytes[2] = 253;
                    bytes[3] = 252;
                    var response = await _signalInvoker.RequestAsync<TestMessageLarge1,TestMessageLargeResponse1>(new TestMessageLarge1
                    {
                        Index = i,
                        Data = bytes
                    }, cancellationToken);
                    
                    if (response?.Data != null && response?.Data.Last() == 255)
                    {
                        t++;
                    }
                    await channel.Writer.WriteAsync(new Test{Value = $"response3: {response?.Index}"}, cancellationToken);
                
                    await Task.Delay(500);
                }
                await channel.Writer.WriteAsync(new Test{Value = $"response3 count: {t}"}, cancellationToken);
                Console.WriteLine("3 done");
            }
            catch (Exception e)
            {
                Console.WriteLine($"2:  {e}");
            }
        });
        
        Task.Factory.StartNew(async () =>
        {
            try
            {
                var t = 0;
                for (int i = 0; i < 50; i++)
                {
                    var response = await _signalInvoker.RequestAsync<TestMessage3,TestMessageLargeResponse3>(new TestMessage3
                    {
                        Index = i,
                    }, cancellationToken);
                    
                    if (response?.Data != null && response?.Data[0] == 255)
                    {
                        t++;
                    }
                    await channel.Writer.WriteAsync(new Test{Value = $"small request -> large response: {response?.Index}"}, cancellationToken);
                
                    await Task.Delay(500);
                }
                await channel.Writer.WriteAsync(new Test{Value = $"small request -> large response count: {t}"}, cancellationToken);
                Console.WriteLine("small request -> large response -- done");
            }
            catch (Exception e)
            {
                Console.WriteLine($"2:  {e}");
            }
        });
        
        // Task.Factory.StartNew(() =>
        // {
        //     for (int i = 0; i < 100; i++)
        //     {
        //         var nowTimeOfDay = DateTime.Now.TimeOfDay;
        //         var data = $"{nowTimeOfDay} - {Guid.NewGuid()}";
        //         _signalInvoker.Send(new Test1Message{Data = data, Time = nowTimeOfDay});
        //         Console.WriteLine($"msg sent: {data}");
        //     }
        // });        
        
        // Task.Factory.StartNew(() =>
        // {
        //     for (int i = 0; i < 100; i++)
        //     {
        //         _signalInvoker.Send(new SignalTest());
        //         //Console.WriteLine("signal sent");
        //         channel.Writer.TryWrite(new Test{Value = "signal sent"});
        //     }
        // });
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}