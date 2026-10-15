using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using EneritBaseline;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.EneritBaseline;
using TrueMoon.Threading;

if (args is ["--list"])
{
    Console.WriteLine(JsonSerializer.Serialize(Case.All));
    return 0;
}
if (args is ["--server", var endpoint]) return await RunServerAsync(endpoint);
if (args.Length is not (4 or 5) || args[0] != "--client" || (args.Length == 5 && args[4] != "--verify")) return 2;
try
{
    await RunClientAsync(Case.Create(args[1], int.Parse(args[2], CultureInfo.InvariantCulture)), args[3], args.Length == 5);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

static async Task<int> RunServerAsync(string endpoint)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
    var implementation = new BaselineService();
    var events = new ProbeEvents<PipesInvocationServer<IBaselineService>>();
    var handler = new BaselineServiceInvocationServerHandler(implementation, new ProbeEvents<BaselineServiceInvocationServerHandler>());
    using var server = new PipesInvocationServer<IBaselineService>(events, handler, endpoint);
    Console.WriteLine("READY " + Environment.ProcessId);
    Snapshot? before = null;
    while (await Console.In.ReadLineAsync(deadline.Token) is { } command)
    {
        if (command == "begin")
        {
            if (before is not null) throw new InvalidOperationException("Nested measurement.");
            before = Snapshot.Capture(implementation.Calls, events.Errors);
            Console.WriteLine("BEGUN");
        }
        else if (command == "end")
        {
            var delta = Snapshot.Capture(implementation.Calls, events.Errors).Subtract(before ?? throw new InvalidOperationException("Measurement not started."));
            before = null;
            Console.WriteLine(JsonSerializer.Serialize(delta));
        }
        else if (command == "stop")
        {
            // Client EOF during normal teardown may produce a transport diagnostic.
            Workload.Check(before is null, "Server measurement not ended.");
            return 0;
        }
        else throw new InvalidOperationException("Unknown control command.");
    }
    return 1;
}

static async Task RunClientAsync(Case descriptor, string reportPath, bool verify)
{
    if (File.Exists(reportPath)) throw new IOException("Report already exists.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
    var pipe = "enerit-baseline-" + Guid.NewGuid().ToString("N");
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("--server"); start.ArgumentList.Add(pipe);
    using var child = new Process { StartInfo = start };
    var started = false;
    try
    {
        started = child.Start();
        var errors = child.StandardError.ReadToEndAsync();
        Workload.Check(await ReadLineAsync(child) == "READY " + child.Id, "Wrong server readiness/PID.");
        var clientEvents = new ProbeEvents<PipesInvocationClient<IBaselineService>>();
        using var client = new PipesInvocationClient<IBaselineService>(clientEvents, pipe);
        IBaselineService service = new BaselineServiceGeneratedImplementation(client);
        var workload = new Workload(descriptor, service);
        var warmupStart = Stopwatch.GetTimestamp();
        var warmupOperations = 0;
        do
        {
            await workload.ExecuteAsync(verify ? 32 : 256, null, deadline.Token);
            warmupOperations += verify ? 32 : 256;
        } while (!verify && Stopwatch.GetElapsedTime(warmupStart).TotalSeconds < 1.5);
        var warmupSeconds = Stopwatch.GetElapsedTime(warmupStart).TotalSeconds;
        var pilotStart = Stopwatch.GetTimestamp();
        await workload.ExecuteAsync(verify ? 32 : 512, null, deadline.Token);
        var pilotSeconds = Stopwatch.GetElapsedTime(pilotStart).TotalSeconds;
        var operations = verify ? 64 : Math.Clamp((int)Math.Ceiling(512 / pilotSeconds), 512, 131072);
        operations = ((operations + descriptor.Concurrency - 1) / descriptor.Concurrency) * descriptor.Concurrency;
        var samples = new List<Sample>();
        for (var iteration = 0; iteration < (verify ? 1 : 3); iteration++)
        {
            var durations = new long[operations]; // Outside the allocation window.
            await SendAsync(child, "begin");
            Workload.Check(await ReadLineAsync(child) == "BEGUN", "Server did not begin measurement.");
            var before = Snapshot.Capture();
            var begin = Stopwatch.GetTimestamp();
            await workload.ExecuteAsync(operations, durations, deadline.Token);
            var seconds = Stopwatch.GetElapsedTime(begin).TotalSeconds;
            var delta = Snapshot.Capture().Subtract(before);
            await SendAsync(child, "end");
            var server = JsonSerializer.Deserialize<Snapshot>(await ReadLineAsync(child))!;
            Workload.Check(server.Calls == operations && server.Errors == 0 && delta.AllocatedBytes > 0 && server.AllocatedBytes > 0,
                "Server call count/allocation counters invalid.");
            Workload.Check(durations.All(tick => tick > 0), "Latency sample missing.");
            if (!verify) Workload.Check(seconds >= 0.25, "Measurement shorter than 250ms; recalibrate workload.");
            var sorted = (long[])durations.Clone(); Array.Sort(sorted);
            samples.Add(new Sample(iteration, operations, seconds, operations / seconds,
                Statistics.Percentile(sorted, .5), Statistics.Percentile(sorted, .95), Statistics.Percentile(sorted, .99),
                delta, server, durations));
        }
        Workload.Check(clientEvents.Connections == 1 && clientEvents.Errors == 0, "Unexpected reconnect/error.");
        client.Dispose();
        Workload.Check(!client.IsConnected, "Client still connected after Dispose.");
        var pending = (System.Collections.IDictionary)typeof(PipesInvocationClient<IBaselineService>)
            .GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        Workload.Check(pending.Count == 0, "Pending invocation leaked.");
        foreach (var field in new[] { "_listenTaskScheduler", "_taskScheduler" })
            Workload.Check(((TmTaskScheduler)typeof(PipesInvocationClient<IBaselineService>)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Completion.IsCompletedSuccessfully,
                "Client worker did not complete.");
        await SendAsync(child, "stop");
        await child.WaitForExitAsync(deadline.Token);
        var serverErrors = await errors;
        Workload.Check(child.ExitCode == 0 && string.IsNullOrEmpty(serverErrors), $"Server exit {child.ExitCode}: {serverErrors}");
        var report = new
        {
            Completed = true, Mode = verify ? "verify" : "measure", Case = descriptor,
            ParentPid = Environment.ProcessId, ChildPid = child.Id, ChildExitCode = child.ExitCode,
            ClientClosed = true, PendingAfterDispose = pending.Count, ClientWorkersCompleted = true,
            Environment = Statistics.EnvironmentInfo(), warmupOperations, warmupSeconds,
            PilotOperations = verify ? 32 : 512, pilotSeconds,
            AllocationScope = "GC.GetTotalAllocatedBytes(true), all threads per process; framework+service+harness/control, not retained memory",
            LatencyScope = "typed await round trip; excludes result validation and rental return",
            ThroughputScope = "closed-loop batch including result validation, rental return, worker/clock overhead",
            Samples = samples
        };
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report));
        Console.WriteLine($"{descriptor.Shape}/c{descriptor.Concurrency} {report.Mode}: passed; {operations} ops/sample.");
    }
    finally
    {
        if (started && !child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
    }
}

static async Task SendAsync(Process process, string command)
{
    await process.StandardInput.WriteLineAsync(command);
    await process.StandardInput.FlushAsync();
}

static async Task<string> ReadLineAsync(Process process)
    => await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))
        ?? throw new EndOfStreamException("Server control channel closed.");
