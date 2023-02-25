using System.Diagnostics;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.Signals;

namespace TrueMoon.Thorium.Tests;

public class MessageSender2Tests
{
    [Fact]
    public void Send()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name}, listener);

        using var sender = new SignalSender(name);

        var data = new byte[] { 0, 1, 2, 3 };

        var sw = Stopwatch.StartNew();
        sender.Send(data, guid);
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        
        using var accesor = new SignalStorageAccessor(name);
        
        var signal = accesor.GetSignal(0);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal.Status);
        
        var payloadCheck = new byte[4];

        accesor.ReadSignalPayload(signal, payloadCheck);
        
        Assert.Equal(data[0], payloadCheck[0]);
        Assert.Equal(data[1], payloadCheck[1]);
        Assert.Equal(data[2], payloadCheck[2]);
        Assert.Equal(data[3], payloadCheck[3]);
    }
    
    [Fact]
    public void Send3()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name}, listener);

        using var sender = new SignalSender(name);

        var data = new byte[] { 0, 1, 2, 3 };

        var sw = Stopwatch.StartNew();
        sender.Send(data, guid);
        sender.Send(data, guid);
        sender.Send(data, guid);
        
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        
        using var accesor = new SignalStorageAccessor(name);
        
        var signal0 = accesor.GetSignal(0);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal0.Status);
        
        var signal1 = accesor.GetSignal(1);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal1.Status);
        
        var signal2 = accesor.GetSignal(2);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal2.Status);
    }
    
    [Fact]
    public async Task Send4()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name}, listener);
        
        var details = ctx.GetDescriptor();

        using var sender = new SignalSender(name);
        using var sender1 = new SignalSender(name);

        var data = new byte[] { 0, 1, 2, 3 };

        var sw = Stopwatch.StartNew();

        var tcs = new TaskCompletionSource();
        Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                sender.Send(data, guid);
            }
            tcs.SetResult();
        });
        
        var tcs1 = new TaskCompletionSource();
        Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                sender1.Send(data, guid);
            }
            tcs1.SetResult();
        });

        await Task.WhenAll(tcs.Task, tcs1.Task);
        
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        
        using var accesor = new SignalStorageAccessor(name);
        
        
        var signal0 = accesor.GetSignal(1);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal0.Status);
        
        var signal1 = accesor.GetSignal(1);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal1.Status);
    }
}