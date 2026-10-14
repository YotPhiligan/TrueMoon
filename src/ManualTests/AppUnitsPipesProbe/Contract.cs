using System.Text.Json;
using TrueMoon;

namespace AppUnitsPipesProbe;

public interface IProbeService
{
    Task<Reply> InspectAsync(int value, string? text, CancellationToken cancellationToken = default);
    Task<int> HoldAsync(CancellationToken cancellationToken = default);
    Task AckAsync(bool fail, CancellationToken cancellationToken = default);
}
public sealed class Reply
{
    public int Value { get; set; }
    public string? Text { get; set; }
    public int Pid { get; set; }
    public int Generation { get; set; }
}
public sealed class UnitState
{
    public string Directory { get; } = Environment.GetEnvironmentVariable("TM_APP_PIPES_DIR")!;
    public string Provider { get; } = Environment.GetEnvironmentVariable("TM_APP_PIPES_PROVIDER")!;
    public string Endpoint { get; } = Environment.GetEnvironmentVariable("TM_APP_PIPES_ENDPOINT")!;
    public int Generation;
    public int Stops;
}
public sealed class ProbeService(UnitState state) : IProbeService, IStartable, IStoppable
{
    public Task<Reply> InspectAsync(int value, string? text, CancellationToken cancellationToken = default)
        => Task.FromResult(new Reply { Value = value + 100, Text = text is null ? null : "remote:" + text, Pid = Environment.ProcessId, Generation = state.Generation });
    public async Task<int> HoldAsync(CancellationToken cancellationToken = default)
    {
        Records.Write(Path.Combine(state.Directory, $"waiting-{state.Generation}.json"), new { Pid = Environment.ProcessId });
        while (!File.Exists(Path.Combine(state.Directory, $"release-{state.Generation}"))) await Task.Delay(10, cancellationToken);
        return 73;
    }
    public Task AckAsync(bool fail, CancellationToken cancellationToken = default)
    { if (fail) throw new InvalidOperationException("composition-remote-failure"); return Task.CompletedTask; }
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var counter = Path.Combine(state.Directory, "generation");
        state.Generation = File.Exists(counter) ? int.Parse(File.ReadAllText(counter)) + 1 : 1;
        File.WriteAllText(counter, state.Generation.ToString());
        Records.Write(Path.Combine(state.Directory, $"ready-{state.Generation}.json"), new Ready(Environment.ProcessId,
            int.Parse(Environment.GetEnvironmentVariable("-p")!), state.Generation, state.Provider, state.Endpoint,
            Environment.GetEnvironmentVariable("TrueMoon.Mithril.ControlPipe")!));
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        state.Stops++;
        Records.Write(Path.Combine(state.Directory, $"stopped-{state.Generation}.json"), new { Pid = Environment.ProcessId, Stops = state.Stops });
        return Task.CompletedTask;
    }
}
internal sealed record Ready(int Pid, int Parent, int Generation, string Provider, string Endpoint, string ControlEndpoint);
internal static class Records
{
    internal static void Write(string path, object value)
    { File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value)); File.Move(path + ".tmp", path, overwrite: true); }
    internal static async Task WaitAsync(string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!File.Exists(path)) await Task.Delay(10, timeout.Token);
    }
    internal static async Task<Ready> ReadyAsync(string directory, int generation)
    {
        var path = Path.Combine(directory, $"ready-{generation}.json"); await WaitAsync(path);
        return JsonSerializer.Deserialize<Ready>(await File.ReadAllTextAsync(path))!;
    }
}
