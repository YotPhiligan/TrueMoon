using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

internal static class NomKvProof
{
    private static readonly Guid Project = Guid.Parse("147d791a-44a0-4fbe-a0d3-cbb227b5ad12");
    private static readonly Guid Store = Guid.Parse("49ecc982-e416-4ac2-8e61-de995fe1ae80");
    private static readonly Guid SeedOperation = Guid.Parse("a49ae147-556f-4cda-bb73-e9bc194c921f");
    private static readonly Guid Operation = Guid.Parse("bd1738e4-cfe7-4053-a9cf-605933d6aff0");
    private static NomJournalBatch Seed() => new(1, 0, 1, SeedOperation, [new([1], [10]), new([2], [20])]);
    private static NomJournalBatch Candidate() => new(2, 1, 2, Operation, [new([1], [11, 12]), new([2], [21, 22])]);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static async Task<int> Run(string reportPath)
    {
        var passed = new List<string>(); var cuts = new List<object>(); var started = 0; var reaped = 0; string? failure = null;
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "kv-stores-" + Path.GetFileNameWithoutExtension(reportPath));
        object? library = null;
        try
        {
            library = Program.LibraryInfo(); Directory.CreateDirectory(directory);
            var args = new NomKvOpenArguments(Path.Combine(directory, "roundtrip"), Project, Store);
            using (var factory = new NomKvStoreFactory())
            {
                var owner = factory.Create(args); var first = owner.WriteBatch(Seed());
                Check(owner.Get([1])!.SequenceEqual(new byte[] { 10 }) && owner.Get([9]) == null, "exact KV lookup");
                var detached = owner.GetMany([new byte[] { 1 }, new byte[] { 2 }]); detached.Values[0]![0] = 99;
                Check(owner.Get([1])![0] == 10 && detached.Sequence == 1 && detached.Generation == 1, "detached consistent view");
                var second = owner.WriteBatch(new(2, 1, 1, Operation, [new([1], null), new([3], [])]));
                Check(owner.Get([1]) == null && owner.Get([3]) is { Length: 0 } && second.Generation == 1, "delete/empty put/metadata generation");
                owner.Dispose(); using var reopened = factory.Open(args);
                Check(reopened.Sequence == 2 && reopened.Generation == 1 && reopened.LookupOutcome(first.Ticket) == NomKvWriteOutcome.Committed &&
                    reopened.LookupOutcome(second.Ticket) == NomKvWriteOutcome.Committed && reopened.Get([2])![0] == 20, "durable reopen and outcomes");
            }
            passed.Add("CreateGetWriteBatchReopen");
            var locked = new NomKvOpenArguments(Path.Combine(directory, "lock"), Project, Store);
            using (var factory = new NomKvStoreFactory())
            {
                var owner = factory.Create(locked); Check(await ChildLine("open", locked, null) == "denied", "live owner excludes other process");
                owner.Dispose(); Check(await ChildLine("open", locked, null) == "opened", "other process opens after release");
            }
            passed.Add("ExclusiveRootAcrossProcesses");
            foreach (var point in new[] { "before-write", "partial-write", "after-write", "after-flush" })
            {
                var arguments = new NomKvOpenArguments(Path.Combine(directory, point), Project, Store);
                using (var factory = new NomKvStoreFactory()) using (var seed = factory.Create(arguments)) _ = seed.WriteBatch(Seed());
                _ = await ChildLine("crash", arguments, point);
                using var recoveredFactory = new NomKvStoreFactory(); using var recovered = recoveredFactory.Open(arguments);
                var view = recovered.GetMany([new byte[] { 1 }, new byte[] { 2 }]);
                var old = view.Sequence == 1 && view.Generation == 1 && view.Values[0]!.SequenceEqual(new byte[] { 10 }) && view.Values[1]!.SequenceEqual(new byte[] { 20 });
                var next = view.Sequence == 2 && view.Generation == 2 && view.Values[0]!.SequenceEqual(new byte[] { 11, 12 }) && view.Values[1]!.SequenceEqual(new byte[] { 21, 22 });
                Check(old || next, "whole old/new state after process kill");
                if (point == "before-write" || point == "partial-write") Check(old, "unwritten/incomplete batch not published");
                if (point == "after-flush") Check(next, "flushed batch survives process kill");
                var frame = NomJournalCodec.EncodeBatch(new(Project, Store), Candidate());
                var ticket = new NomKvWriteTicket(Project, Store, 2, Operation, Convert.ToHexStringLower(SHA256.HashData(frame)));
                Check(recovered.LookupOutcome(ticket) == (next ? NomKvWriteOutcome.Committed : NomKvWriteOutcome.NotCommitted), "kill outcome resolution");
                cuts.Add(new { Point = point, Sequence = recovered.Sequence, Generation = recovered.Generation, recovered.RecoveredTailBytes, WholeState = next ? "new" : "old" });
                passed.Add("Kill-" + point);
            }
            Check(started == 6 && reaped == started, "all child processes reaped");
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Completed = failure == null, Library = library, Passed = passed, Cuts = cuts,
            StartedChildren = started, ReapedChildren = reaped, Failure = failure, Scope = "Own KV core; public catalog and App are covered by Core and public App profiles" }, new JsonSerializerOptions { WriteIndented = true }));
        return failure == null ? 0 : 1;

        async Task<string> ChildLine(string mode, NomKvOpenArguments arguments, string? point)
        {
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Assembly.GetExecutingAssembly().Location, "kv-child", mode, arguments.Root, arguments.ProjectId.ToString(), arguments.StoreId.ToString(), point ?? "none" }) info.ArgumentList.Add(arg);
            using var process = new Process { StartInfo = info }; var launched = false; var observedExit = false;
            try
            {
                if (!process.Start()) throw new InvalidOperationException("KV child start failed."); launched = true; started++;
                var stderr = process.StandardError.ReadToEndAsync();
                var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                if (mode == "crash")
                {
                    Check(line == "cut:" + point, "child reached actual crash cutpoint");
                    try { using var unexpected = new FileStream(Path.Combine(arguments.Root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); throw new InvalidOperationException("Child root lock released early."); }
                    catch (IOException) { }
                    process.Kill(entireProcessTree: true);
                }
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); observedExit = true; reaped++;
                Check((await stderr).Length == 0 && (mode == "crash" || process.ExitCode == 0), "KV child exit/stderr"); return line ?? "no-output";
            }
            finally
            {
                if (launched && !observedExit)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); reaped++;
                }
            }
        }
    }
    internal static int Child(string[] args)
    {
        try
        {
            var files = new CrashFiles(args[5]); using var factory = new NomKvStoreFactory(files);
            using var owner = factory.Open(new(args[2], Guid.Parse(args[3]), Guid.Parse(args[4])));
            if (args[1] == "open") { Console.WriteLine("opened"); return 0; }
            files.Armed = true; owner.WriteBatch(Candidate()); throw new InvalidOperationException("Crash cutpoint was not reached.");
        }
        catch (IOException) when (args[1] == "open") { Console.WriteLine("denied"); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private sealed class CrashFiles(string point) : NomKvFileOperations
    {
        internal bool Armed;
        internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
        {
            if (!Armed) { base.Write(file, bytes); return; }
            if (point == "before-write") Cut(point);
            if (point == "partial-write") { base.Write(file, bytes[..17]); Cut(point); }
            base.Write(file, bytes); if (point == "after-write") Cut(point);
        }
        internal override void Flush(FileStream file) { base.Flush(file); if (Armed && point == "after-flush") Cut(point); }
        private static void Cut(string point)
        {
            Console.WriteLine("cut:" + point); Console.Out.Flush(); using var never = new ManualResetEvent(false); never.WaitOne();
        }
    }
}
