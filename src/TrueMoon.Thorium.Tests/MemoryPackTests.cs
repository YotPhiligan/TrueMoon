using System.Buffers;
using System.Diagnostics;
using MemoryPack;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.Tests.Utils;

namespace TrueMoon.Thorium.Tests;

public class MemoryPackTests
{
    [Fact]
    public void Test1()
    {
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name},listener);
        using var accesor = new SignalStorageAccessor(name);

        var obj = new TestPoco
        {
            Number = 42,
            ByteValue = 254,
            TestString = "Test string",
            Data = new byte[]{0,1,2,3}
        };
        
        //var writer = new ArrayBufferWriter<byte>();
        //MemoryPackSerializer.Serialize(writer, obj);
        var data = MemoryPackSerializer.Serialize(obj);

        //var data = writer.GetSpan();
        accesor.Write(data);


        var array = ArrayPool<byte>.Shared.Rent(1 * 1024 * 1024);

        var size = accesor.Read(array, 0, data.Length);

        TestPoco? valueOut = null;

        var t = MemoryPackSerializer.Deserialize(array.AsSpan(0, size), ref valueOut);
        
        Assert.NotNull(valueOut);
        
        Assert.Equal(obj.ByteValue, valueOut.ByteValue);
        Assert.Equal(obj.TestString, valueOut.TestString);
        Assert.Equal(obj.Number, valueOut.Number);

        Assert.True(obj.Data[0] == valueOut.Data[0] 
                    && obj.Data[1] == valueOut.Data[1]
                    && obj.Data[2] == valueOut.Data[2]
                    && obj.Data[3] == valueOut.Data[3]
        );
    }
    
    [Fact]
    public void Test2()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name}, listener);
        using var sender = new SignalSender(name);
        using var receiver = new SignalListener(name, listener, new Guid[]{guid});
        
        TestPoco? valueOut = null;
        
        receiver.OnSignal((signal, handle, arg3, arg4) =>
        {
            var sw1 = Stopwatch.StartNew();
            var t = MemoryPackSerializer.Deserialize(handle.GetData(), ref valueOut);
            sw1.Stop();
            var ms = sw1.ElapsedMilliseconds;
        });
        receiver.Listen();

        var obj = new TestPoco
        {
            Number = 42,
            ByteValue = 254,
            TestString = "Test string",
            Data = new byte[]{0,1,2,3}
        };
        
        //var writer = new ArrayBufferWriter<byte>(33);
        using var writer = new ArrayPoolBufferWriter<byte>();
        MemoryPackSerializer.Serialize(writer, obj);
        //var data = MemoryPackSerializer.Serialize(obj);

        sender.Invoke(writer.WrittenMemory, guid);
        
        
        Assert.NotNull(valueOut);
        
        Assert.Equal(obj.ByteValue, valueOut.ByteValue);
        Assert.Equal(obj.TestString, valueOut.TestString);
        Assert.Equal(obj.Number, valueOut.Number);

        Assert.True(obj.Data[0] == valueOut.Data[0] 
                    && obj.Data[1] == valueOut.Data[1]
                    && obj.Data[2] == valueOut.Data[2]
                    && obj.Data[3] == valueOut.Data[3]
        );
    }
    
    [Fact]
    public void Test3()
    {
        var guid = new Guid("9C424CC3-A658-4F1F-8F2E-FD546EAB0343");
        
        var name = $"tm_test{Guid.NewGuid()}";
        var listener = new EventsSource<StorageHandle>();
        using var ctx = new StorageHandle(new ThoriumConfiguration{Name = name},listener);
        using var sender = new SignalSender(name);
        using var receiver = new SignalListener(name, listener, new []{guid});
        
        TestPoco? valueOut = null;
        
        Stopwatch sw = default;
        
        receiver.OnSignal((signal, handle, writeHandle, arg4) =>
        {
            var els = sw.ElapsedMilliseconds;
            var t = MemoryPackSerializer.Deserialize(handle.GetData(), ref valueOut);

            var response1 = new TestResponsePoco
            {
                Result = true,
                ResultString = $"response for: {valueOut?.TestString}"
            };
            
            var bufferWriter = new ArrayBufferWriter<byte>(64);
            MemoryPackSerializer.Serialize(bufferWriter, response1);

            writeHandle.Write(bufferWriter.WrittenMemory);
        });
        receiver.Listen();

        var obj = new TestPoco
        {
            Number = 42,
            ByteValue = 254,
            TestString = "Test string",
            Data = new byte[]{0,1,2,3}
        };
        
        var writer = new ArrayBufferWriter<byte>(33);
        MemoryPackSerializer.Serialize(writer, obj);
        //var data = MemoryPackSerializer.Serialize(obj);

        TestResponsePoco? response = default;

        sw = Stopwatch.StartNew();
        sender.Request(writer.WrittenMemory, guid, responsePayloadReadFunc: (signal, handle) =>
        {
            var t = MemoryPackSerializer.Deserialize(handle.GetData(), ref response);
        });
        
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        
        Assert.NotNull(response);
        Assert.True(response.Result);
        Assert.NotNull(response.ResultString);
    }

}