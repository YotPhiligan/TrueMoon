using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using EneritPipesProbe;
using TrueMoon;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;
using TrueMoon.Threading;
using TrueMoon.Generated.EneritPipesProbe;

if (args is ["--server", var endpoint]) return await RunServerAsync(endpoint);
if (args is not [var scenario] || !Probe.Scenarios.Contains(scenario)) return 2;
try
{
    Console.WriteLine("RESULT " + JsonSerializer.Serialize(await Probe.RunAsync(scenario)));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

static async Task<int> RunServerAsync(string endpoint)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var service = new ProbeService();
    var handler = new ProbeServiceInvocationServerHandler(service, new ProbeEvents<ProbeServiceInvocationServerHandler>());
    PipesInvocationServer<IProbeService> Create() => new(new ProbeEvents<PipesInvocationServer<IProbeService>>(), handler, endpoint);
    var server = Create();
    try
    {
        Console.WriteLine($"READY {Environment.ProcessId}");
        while (await Console.In.ReadLineAsync(deadline.Token) is { } command)
        {
            if (command == UnitCodes.Exit) return 0;
            if (command == "release") service.Release.TrySetResult();
            if (command == "rebind")
            {
                server.Dispose();
                server = Create();
                Console.WriteLine("REBOUND");
            }
        }
        return 3;
    }
    catch (OperationCanceledException) { return 4; }
    finally { server.Dispose(); }
}

