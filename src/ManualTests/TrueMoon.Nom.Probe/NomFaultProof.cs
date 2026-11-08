using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

internal static class NomFaultProof
{
    private static readonly Guid OldId = Guid.Parse("20aaf451-a15a-4dfc-9934-2c9c693ea831");
    private static readonly Guid NewId = Guid.Parse("c44b6191-32f4-45ea-90f7-8f3ba906fc04");
    private static readonly Guid SourceId = Guid.Parse("3b8d7613-d4a3-4db4-a96f-845971d4c050");
    private static readonly Guid KindId = Guid.Parse("08fa7d7e-7df6-4dc3-af55-97483f412e34");
    private static readonly byte[] OldBytes = [11, 22, 33], NewBytes = [44, 55, 66];
    private static AssetRecord Row(Guid id) => new(id, SourceId, KindId, id == OldId ? "old" : "new", [7, 8]);
    private static AssetResourceWrite Spec(Guid id, byte[] bytes) => new(Row(id), bytes.Length, ResourceCodec.Hash(bytes), bytes.Length);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static async Task<AssetWriteReceipt> Publish(CatalogOwner owner, AssetWriteTicket ticket, Guid id, byte[] bytes)
    { using var input = new MemoryStream(bytes, writable: false); return await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [id] = input }); }
    private static string Snapshot(string root)
    {
        var parts = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "owner.lock").Order(StringComparer.Ordinal))
        { parts.Add(Path.GetRelativePath(root, path)); parts.Add(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))); }
        return CatalogCodec.Key(parts.ToArray());
    }
    internal static async Task<int> Run(string report)
    {
        var passed = new List<string>(); var observations = new List<object>(); var started = 0; var reaped = 0; var killed = 0; string? failure = null;
        var scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "fault-stores-" + Path.GetFileNameWithoutExtension(report));
        object? library = null;
        try
        {
            library = Program.LibraryInfo(); Directory.CreateDirectory(scratch);
            foreach (var phase in new[] { "allocate", "reserve", "commit", "abort", "prune", "recover-abort" })
            foreach (var cut in new[] { "before-write", "partial-write", "after-write", "after-flush" })
            {
                var args = Arguments(phase + "-" + cut); var seed = await Seed(args); AssetWriteTicket? prepared = null;
                if (phase == "recover-abort") prepared = await Kill(args, "prepare", "prepared");
                var issued = await Kill(args, phase, cut) ?? prepared;
                using (var rawFactory = new NomKvStoreFactory())
                using (var raw = rawFactory.Open(new(args.Root, args.ProjectId, args.StoreId, RepairIncompleteTail: false)))
                {
                    var catalog = new NomCatalog(raw); var op = catalog.Operation(2); var next = catalog.Control.Next; var floor = catalog.Control.Floor;
                    if (phase == "allocate") Check(op == null && next == (cut is "before-write" or "partial-write" ? 2 : 3), "allocation high-water exact whole state");
                    if (phase == "reserve") Check(next == 3 && (cut is "before-write" or "partial-write" ? op == null : op?.State == "pending"), "pending batch exact closure boundary");
                    if (phase == "commit") Check(cut is "before-write" or "partial-write" ? raw.Generation == 1 && op?.State == "pending" : raw.Generation == 2 && op?.State == "committed", "publication whole metadata state");
                    if (phase is "abort" or "recover-abort") Check(op?.State == (cut is "before-write" or "partial-write" ? "pending" : "aborted"), "abort exact durable boundary");
                    if (phase == "prune") Check(cut is "before-write" or "partial-write" ? floor == 0 && op?.State == "committed" && catalog.Items(2).Length == 1 : floor == 2 && op == null && catalog.Items(2).Length == 0, "floor/items/outcomes atomic prune");
                    observations.Add(new { Phase = phase, Cut = cut, raw.Sequence, raw.Generation, Next = next, Floor = floor, State = op?.State, raw.RecoveredTailBytes });
                }
                using var factory = new CatalogFactory(); using var reopened = factory.Create(args with { Create = false });
                var hasNew = phase == "prune" || phase == "commit" && cut is "after-write" or "after-flush";
                Check(reopened.Header.Generation == (hasNew ? 2 : 1) && (await reopened.ReadAsync()).Length == (hasNew ? 2 : 1), "no partial visible resource batch");
                await Exact(reopened, seed.Version, OldBytes);
                if (hasNew) await Exact(reopened, (await reopened.FindResourceAsync(NewId))!, NewBytes);
                else Check(await reopened.FindResourceAsync(NewId) == null, "unpublished resource absent");
                if (issued != null)
                {
                    if (phase == "prune" && cut is "after-write" or "after-flush")
                    {
                        await Expired(reopened, issued); await Expired(reopened, seed.Ticket);
                    }
                    else
                    {
                        var outcome = await reopened.LookupResourceOperationAsync(issued);
                        Check(outcome.Status == (hasNew ? "committed" : "aborted"), "resolved exact issued ticket");
                        if (outcome.Status == "committed")
                        {
                            using var forbidden = new ThrowOnRead();
                            var replay = await reopened.PublishResourcesAsync(issued, new Dictionary<Guid, Stream> { [NewId] = forbidden });
                            Check(replay.Generation == 2 && replay.Resources.Single() == outcome.Resources.Single() && forbidden.Reads == 0, "committed replay never rereads input");
                        }
                    }
                }
                if (phase is "allocate" or "reserve")
                {
                    var next = await reopened.ReserveResourcesAsync(1, [Spec(NewId, NewBytes)]);
                    Check(next.Sequence == (phase == "allocate" && cut is "before-write" or "partial-write" ? 2 : 3), "issued sequence never reuses durable allocation");
                    await reopened.AbortResourceOperationAsync(next);
                }
                passed.Add(phase + "/" + cut);
            }
            foreach (var cut in new[] { "before-truncate", "after-truncate", "after-repair-flush" })
            {
                var args = Arguments(cut); var seed = await Seed(args); var ticket = await Kill(args, "prepare", "prepared");
                AppendTail(args); await Kill(args, "repair", cut);
                using var factory = new CatalogFactory(); using var owner = factory.Create(args with { Create = false });
                Check(owner.Header.Generation == 1 && (await owner.LookupResourceOperationAsync(ticket!)).Status == "aborted", "repair/recovery preserves generation and pending outcome");
                await Exact(owner, seed.Version, OldBytes); passed.Add("repair/" + cut);
            }
            foreach (var phase in new[] { "create-segment", "create-pending", "create-catalog" })
            foreach (var cut in new[] { "before-write", "partial-write", "after-write", "after-flush" }) await Creation(phase, cut);
            foreach (var cut in new[] { "before-publish", "after-publish" }) await Creation("create-publish", cut);
            Check(started == 48 && reaped == started && killed == started, "all48 owned children killed/reaped");
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(report, JsonSerializer.Serialize(new { Completed = failure == null, Library = library, Passed = passed, Observations = observations,
            StartedChildren = started, ReapedChildren = reaped, KilledChildren = killed, Failure = failure,
            Scope = "Integrated process-kill acceptance; no physical-volume or power-loss claim" }, new JsonSerializerOptions { WriteIndented = true }));
        return failure == null ? 0 : 1;

        OpenArguments Arguments(string name) => new(Path.Combine(scratch, name), Guid.NewGuid(), Guid.NewGuid(), true);
        async Task Creation(string phase, string cut)
        {
            var args = Arguments(phase + "-" + cut); await Kill(args, phase, cut); var before = Snapshot(args.Root);
            var initialized = phase == "create-catalog" && cut is "after-write" or "after-flush";
            using var factory = new CatalogFactory();
            if (initialized)
            { using var owner = factory.Create(args with { Create = false }); Check(owner.Header.Generation == 0 && (await owner.ReadAsync()).Length == 0, "complete initial catalog reopens"); }
            else
            {
                try { using var unexpected = factory.Create(args with { Create = false }); throw new InvalidOperationException("Uninitialized root accepted"); }
                catch (Exception error) when (error is IOException or InvalidDataException) { }
                Check(Snapshot(args.Root) == before && factory.Count == 0, "interrupted Create not repaired/reset and releases handles");
            }
            try { using var unexpected = factory.Create(args); throw new InvalidOperationException("Nonempty root reset"); }
            catch (InvalidDataException) { }
            Check(Snapshot(args.Root) == before, "Create preserves interrupted/initialized files");
            passed.Add(phase + "/" + cut);
        }
        async Task<AssetWriteTicket?> Kill(OpenArguments args, string phase, string cut)
        {
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { Assembly.GetExecutingAssembly().Location, "fault-child", args.Root, args.ProjectId.ToString(), args.StoreId.ToString(), phase, cut }) info.ArgumentList.Add(value);
            using var child = new Process { StartInfo = info }; var launched = false; var observedExit = false;
            try
            {
                Check(child.Start(), "child starts"); launched = true; started++; var errors = child.StandardError.ReadToEndAsync(); AssetWriteTicket? ticket = null;
                for (var i = 0; ; i++)
                {
                    Check(i < 3, "bounded child protocol"); var line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    if (line?.StartsWith("ticket:", StringComparison.Ordinal) == true) { ticket = JsonSerializer.Deserialize<AssetWriteTicket>(line[7..]); continue; }
                    Check(line == "cut:" + cut && !child.HasExited, "child reached exact live cutpoint: " + phase + "/" + cut); break;
                }
                try { using var unexpected = new FileStream(Path.Combine(args.Root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); throw new InvalidOperationException("Child released lock early"); }
                catch (IOException) { }
                child.Kill(entireProcessTree: true); killed++; await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); observedExit = true; reaped++;
                Check((await errors).Length == 0, "child failed before controlled kill"); return ticket;
            }
            finally
            {
                if (launched && !observedExit)
                { if (!child.HasExited) { child.Kill(entireProcessTree: true); killed++; } await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); reaped++; }
            }
        }
    }
    private static async Task<(AssetWriteTicket Ticket, AssetResourceVersion Version)> Seed(OpenArguments args)
    {
        using var factory = new CatalogFactory(); using var owner = factory.Create(args);
        var ticket = await owner.ReserveResourcesAsync(0, [Spec(OldId, OldBytes)]);
        return (ticket, (await Publish(owner, ticket, OldId, OldBytes)).Resources.Single());
    }
    private static async Task Exact(CatalogOwner owner, AssetResourceVersion version, byte[] expected)
    { await using var stream = await owner.OpenResourceAsync(version); using var copy = new MemoryStream(); await stream.CopyToAsync(copy); Check(copy.ToArray().AsSpan().SequenceEqual(expected), "exact committed bytes survive kill"); }
    private static async Task Expired(CatalogOwner owner, AssetWriteTicket ticket)
    { try { await owner.LookupResourceOperationAsync(ticket); } catch (ExpiredOperationException) { return; } throw new InvalidDataException("Pruned ticket replayed"); }
    private static void AppendTail(OpenArguments args)
    {
        byte[] frame;
        using (var factory = new NomKvStoreFactory()) using (var raw = factory.Open(new(args.Root, args.ProjectId, args.StoreId)))
            frame = NomJournalCodec.EncodeBatch(new(args.ProjectId, args.StoreId), new(raw.Sequence + 1, raw.Generation, raw.Generation, Guid.NewGuid(), [new(NomCatalogKeys.Control, raw.Get(NomCatalogKeys.Control))]));
        using var file = new FileStream(Path.Combine(args.Root, "journal", "00000000000000000001.nom"), FileMode.Append); file.Write(frame.AsSpan(0, 100));
    }
    internal static int Child(string[] args)
    { try { return ChildAsync(args).GetAwaiter().GetResult(); } catch (Exception error) { Console.Error.WriteLine(error); return 1; } }
    private static async Task<int> ChildAsync(string[] args)
    {
        var phase = args[4]; var cut = args[5]; var files = new CrashFiles(phase, cut);
        var options = new OpenArguments(args[1], Guid.Parse(args[2]), Guid.Parse(args[3]), phase.StartsWith("create-", StringComparison.Ordinal));
        using var factory = new CatalogFactory(fileOperations: files);
        if (options.Create || phase is "recover-abort" or "repair") files.Armed = true;
        using var owner = factory.Create(options);
        if (phase is "allocate" or "reserve") files.Armed = true;
        var ticket = await owner.ReserveResourcesAsync(1, [Spec(NewId, NewBytes)]);
        Console.WriteLine("ticket:" + JsonSerializer.Serialize(ticket)); Console.Out.Flush();
        if (phase == "prepare") Cut("prepared");
        if (phase is "commit" or "abort") files.Armed = true;
        if (phase == "abort") await owner.AbortResourceOperationAsync(ticket);
        else
        {
            await Publish(owner, ticket, NewId, NewBytes);
            if (phase == "prune") { files.Armed = true; await owner.PruneResourceOperationsAsync(ticket.Sequence); }
        }
        throw new InvalidOperationException("Cutpoint was not reached: " + phase + "/" + cut);
    }
    private static void Cut(string point)
    { Console.WriteLine("cut:" + point); Console.Out.Flush(); using var never = new ManualResetEvent(false); never.WaitOne(); }
    private sealed class CrashFiles(string phase, string cut) : NomFileOperations
    {
        internal bool Armed; private int writes; private bool matched, truncated;
        internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
        {
            var kind = Path.GetFileName(file.Name) == "nom.root.pending" ? "pending" : "journal";
            var wanted = phase == "create-pending" ? "pending" : "journal";
            var ordinal = phase is "reserve" or "create-catalog" ? 2 : 1;
            if (!Armed || phase is "repair" or "create-publish" || kind != wanted || ++writes != ordinal) { base.Write(file, bytes); return; }
            matched = true;
            if (cut == "before-write") Cut(cut);
            if (cut == "partial-write") { base.Write(file, bytes[..Math.Min(100, bytes.Length)]); Cut(cut); }
            base.Write(file, bytes); if (cut == "after-write") Cut(cut);
        }
        internal override void Flush(FileStream file)
        { base.Flush(file); if (Armed && (matched && cut == "after-flush" || truncated && cut == "after-repair-flush")) Cut(cut); }
        internal override void Truncate(FileStream file, long length)
        { if (Armed && phase == "repair" && cut == "before-truncate") Cut(cut); base.Truncate(file, length); truncated = true; if (Armed && phase == "repair" && cut == "after-truncate") Cut(cut); }
        internal override void PublishRoot(string source, string destination)
        { if (Armed && phase == "create-publish" && cut == "before-publish") Cut(cut); base.PublishRoot(source, destination); if (Armed && phase == "create-publish" && cut == "after-publish") Cut(cut); }
    }
    private sealed class ThrowOnRead : Stream
    {
        internal int Reads;
        public override bool CanRead => true; public override bool CanWrite => false; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { Reads++; throw new IOException("Replay read replacement input"); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { Reads++; return ValueTask.FromException<int>(new IOException("Replay read replacement input")); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
