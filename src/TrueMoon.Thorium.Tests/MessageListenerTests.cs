using System.Diagnostics;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.Signals;

namespace TrueMoon.Thorium.Tests;

public class MessageListenerTests
{
    [Fact]
    public void SendAsync()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name}, listener);

        using var sender = new SignalSender(name);

        var data = new byte[] { 0, 1, 2, 3 };

        sender.Send(data, guid);

        using var accessor = new SignalStorageAccessor(name);
        
        var signal = accessor.GetSignal(0);
        
        Assert.Equal(SignalStatus.ReadyToProcess, signal.Status);
        
        using var receiver = new SignalListener(name, listener, new Guid[]{guid});
        receiver.Listen();
        
        Thread.Sleep(100);
        
        signal = accessor.GetSignal(0);
        
        Assert.Equal(SignalStatus.None, signal.Status);
    }
    
    [Fact]
    public void Sender_Invoke()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name},listener);
        
        using var sender = new SignalSender(name);
        using var receiver = new SignalListener(name, listener,new Guid[]{guid});
        receiver.OnSignal((signal, handle, arg3, arg4) => Thread.Sleep(1000));
        receiver.Listen();

        var data = new byte[] { 0, 1, 2, 3 };

        var sw = Stopwatch.StartNew();
        sender.Invoke(data,guid);

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds > 999);
    }
    
    [Fact]
    public void Sender_Invoke2()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");

        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name}, listener);

        var signaled = false;
        
        using var sender = new SignalSender(name);
        using var receiver = new SignalListener(name, listener,new Guid[]{guid});
        receiver.OnSignal((signal, handle, arg3, arg4) => signaled = true);
        receiver.Listen();

        var data = new byte[] { 0, 1, 2, 3 };

        var sw = Stopwatch.StartNew();
        sender.Invoke(data,guid);

        sw.Stop();

        Assert.True(signaled);
    }
    
    [Fact]
    public void Send1()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration { Name = name }, listener);

        using var sender = new SignalSender(name);
        using var receiver = new SignalListener(name, listener,new Guid[]{guid});
        receiver.Listen();

        var data = new byte[] { 0, 1, 2, 3 };

        sender.Send(data,guid);

        //using var accessor = new MessageStorageAccessor(name);

        
    }
}