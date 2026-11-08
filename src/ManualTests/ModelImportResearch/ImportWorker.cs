#if NETTERLOW
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ModelImportResearch;

internal sealed record WorkerResult(string Revision, string Fingerprint, string[] Used, long ReadBytes, int LiveStreams, int LiveImports);

internal static class ImportWorker
{
    private static int active;
    internal static int Starts { get; private set; }
    internal static int Reaped { get; private set; }
    // On failed OS cleanup retain the owner and packet; never report cancellation as completed.
    private static readonly List<(Process Process, ImportJob? Job, CapturedInput Input, string Packet)> retained = [];
    internal static async Task<string?> ReadLineLimited(TextReader reader, CancellationToken token)
    {
        var text = new StringBuilder(); var character = new char[1];
        while (await reader.ReadAsync(character.AsMemory(), token) != 0)
        {
            if (character[0] == '\n') return text.ToString().TrimEnd('\r');
            if (text.Length == 16384) throw new InvalidDataException("Worker line quota.");
            text.Append(character[0]);
        }
        return text.Length == 0 ? null : text.ToString();
    }
    internal static async Task<(string Text, bool Overflow)> ReadErrors(TextReader reader)
    {
        var text = new StringBuilder(); var buffer = new char[1024]; var overflow = false; int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
        {
            var keep = Math.Min(count, 8192 - text.Length); text.Append(buffer, 0, keep);
            overflow |= keep != count; // Drain without growing the retained diagnostic.
        }
        return (text.ToString(), overflow);
    }

    internal static async Task<WorkerResult> Run(CapturedInput input, string scratch, string mode,
        CancellationToken token, TimeSpan timeout, Action<string>? signal = null, string executable = "dotnet")
    {
        token.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("Import capacity: one worker, no queue.");
        Process? process = null; ImportJob? job = null; Task<(string Text, bool Overflow)>? errors = null;
        var started = false; var exited = false; var packet = Path.Combine(scratch, Guid.NewGuid().ToString("N") + ".capture");
        try
        {
            token.ThrowIfCancellationRequested(); input.Write(packet);
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = scratch };
            foreach (var arg in new[] { Assembly.GetExecutingAssembly().Location, "worker", packet, mode }) info.ArgumentList.Add(arg);
            process = new() { StartInfo = info };
            if (!process.Start()) throw new InvalidOperationException("Worker start failed.");
            started = true; Starts++;
            errors = ReadErrors(process.StandardError);
            using var deadline = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            WorkerResult? result = null;
            try
            {
                var ready = await ReadLineLimited(process.StandardOutput, linked.Token);
                if (ready != "ready") throw new InvalidDataException("Worker admission handshake failed.");
                job = ImportJob.Attach(process); // Native library is not activated until the job is installed.
                token.ThrowIfCancellationRequested();
                await process.StandardInput.WriteLineAsync("go"); await process.StandardInput.FlushAsync();
                var lines = 0;
                while (await ReadLineLimited(process.StandardOutput, linked.Token) is { } line)
                {
                    if (++lines > 8 || line.Length > 16384) throw new InvalidDataException("Worker output quota.");
                    if (line.StartsWith("result ", StringComparison.Ordinal)) result = JsonSerializer.Deserialize<WorkerResult>(line[7..]);
                    else signal?.Invoke(line);
                }
                await process.WaitForExitAsync(linked.Token); exited = true;
                token.ThrowIfCancellationRequested();
                var diagnostic = await errors;
                if (process.ExitCode != 0 || diagnostic.Overflow) throw new InvalidDataException("Worker failed: " + diagnostic.Text);
                if (result == null || result.Revision != input.Revision || result.LiveStreams != 0 || result.LiveImports != 0)
                    throw new InvalidDataException("Incomplete worker result.");
                return result;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { throw new TimeoutException("Native import deadline exceeded."); }
        }
        finally
        {
            try
            {
                if (started && process != null && !exited)
                {
                    if (!process.HasExited)
                    {
                        if (job != null) job.Terminate(); else process.Kill(true);
                    }
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    exited = process.HasExited;
                }
                if (started && exited) Reaped++;
                if (errors != null) _ = await errors.WaitAsync(TimeSpan.FromSeconds(10));
                job?.Dispose(); process?.Dispose();
                File.Delete(packet);
                Volatile.Write(ref active, 0);
            }
            catch
            {
                if (process != null) retained.Add((process, job, input, packet));
                // Keep capacity occupied until this owner can actually be drained.
                throw new InvalidOperationException("Worker cleanup failed; owner and capture retained.");
            }
        }
    }

    internal static int Execute(string[] args)
    {
        Console.WriteLine("ready"); Console.Out.Flush();
        if (Console.ReadLine() != "go") return 2;
        var activated = false;
        try
        {
            var input = CapturedInput.Read(args[1], new());
            if (args[2] == "memory")
            {
                ImportJob.ProveCommitDenial();
                Console.WriteLine("result " + JsonSerializer.Serialize(new WorkerResult(input.Revision, "memory-denied", [], 0, 0, 0)));
                return 0;
            }
            Candidate.Activate(); activated = true; _ = Program.NativeInfo();
            Action? block = args[2] == "block" ? () =>
            {
                Console.WriteLine("read"); Console.Out.Flush();
                Thread.Sleep(Timeout.Infinite); // Deterministic stuck native callback, killed by the owning job.
            } : null;
            WorkerResult result;
            using (var io = new CapturedIo(input, new(), block))
            {
                var imported = Candidate.Import(input.Root, io.Pointer);
                io.ThrowIfFailed();
                Projection.Verify(imported, input.Root);
                result = new(input.Revision, Projection.Fingerprint(imported), io.Used.Order(StringComparer.Ordinal).ToArray(), io.ReadBytes, io.LiveStreams, Candidate.LiveImports);
            }
            Candidate.Shutdown(); activated = false;
            Console.WriteLine("result " + JsonSerializer.Serialize(result)); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
        finally { if (activated) Candidate.Shutdown(); }
    }
}
#endif
