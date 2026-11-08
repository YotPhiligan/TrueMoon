using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

internal static class NomMaintenanceProof
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static readonly string[] Cuts = ["cp-write-before", "cp-write-partial", "cp-write-after", "cp-flush-after", "cp-publish-before", "cp-publish-after",
        "segment-write-before", "segment-write-partial", "segment-write-after", "segment-flush-after", "segment-publish-before", "segment-publish-after",
        "root-write-before", "root-write-partial", "root-write-after", "root-flush-after", "root-close-before", "root-close-after", "root-switch-before", "root-switch-after",
        "cleanup-close-before", "cleanup-close-after", "cleanup-delete-before", "cleanup-delete-after"];
    private static NomJournalBatch First() => new(1, 0, 1, Guid.Parse("e8317946-78b5-48a5-bd74-e957fe064caa"), [new([1], [11]), new([2], [22])]);
    private static NomJournalBatch Second() => new(2, 1, 2, Guid.Parse("004ad8fe-01f9-4fe7-8faa-fb5de61d9542"), [new([1], [33]), new([2], null), new([3], [])]);
    private static void State(NomKvStore owner)
    { Check(owner.Sequence == 2 && owner.Generation == 2 && owner.Get([1])!.AsSpan().SequenceEqual(new byte[] { 33 }) && owner.Get([2]) == null && owner.Get([3]) is { Length: 0 }, "exact live/deleted/empty state"); }
    internal static async Task<int> Run(string report)
    {
        var passed = new List<string>(); var observations = new List<object>(); var started = 0; var killed = 0; var reaped = 0; string? failure = null; object? library = null;
        var scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "maintenance-" + Path.GetFileNameWithoutExtension(report));
        try
        {
            library = Program.LibraryInfo(); Directory.CreateDirectory(scratch);
            foreach (var cut in Cuts)
            {
                var args = Arguments(cut); Seed(args); await Kill(args, cut);
                var root = NomRootCodec.Decode(File.ReadAllBytes(Path.Combine(args.Root, "nom.root")), args.ProjectId, args.StoreId);
                var selectedNew = cut == "root-switch-after" || cut.StartsWith("cleanup-", StringComparison.Ordinal);
                Check(root.Epoch == (selectedNew ? 2 : 1), "exact selected epoch at " + cut);
                using var factory = new NomKvStoreFactory(); using var owner = factory.Open(args); State(owner);
                var cleanup = owner.Compact(); Check(cleanup.Epoch == 2 && !cleanup.CleanupPending, "retry compaction/cleanup selects complete file set"); State(owner);
                Check(owner.LookupOutcome(Ticket(args, First())) == NomKvWriteOutcome.Expired, "compacted physical ticket expires explicitly");
                var repeat = owner.Compact(); Check(!repeat.Changed && repeat.Epoch == 2 && !repeat.CleanupPending, "no-op repeat keeps epoch");
                Check(Directory.EnumerateFiles(Path.Combine(args.Root, "journal")).Count() == 1 && Directory.EnumerateFiles(Path.Combine(args.Root, "checkpoints")).Count() == 1 &&
                    !File.Exists(Path.Combine(args.Root, "nom.root.switch")), "obsolete/prepared file set cleaned after actual reopen");
                observations.Add(new { Cut = cut, SelectedEpoch = root.Epoch, cleanup.CheckpointBytes, cleanup.Keys }); passed.Add(cut);
            }
            {
                var args = Arguments("post-checkpoint"); Seed(args); using var factory = new NomKvStoreFactory(); var owner = factory.Open(args);
                var first = owner.Compact(); Check(first.Changed && first.Epoch == 2, "first checkpoint selected");
                var appended = owner.WriteBatch(new(3, 2, 2, Guid.NewGuid(), [new([1], [44]), new([3], null)])); owner.Dispose();
                owner = factory.Open(args); Check(owner.Sequence == 3 && owner.Generation == 2 && owner.Get([1])![0] == 44 && owner.Get([3]) == null &&
                    owner.LookupOutcome(appended.Ticket) == NomKvWriteOutcome.Committed, "checkpoint plus following journal continuity");
                var second = owner.Compact(); Check(second.Epoch == 3 && second.Changed && owner.LookupOutcome(appended.Ticket) == NomKvWriteOutcome.Expired, "second checkpoint advances physical floor");
                owner.Dispose(); using var reopened = factory.Open(args); Check(reopened.Sequence == 3 && reopened.Get([1])![0] == 44 && reopened.Get([2]) == null && reopened.Get([3]) == null, "deletes never resurrect from discarded journals");
                passed.Add("PostCheckpointAppendAndSecondEpoch");
            }
            await PublicCatalog(); passed.Add("RetainedResourceOutcomesAndCas");
            {
                var args = Arguments("corrupt-selected"); Seed(args); var files = new RetainFiles();
                using (var factory = new NomKvStoreFactory(files)) using (var owner = factory.Open(args)) Check(owner.Compact().CleanupPending, "old set retained for corruption witness");
                var root = NomRootCodec.Decode(File.ReadAllBytes(Path.Combine(args.Root, "nom.root")), args.ProjectId, args.StoreId);
                var checkpoint = Path.Combine(args.Root, "checkpoints", root.CheckpointDigest + ".nom"); var bytes = File.ReadAllBytes(checkpoint); var corrupt = bytes.ToArray(); corrupt[0] ^= 1; File.WriteAllBytes(checkpoint, corrupt);
                var before = Snapshot(args.Root);
                using (var factory = new NomKvStoreFactory())
                {
                    try { using var unexpected = factory.Open(args); throw new InvalidOperationException("Corrupt selected checkpoint accepted"); } catch (InvalidDataException) { }
                    Check(factory.ActiveStoreCount == 0 && Snapshot(args.Root) == before, "selected corruption cannot fallback/reset/delete old set");
                }
                File.WriteAllBytes(checkpoint, bytes); using var recoveredFactory = new NomKvStoreFactory(); using var recovered = recoveredFactory.Open(args); State(recovered);
                Check(!recovered.Compact().CleanupPending, "explicit validated cleanup retry"); passed.Add("SelectedCorruptionNoFallbackOrMutation");
            }
            Check(started == Cuts.Length && killed == started && reaped == started, "all maintenance children killed/reaped");
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(report, JsonSerializer.Serialize(new { Completed = failure == null, Library = library, Passed = passed, Observations = observations,
            StartedChildren = started, KilledChildren = killed, ReapedChildren = reaped, Failure = failure,
            Scope = "Explicit offline checkpoint/rotation/compaction; process kill is not physical-volume/power-loss acceptance" }, new JsonSerializerOptions { WriteIndented = true }));
        return failure == null ? 0 : 1;

        NomKvOpenArguments Arguments(string name) => new(Path.Combine(scratch, name), Guid.NewGuid(), Guid.NewGuid());
        async Task PublicCatalog()
        {
            var args = Arguments("catalog"); using var factory = new CatalogFactory(); var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true));
            var row = new AssetRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "retained", [1, 2]); var bytes = new byte[] { 4, 5, 6 }; var spec = new AssetResourceWrite(row, 3, ResourceCodec.Hash(bytes), 3);
            var ticket = await owner.ReserveResourcesAsync(0, [spec]); using var input = new MemoryStream(bytes);
            var version = (await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [row.Id] = input })).Resources.Single();
            Check((await owner.CompactAsync()).Changed && owner.Header.Generation == 1, "public catalog compaction keeps content generation"); owner.Dispose();
            owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, false));
            Check((await owner.LookupResourceOperationAsync(ticket)).Status == "committed" && await owner.FindResourceAsync(row.Id) == version, "public operation retained across checkpoint");
            await owner.PruneResourceOperationsAsync(ticket.Sequence); await owner.CompactAsync(); owner.Dispose();
            using var reopened = factory.Create(new(args.Root, args.ProjectId, args.StoreId, false));
            try { await reopened.LookupResourceOperationAsync(ticket); throw new InvalidOperationException("Expired public ticket revived"); } catch (ExpiredOperationException) { }
            await using var stream = await reopened.OpenResourceAsync(version); using var content = new MemoryStream(); await stream.CopyToAsync(content);
            Check(content.ToArray().AsSpan().SequenceEqual(bytes), "retained CAS exact bytes after prune/checkpoints");
        }
        async Task Kill(NomKvOpenArguments args, string cut)
        {
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { Assembly.GetExecutingAssembly().Location, "maintenance-child", args.Root, args.ProjectId.ToString(), args.StoreId.ToString(), cut }) info.ArgumentList.Add(value);
            using var child = new Process { StartInfo = info }; var launched = false; var exited = false;
            try
            {
                Check(child.Start(), "maintenance child starts"); launched = true; started++; var errors = child.StandardError.ReadToEndAsync();
                var line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)); Check(line == "cut:" + cut && !child.HasExited, "exact live maintenance cutpoint");
                try { using var unexpected = new FileStream(Path.Combine(args.Root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); throw new InvalidOperationException("Maintenance released root early"); } catch (IOException) { }
                child.Kill(true); killed++; await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); exited = true; reaped++; Check((await errors).Length == 0, "maintenance child stderr");
            }
            finally { if (launched && !exited) { if (!child.HasExited) { child.Kill(true); killed++; } await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); reaped++; } }
        }
    }
    private static void Seed(NomKvOpenArguments args)
    { using var factory = new NomKvStoreFactory(); using var owner = factory.Create(args); owner.WriteBatch(First()); owner.WriteBatch(Second()); }
    private static NomKvWriteTicket Ticket(NomKvOpenArguments args, NomJournalBatch batch)
    { var bytes = NomJournalCodec.EncodeBatch(new(args.ProjectId, args.StoreId), batch); return new(args.ProjectId, args.StoreId, batch.Sequence, batch.OperationId, Convert.ToHexStringLower(SHA256.HashData(bytes))); }
    private static string Snapshot(string root) => CatalogCodec.Key(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "owner.lock").Order(StringComparer.Ordinal)
        .SelectMany(p => new[] { Path.GetRelativePath(root, p), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray());
    internal static int Child(string[] args)
    {
        try { using var factory = new NomKvStoreFactory(new CrashFiles(args[4])); using var owner = factory.Open(new(args[1], Guid.Parse(args[2]), Guid.Parse(args[3]))); owner.Compact(); throw new InvalidOperationException("Maintenance cut not reached"); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private sealed class RetainFiles : NomKvFileOperations { internal override void DeleteFile(string path) => throw new IOException("Retain obsolete set for corruption witness"); }
    private sealed class CrashFiles(string cut) : NomKvFileOperations
    {
        private bool hit;
        private static string Kind(string path) => Path.GetFileName(path) == "nom.root.switch" ? "root" : Path.GetDirectoryName(path)!.EndsWith("checkpoints", StringComparison.Ordinal) ? "cp" : "segment";
        private void At(string point) { if (!hit && point == cut) { hit = true; Console.WriteLine("cut:" + cut); Console.Out.Flush(); using var never = new ManualResetEvent(false); never.WaitOne(); } }
        internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
        {
            var kind = Kind(file.Name); At(kind + "-write-before");
            if (cut == kind + "-write-partial") { base.Write(file, bytes[..Math.Min(100, bytes.Length)]); At(cut); }
            base.Write(file, bytes); At(kind + "-write-after");
        }
        internal override void Flush(FileStream file) { base.Flush(file); At(Kind(file.Name) + "-flush-after"); }
        internal override void PublishRoot(string source, string destination) { var kind = Kind(source); At(kind + "-publish-before"); base.PublishRoot(source, destination); At(kind + "-publish-after"); }
        internal override void ReplaceRoot(string source, string destination) { At("root-switch-before"); base.ReplaceRoot(source, destination); At("root-switch-after"); }
        internal override void Close(FileStream file)
        {
            var name = Path.GetFileName(file.Name); var point = name == "nom.root" ? "root-close" : name == "00000000000000000001.nom" ? "cleanup-close" : "unused";
            At(point + "-before"); base.Close(file); At(point + "-after");
        }
        internal override void DeleteFile(string path) { At("cleanup-delete-before"); base.DeleteFile(path); At("cleanup-delete-after"); }
    }
}