namespace EneritPipesProbe
{
    internal static class Probe
    {
        internal static readonly string[] Scenarios = ["payload", "concurrent", "cancellation", "disconnect", "client-dispose", "server-rebind"];
        internal static async Task<object> RunAsync(string scenario)
        {
            var endpoint = $"enerit-probe-{Environment.ProcessId}-{Guid.NewGuid():N}";
            await using var child = new ProbeChild(endpoint);
            Check(await child.StartAsync() is true, "Child start failed.");
            var process = child.OwnedProcess!;
            using var observer = Process.GetProcessById(process.Id);
            _ = observer.SafeHandle;
            Check(await ReadLineAsync(process) == $"READY {process.Id}", "Missing child readiness.");
            var errors = process.StandardError.ReadToEndAsync();
            var events = new ProbeEvents<PipesInvocationClient<IProbeService>>();
            using var client = new PipesInvocationClient<IProbeService>(events, endpoint);
            var service = new ProbeServiceGeneratedImplementation(client);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var token = deadline.Token;
            Check(await service.PingAsync(40, token) == 41, "Initial typed invocation failed.");
            var checks = 1;
            switch (scenario)
            {
                case "payload":
                    foreach (var text in new string?[] { null, "", "Привет 🌙\0𝄞" })
                    {
                        Check(await service.EchoAsync(text, token) == text, "String round trip failed.");
                        checks++;
                    }
                    Check(await service.TransformAsync(null, token) is null, "Null DTO lost.");
                    Check(await service.IncrementAsync(null, token) is null, "Null scalar lost.");
                    Check(await service.IncrementAsync(41, token) == 42, "Nullable scalar transformation failed.");
                    Check(await service.ReverseAsync(null, token) is null, "Null list lost.");
                    Check((await service.ReverseAsync([], token))!.Count == 0, "Empty list lost.");
                    Check((await service.ReverseAsync([1, 4, 9], token))!.SequenceEqual([9, 4, 1]), "List transformation failed.");
                    Check(await service.MapAsync(null, token) is null, "Null dictionary lost.");
                    Check((await service.MapAsync([], token))!.Count == 0, "Empty dictionary lost.");
                    Check((await service.MapAsync(new() { ["α"] = 4, [""] = -2 }, token))!.OrderBy(p => p.Key)
                        .SequenceEqual(new Dictionary<string, int> { ["α"] = 5, [""] = -1 }.OrderBy(p => p.Key)), "Dictionary transformation failed.");
                    checks += 9;
                    foreach (var empty in new[] { true, false })
                    {
                        await VerifyPayloadAsync(service, empty ? new Payload() : CreatePayload(17), token);
                        checks++;
                    }
                    foreach (var length in new[] { 0, PipesProtocol.MaxPayloadLength - 4 })
                    {
                        var expected = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
                        var actual = await service.BytesAsync(expected, token);
                        try { Check(actual.Span.SequenceEqual(expected), "Bytes/frame boundary round trip failed."); }
                        finally { Check(actual.TryReturn(), "Returned bytes rental was not owned."); }
                        checks++;
                    }
                    await ExpectAsync<InvalidDataException>(() => service.BytesAsync(new byte[PipesProtocol.MaxPayloadLength - 3], token));
                    await service.AckAsync(false, token); service.AckSync(false);
                    await ExpectAsync<InvocationException>(() => service.AckAsync(true, token), "remote-probe-failure");
                    await ExpectAsync<InvocationException>(() => Task.Run(() => service.AckSync(true)), "remote-sync-failure");
                    await ExpectAsync<InvocationException>(() => client.InvokeAsync(255, null, token), "Unknown invocation method");
                    await ExpectAsync<InvocationException>(() => client.InvokeAsync(0, w => w.Write(new byte[] { 2 }), token), "Instance state");
                    await ExpectAsync<InvocationException>(() => client.InvokeAsync(0, w => w.Write(new byte[] { 0, 42 }), token), "trailing");
                    Check(await service.PingAsync(6, token) == 7, "Error poisoned the connection.");
                    checks += 9;
                    break;
                case "concurrent":
                    await Task.WhenAll(Enumerable.Range(0, 32).Select(i => VerifyPayloadAsync(service, CreatePayload(i), token)));
                    checks += 32;
                    break;
                case "cancellation":
                    using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        var pending = service.HoldAsync(cancellation.Token);
                        Check(await ReadLineAsync(process) == "WAITING", "Handler never entered.");
                        cancellation.Cancel();
                        await ExpectAsync<OperationCanceledException>(() => pending);
                        var peer = service.PingAsync(99, token);
                        await SendAsync(process, "release");
                        await events.LateResponse.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        Check(await peer == 100, "Cancelled request poisoned another pending invocation.");
                        Check(await service.PingAsync(7, token) == 8, "Late response poisoned the next invocation.");
                        Check(events.Connections == 1, "Local cancellation reconnected the channel.");
                        checks += 5;
                    }
                    break;
                default:
                    var blocked = service.HoldAsync(token);
                    Check(await ReadLineAsync(process) == "WAITING", "Handler never entered.");
                    if (scenario == "client-dispose")
                    {
                        await Task.Run(client.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
                        await ExpectAsync<ObjectDisposedException>(() => blocked);
                    }
                    else if (scenario == "disconnect")
                    {
                        Check(await child.StopAsync() is true, "Child stop failed.");
                        await ExpectAsync<IOException>(() => blocked);
                    }
                    else
                    {
                        await SendAsync(process, "rebind");
                        Check(await ReadLineAsync(process) == "REBOUND", "Server did not release/rebind its endpoint.");
                        await ExpectAsync<IOException>(() => blocked);
                        Check(await service.PingAsync(122, token) == 123, "Existing client failed to reconnect to the rebound endpoint.");
                        Check(events.Connections >= 2, "Existing client did not establish a replacement connection.");
                    }
                    checks += 2;
                    break;
            }
            await Task.Run(client.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            AssertClientClosed(client);
            await ExpectAsync<ObjectDisposedException>(() => service.PingAsync(0));
            var output = child.OwnedProcess is null ? Task.FromResult("") : process.StandardOutput.ReadToEndAsync();
            Check(await child.StopAsync() is true, "Final child stop failed.");
            observer.Refresh();
            Check(observer.HasExited && observer.ExitCode == 0, "Child exit was not observed or was unsuccessful.");
            Check(child.OwnedProcess is null, "Owned child handle leaked.");
            Check(await child.StopAsync() is true, "Repeated stop failed.");
            Check(string.IsNullOrEmpty(await errors.WaitAsync(TimeSpan.FromSeconds(5))), "Child stderr was not empty.");
            await output.WaitAsync(TimeSpan.FromSeconds(5));
            return new { Scenario = scenario, Checks = checks, ParentPid = Environment.ProcessId, ChildPid = observer.Id,
                Endpoint = endpoint, Connections = events.Connections, LateResponses = events.LateResponses,
                PendingAfterDispose = 0, ClientWorkersCompleted = true, ChildExited = true, ChildExitCode = observer.ExitCode, HandleReleased = true };
        }

        private static Payload CreatePayload(int id) => new()
        {
            Id = id, Text = $"id:{id}:Привет", Values = [1, null, -3, id], Tags = new() { ["key"] = id, ["🌙"] = -1 },
            Bytes = Enumerable.Range(0, 20000).Select(index => (byte)((index + id) % 251)).ToArray()
        };
        private static async Task VerifyPayloadAsync(IProbeService service, Payload input, CancellationToken token)
        {
            var result = await service.TransformAsync(input, token) ?? throw new InvalidOperationException("DTO response was null.");
            try
            {
                Check(result.Id == input.Id + 100 && result.Text == (input.Text is null ? null : input.Text + " 🌙"), "DTO scalar transformation failed.");
                Check(result.Values is null ? input.Values is null : input.Values is not null && result.Values.SequenceEqual(input.Values.Select(v => v + 1).Reverse()), "DTO list transformation failed.");
                Check(result.Tags is null ? input.Tags is null : input.Tags is not null && result.Tags.OrderBy(p => p.Key).SequenceEqual(input.Tags.ToDictionary(p => p.Key, p => p.Value + 1).OrderBy(p => p.Key)), "DTO dictionary transformation failed.");
                Check(result.Bytes.ToArray().SequenceEqual(input.Bytes.ToArray().Reverse()), "DTO bytes transformation failed.");
            }
            finally { Check(result.Bytes.TryReturn(), "DTO response bytes rental leaked."); }
        }
        private static async Task ExpectAsync<T>(Func<Task> action, string? message = null) where T : Exception
        {
            try { await action().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (T error)
            {
                Check(message is null || error.Message.Contains(message, StringComparison.Ordinal), $"Wrong {typeof(T).Name} message: {error.Message}");
                return;
            }
            throw new InvalidOperationException($"Expected {typeof(T).Name}.");
        }
        private static void AssertClientClosed(PipesInvocationClient<IProbeService> client)
        {
            Check(!client.IsConnected, "Disposed client remains connected.");
            var fields = client.GetType();
            var pending = fields.GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
            Check((int)pending.GetType().GetProperty("Count")!.GetValue(pending)! == 0, "Pending map leaked.");
            foreach (var name in new[] { "_listenTaskScheduler", "_taskScheduler" })
                Check(((TmTaskScheduler)fields.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Completion.IsCompletedSuccessfully, "Client worker did not complete.");
        }
        private static Task<string?> ReadLineAsync(Process process) => process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        private static async Task SendAsync(Process process, string command)
        {
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }

    internal sealed class ProbeChild(string endpoint) : ChildProcessUnitHandle(new UnitConfiguration(8)
        { RestartPolicy = UnitRestartPolicy.Never, TerminationDelay = 3000 }, new ProbeEvents<ProbeChild>())
    {
        internal Process? OwnedProcess => Process;
        protected override async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
        {
            var info = await base.GetProcessStartInfoAsync(cancellationToken);
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            return info;
        }
        protected override void ProcessLaunchArguments(Collection<string> arguments)
        {
            arguments.Add("--server"); arguments.Add(endpoint);
        }
    }

    internal sealed class ProbeEvents<T> : IEventsSource<T>
    {
        internal int Connections;
        internal int LateResponses;
        internal readonly TaskCompletionSource LateResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => typeof(T).Name;
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? func, string? category = null, string? caller = null)
        {
            if (func?.Invoke() is "Connected") Interlocked.Increment(ref Connections);
            if (category == "LateResponse") { Interlocked.Increment(ref LateResponses); LateResponse.TrySetResult(); }
        }
    }
}
