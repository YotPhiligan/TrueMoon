using TrueMoon;

namespace EneritBaseline;

public interface IBaselineService
{
    Task<int> PingAsync(int value, CancellationToken cancellationToken = default);
    Task<Memory<byte>> EchoAsync(Memory<byte> value, CancellationToken cancellationToken = default);
    Task<BaselineDto?> TransformAsync(BaselineDto? value, CancellationToken cancellationToken = default);
}

public sealed class BaselineDto
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public List<int>? Values { get; set; }
    public Memory<byte> Bytes { get; set; }
}

internal static class GeneratedContract
{
    internal static void Register() => UseInvocationService<IBaselineService>();
    private static void UseInvocationService<T>() { }
}

internal sealed class BaselineService : IBaselineService
{
    private long _calls;
    internal long Calls => Interlocked.Read(ref _calls);

    public Task<int> PingAsync(int value, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(value + 1);
    }

    public Task<Memory<byte>> EchoAsync(Memory<byte> value, CancellationToken cancellationToken = default)
    {
        try
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult<Memory<byte>>(value.ToArray());
        }
        finally { value.TryReturn(); }
    }

    public Task<BaselineDto?> TransformAsync(BaselineDto? value, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        if (value is null) return Task.FromResult<BaselineDto?>(null);
        try
        {
            return Task.FromResult<BaselineDto?>(new BaselineDto
            {
                Id = value.Id + 1, Text = value.Text + "!",
                Values = value.Values?.Select(item => item + 1).ToList(), Bytes = value.Bytes.ToArray()
            });
        }
        finally { value.Bytes.TryReturn(); }
    }
}
