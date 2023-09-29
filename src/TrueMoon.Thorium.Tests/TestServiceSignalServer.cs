using System.Buffers;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public class TestInvocationServerHandler : IInvocationServerHandler<ITestService>
{
    private readonly ITestService _service;
    private readonly IEventsSource<TestInvocationServerHandler> _eventsSource;

    public TestInvocationServerHandler(ITestService service,
        IEventsSource<TestInvocationServerHandler> eventsSource)
    {
        _service = service;
        _eventsSource = eventsSource;
    }
    
    public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readHandle, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default)
    {
        switch (method)
        {
            case 3:
            {
                try
                {
                    var offset = 0;
                    TestPoco parameter = default;
                    parameter = parameter.Deserialize(readHandle.Span[offset..], ref offset);
                    await _service.Foo2Async(parameter, cancellationToken);
                }
                catch (Exception e)
                {
                    _eventsSource.Exception(e);
                    return (false, e);
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
                    parameter = parameter.Deserialize(readHandle.Span[offset..], ref offset);
                    result = await _service.Foo4Async(parameter, cancellationToken);
                }
                catch (Exception e)
                {
                    _eventsSource.Exception(e);
                    return (false, e);
                }
                result.Serialize(bufferWriter);
            }
                break;
        }

        return (true, null);
    }
}