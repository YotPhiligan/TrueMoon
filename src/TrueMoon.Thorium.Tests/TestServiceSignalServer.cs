using System.Buffers;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public class TestSignalServerHandler : ISignalServerHandler<ITestService>
{
    private readonly ITestService _service;
    private readonly IEventsSource<TestSignalServerHandler> _eventsSource;

    public TestSignalServerHandler(ITestService service,
        IEventsSource<TestSignalServerHandler> eventsSource)
    {
        _service = service;
        _eventsSource = eventsSource;
    }
    
    public async Task<bool> HandleAsync(byte method, IMemoryReadHandle readHandle, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default)
    {
        switch (method)
        {
            case 3:
            {
                try
                {
                    var offset = 0;
                    TestPoco parameter = default;
                    parameter = parameter.Deserialize(readHandle.GetData()[offset..], ref offset);
                    await _service.Foo2Async(parameter, cancellationToken);
                }
                catch (Exception e)
                {
                    _eventsSource.Exception(e);
                    return false;
                }
            }
                break;
            case 5:
            {
                TestPoco result = default;
                try
                {
                    var offset = 0;
                    TestPoco2 parameter = default;
                    parameter = parameter.Deserialize(readHandle.GetData()[offset..], ref offset);
                    result = await _service.Foo4Async(parameter, cancellationToken);
                }
                catch (Exception e)
                {
                    _eventsSource.Exception(e);
                    return false;
                }
                result.Serialize(bufferWriter);
            }
                break;
        }

        return true;
    }
}