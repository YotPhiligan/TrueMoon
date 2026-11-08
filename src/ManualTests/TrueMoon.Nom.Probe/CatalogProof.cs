using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using JsonSerializer = System.Text.Json.JsonSerializer;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

internal static class CatalogProof
{
    private static readonly List<string> passed = [];
    private static readonly List<object> observations = [];
    private static readonly Guid Source = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Kind = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static void Check(bool value, string detail) { if (!value) throw new InvalidDataException("Proof assertion: " + detail); }
    private static T Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T ex) { return ex; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T ex) { return ex; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    private static AssetRecord Row(string locator, int bytes = 3, Guid? id = null) => new(id ?? Guid.NewGuid(), Source, Kind, locator, Enumerable.Repeat((byte)7, bytes).ToArray());
    private static OpenArguments Args(string scratch, string name, CatalogLimits? limits = null) => new(Path.Combine(scratch, name), Guid.NewGuid(), Guid.NewGuid(), true, limits);
    private static string Hash(string root)
    {
        var parts = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(path=>Path.GetFileName(path)!="owner.lock").Order(StringComparer.Ordinal))
        { using var stream = new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete); parts.Add(Path.GetRelativePath(root,file)); parts.Add(Convert.ToHexString(SHA256.HashData(stream))); }
        return CatalogCodec.Key(parts.ToArray());
    }
    private static void Passed(string name) { passed.Add(name); Console.WriteLine(name + " passed"); }

    internal static async Task<int> Run(string output)
    {
        var scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "stores-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
        object? library = null; string? failure = null; var complete = false;
        try
        {
            library = Program.LibraryInfo();
            CodecCultureAndIntegrity(); Passed(nameof(CodecCultureAndIntegrity));
            await CreateOpenAndIdentity(scratch); Passed(nameof(CreateOpenAndIdentity));
            await FailedOpenPreservesCatalog(scratch); Passed(nameof(FailedOpenPreservesCatalog));
            await OwnerThreadRollbackAndReopen(scratch); Passed(nameof(OwnerThreadRollbackAndReopen));
            await ExclusiveRootAndDirectProcesses(scratch); Passed(nameof(ExclusiveRootAndDirectProcesses));
            await SerializedBudgetBoundaries(scratch); Passed(nameof(SerializedBudgetBoundaries));
            await DetachedResponseBoundaries(scratch); Passed(nameof(DetachedResponseBoundaries));
            await QueueCancelAndStop(scratch); Passed(nameof(QueueCancelAndStop));
            await FailedStartupAndFactoryCapacity(scratch); Passed(nameof(FailedStartupAndFactoryCapacity));
            await FactoryStartupStopAndDrain(scratch); Passed(nameof(FactoryStartupStopAndDrain));
            foreach(var name in await StreamingProof.Run(scratch)) Passed(name);
            observations.AddRange(StreamingProof.Observations);
            complete = true;
        }
        catch (Exception ex) { failure = ex.ToString(); Console.Error.WriteLine(ex); }
        finally
        {
            File.WriteAllText(output, JsonSerializer.Serialize(new { Completed = complete, Library = library, Passed = passed, Observations = observations, Failure = failure }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return complete ? 0 : 1;
    }

    private static void CodecCultureAndIntegrity()
    {
        var original = new byte[] { 1, 2, 3 };
        var row = new AssetRecord(Guid.NewGuid(), Source, Kind, "Body", original); original[0] = 99;
        Check(row.Metadata.SequenceEqual(new byte[] { 1, 2, 3 }), "caller array copied");
        var bytes = CatalogCodec.Encode(row); var decoded = CatalogCodec.Decode(bytes); var returned = decoded.Metadata; returned[0] = 33;
        Check(decoded.Metadata.SequenceEqual(new byte[] { 1, 2, 3 }) && CatalogCodec.Encode(decoded).SequenceEqual(bytes), "detached immutable codec roundtrip");
        Check(CatalogCodec.Binding(row) == CatalogCodec.Key("TrueMoon.Nom/binding/1", Source.ToString("N"), Kind.ToString("N"), "Body"), "versioned binding domain");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "tr-TR", "ru-RU" })
            { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); Check(CatalogCodec.Encode(row).SequenceEqual(bytes), "culture invariant serialization/key"); }
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Check(CatalogCodec.Binding(Row("A")) != CatalogCodec.Binding(Row("a")), "ordinal locator equality");
        Check(CatalogCodec.Key("ab", "c") != CatalogCodec.Key("a", "bc"), "composite framing avoids concatenation aliases");
        Check(CatalogCodec.Key("first", "second") != CatalogCodec.Key("second", "first"), "field roles ordered");
        foreach(var offset in new[] {0,8,10}) { var malformed=bytes.ToArray(); malformed[offset]^=1; Throws<InvalidDataException>(()=>CatalogCodec.Decode(malformed)); }
        Throws<InvalidDataException>(() => CatalogCodec.Decode(new byte[CatalogCodec.MaximumDocument + 1]));
        Throws<InvalidDataException>(() => CatalogCodec.Decode(bytes[..^1]));
        Throws<InvalidDataException>(() => CatalogCodec.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Throws<InvalidDataException>(() => new AssetRecord(Guid.Empty, Source, Kind, "a", []));
        Throws<InvalidDataException>(() => Row("\0"));
        Throws<InvalidDataException>(() => Row(" "));
        foreach (var limits in new[] { new CatalogLimits(QueueCount:0), new CatalogLimits(QueueCount:33), new CatalogLimits(QueueBytes:0), new CatalogLimits(QueueBytes:16777217), new CatalogLimits(BatchRows:0), new CatalogLimits(BatchRows:257), new CatalogLimits(BatchBytes:0), new CatalogLimits(BatchBytes:4194305), new CatalogLimits(DocumentBytes:0), new CatalogLimits(DocumentBytes:131073) })
            Throws<InvalidDataException>(limits.Validate);
    }

    private static async Task CreateOpenAndIdentity(string scratch)
    {
        using var factory = new CatalogFactory(); var request = Args(scratch, "identity;literal=path");
        Throws<IOException>(() => factory.Create(request with { Create = false }));
        Check(!Directory.Exists(request.Root) && factory.Count == 0, "Open missing root creates nothing");
        var seed = Row("I");
        using (var owner = factory.Create(request))
        {
            Check(owner.Header == new CatalogHeader(request.ProjectId, request.StoreId, 0), "Create explicit IDs and initial generation");
            await owner.AddAsync(0, [seed, Row("i"), Row("ı")]);
            var values = await owner.ReadAsync(); Check(values.Length == 3 && values.Select(v => v.Locator).ToHashSet().SetEquals(new[] { "I", "i", "ı" }), "binary collation unique locators");
        }
        var before = Hash(request.Root);
        Throws<InvalidDataException>(() => factory.Create(request));
        Check(Hash(request.Root) == before && factory.Count == 0, "Create occupied root does not reset catalog");
        using (var owner = factory.Create(request with { Create = false }))
        {
            Check(owner.Header.ProjectId == request.ProjectId && owner.Header.StoreId == request.StoreId && owner.Header.Generation == 1, "durable ProjectId/StoreId/generation");
            var found = (await owner.ReadAsync()).Single(r => r.Id == seed.Id); var mutable = found.Metadata; mutable[0] = 0;
            Check((await owner.ReadAsync()).Single(r => r.Id == seed.Id).Metadata.SequenceEqual(new byte[] { 7, 7, 7 }), "returned DTO cannot mutate catalog");
        }
        Check(factory.Count == 0, "identity owners drained");
    }

    private static void RawFixture(OpenArguments args, Action<NomCatalog>? mutate = null)
    {
        using (var factory = new CatalogFactory()) using (var owner = factory.Create(args)) owner.AddAsync(0, [Row("seed")]).GetAwaiter().GetResult();
        if (mutate == null) return;
        using var kvFactory = new NomKvStoreFactory(); using var store = kvFactory.Open(new(args.Root, args.ProjectId, args.StoreId));
        var catalog = new NomCatalog(store); catalog.Begin(); try { mutate(catalog); catalog.Commit(); } catch { catalog.Rollback(); throw; }
    }
    private static void RejectPreserving(CatalogFactory factory, OpenArguments args)
    {
        var before = Hash(args.Root); _ = Throws<InvalidDataException>(() => factory.Create(args with { Create = false }));
        Check(Hash(args.Root) == before && factory.Count == 0, "invalid catalog left exact bytes unchanged");
    }
    private static async Task FailedOpenPreservesCatalog(string scratch)
    {
        using var factory = new CatalogFactory(); var valid = Args(scratch, "wrong-identity"); RawFixture(valid);
        RejectPreserving(factory, valid with { ProjectId = Guid.NewGuid() }); RejectPreserving(factory, valid with { StoreId = Guid.NewGuid() });
        using (var owner = factory.Create(valid with { Create = false })) Check((await owner.ReadAsync()).Single().Locator == "seed", "correct identity reopens");
        foreach (var mode in new[] { "header", "control", "binding", "asset", "generation" })
        {
            var args = Args(scratch, "bad-" + mode); RawFixture(args);
            using (var rawFactory = new NomKvStoreFactory()) using (var raw = rawFactory.Open(new(args.Root, args.ProjectId, args.StoreId)))
            {
                var key = mode is "header" or "generation" ? NomCatalogKeys.Header : mode == "control" ? NomCatalogKeys.Control : raw.ReadPage(NomCatalogKeys.Prefix(mode == "binding" ? NomKeySpace.Binding : NomKeySpace.Asset)).Items.Single().Key;
                byte[]? bytes = raw.Get(key);
                if (mode == "control" || mode == "binding") bytes = null;
                else if (mode == "generation") bytes = NomCatalogRecordCodec.EncodeHeader(new(args.ProjectId, args.StoreId, 2));
                else bytes![0] ^= 1;
                raw.WriteBatch(new(raw.Sequence + 1, raw.Generation, raw.Generation, Guid.NewGuid(), [new(key, bytes)]));
            }
            RejectPreserving(factory, args);
        }
        var noCatalog = Args(scratch, "objects-without-catalog"); Directory.CreateDirectory(Path.Combine(noCatalog.Root, "objects"));
        File.WriteAllText(Path.Combine(noCatalog.Root, "objects", "sentinel"), "preserve");
        Throws<IOException>(() => factory.Create(noCatalog with { Create = false })); Throws<InvalidDataException>(() => factory.Create(noCatalog));
        Check(File.ReadAllText(Path.Combine(noCatalog.Root, "objects", "sentinel")) == "preserve", "no reset over unmanaged objects");
        var corrupt = Args(scratch, "corrupt-root"); Directory.CreateDirectory(corrupt.Root); File.WriteAllBytes(Path.Combine(corrupt.Root, "nom.root"), new byte[256]);
        RejectPreserving(factory, corrupt);
    }

    private static async Task OwnerThreadRollbackAndReopen(string scratch)
    {
        using var factory = new CatalogFactory(); var args = Args(scratch, "transactions"); var seed = Row("seed");
        var owner = factory.Create(args);
        try
        {
            await owner.AddAsync(0, [seed]);
            await ThrowsAsync<InvalidDataException>(() => owner.AddAsync(1, [Row("first"), Row("second")], failAfterFirst: true));
            Check(owner.Header.Generation == 1 && (await owner.ReadAsync()).Select(r => r.Id).SequenceEqual(new[] { seed.Id }), "managed injected failure rolls back first write and generation");
            var duplicate = ThrowsAsync<IOException>(() => owner.AddAsync(1, [Row("new-before-duplicate"), Row("seed")]));
            var error = await duplicate; Check(error.Message.Length > 0, "native unique binding index rejected duplicate");
            Check(owner.Header.Generation == 1 && (await owner.ReadAsync()).Single().Id == seed.Id, "native failure rolls back complete transaction");
            var duplicateId = await ThrowsAsync<IOException>(() => owner.AddAsync(1, [Row("before-id"), Row("different", id: seed.Id)]));
            Check(duplicateId.Message.Length > 0 && (await owner.ReadAsync()).Single().Id == seed.Id, "primary ID duplicate rollback");
            await ThrowsAsync<InvalidDataException>(() => owner.AddAsync(0, [Row("stale")]));
            Check(owner.Header.Generation == 1, "stale generation unchanged");
            var commands = Enumerable.Range(0, 12).Select(_ => Task.Run(async () => (await owner.ReadAsync()).Single().Id)).ToArray();
            Check((await Task.WhenAll(commands)).All(id => id == seed.Id), "concurrent callers see committed seed");
            Check(await owner.AddAsync(1, [Row("committed")]) == 2, "owner usable after rollback");
        }
        finally { owner.Dispose(); }
        owner.Dispose();
        var trace = owner.Trace;
        foreach (var step in new[] { "kv-open", "read-header", "begin", "insert", "commit", "rollback", "command", "kv-close" })
            Check(trace.Any(e => e.Step == step), "trace covers " + step);
        Check(trace.All(e => e.Thread == owner.OwnerThread) && owner.OwnerThread != Environment.CurrentManagedThreadId && owner.IsClosed && factory.Count == 0, "all database/transaction/close actions on single joined owner thread");
        observations.Add(new { Case = nameof(OwnerThreadRollbackAndReopen), owner.OwnerThread, Events = trace });
        using var reopened = factory.Create(args with { Create = false });
        Check(reopened.Header.Generation == 2 && (await reopened.ReadAsync()).Select(r => r.Locator).ToHashSet().SetEquals(new[] { "seed", "committed" }), "reopen exact post-rollback state");
    }

    private static async Task ExclusiveRootAndDirectProcesses(string scratch)
    {
        using var factory = new CatalogFactory(); var args = Args(scratch, "exclusive"); var seed = Row("seed");
        using (var owner = factory.Create(args))
        {
            await owner.AddAsync(0, [seed]);
            Throws<IOException>(() => factory.Create(args with { Create = false }));
            var root = await Program.ChildProbe("root", args); var direct = await Program.ChildProbe("raw", args);
            Check(root.Contains("denied:System.IO.IOException", StringComparison.Ordinal), "other-process root lock denied");
            Check(direct.Contains("denied:", StringComparison.Ordinal) && !direct.Contains("opened", StringComparison.Ordinal), "independent raw Direct open denied without checking owner.lock");
            Check((await owner.ReadAsync()).Single().Id == seed.Id && factory.Count == 1, "failed contenders preserve live owner/data");
            observations.Add(new { Case = nameof(ExclusiveRootAndDirectProcesses), Root = root, Direct = direct });
        }
        Check((await Program.ChildProbe("root", args)).Contains("opened", StringComparison.Ordinal), "child opens after owner close");
        Check(factory.Count == 0, "contention owners drained");
    }

    private static async Task SerializedBudgetBoundaries(string scratch)
    {
        using var factory = new CatalogFactory(); var args = Args(scratch, "bounds");
        using (var owner = factory.Create(args))
        {
            foreach (var n in new[] { 0, CatalogCodec.MaximumMetadata - 1, CatalogCodec.MaximumMetadata })
                await owner.AddAsync(owner.Header.Generation, [Row("metadata-" + n, n)]);
            var admitted = owner.Admitted;
            Throws<InvalidDataException>(() => Row("oversize", CatalogCodec.MaximumMetadata + 1));
            Check(owner.Admitted == admitted && owner.Header.Generation == 3, "metadata over budget before admission");
            foreach (var n in new[] { 1023, 1024 }) await owner.AddAsync(owner.Header.Generation, [Row(new string('x', n))]);
            Throws<InvalidDataException>(() => Row(new string('x',1025)));
            await owner.AddAsync(owner.Header.Generation, [Row(new string('я',512))]);
            Throws<InvalidDataException>(() => Row(new string('я',513)));
            Check((await owner.ReadAsync()).Length == 6, "metadata/UTF8 before/at cap persisted");
        }
        var sample = Row("record-a", 40); var exact = CatalogCodec.Encode(sample).Length;
        var recordArgs = Args(scratch, "record-bounds", new(DocumentBytes: exact));
        using (var owner = factory.Create(recordArgs))
        {
            await owner.AddAsync(0, [Row("record-b",39)]); await owner.AddAsync(1, [sample]);
            var admitted = owner.Admitted;
            Throws<InvalidDataException>(() => owner.AddAsync(2, [Row("record-c",41)]));
            Check(owner.Admitted == admitted && owner.Header.Generation == 2 && (await owner.ReadAsync()).Length == 2, "full serialized binary exact/over bound before transaction");
        }
        var sum = CatalogCodec.Encode(Row("batch-a",10)).Length + CatalogCodec.Encode(Row("batch-b",10)).Length;
        var batchArgs = Args(scratch, "batch-bounds", new(BatchRows: 2, BatchBytes: sum));
        using (var owner = factory.Create(batchArgs))
        {
            await owner.AddAsync(0, [Row("batch-a",10), Row("batch-b",10)]);
            Throws<InvalidDataException>(() => owner.AddAsync(1, []));
            Throws<InvalidDataException>(() => owner.AddAsync(1, [Row("more-a"), Row("more-b"), Row("more-c")]));
            Throws<InvalidDataException>(() => owner.AddAsync(1, [Row("batch-c",11), Row("batch-d",10)]));
            Check(owner.Header.Generation == 1 && (await owner.ReadAsync()).Length == 2, "batch count/aggregate exact and over preserve generation");
        }
        observations.Add(new { Case = nameof(SerializedBudgetBoundaries), ExactRecordBytes = exact, ExactBatchBytes = sum });
    }

    private static async Task QueueCancelAndStop(string scratch)
    {
        using var factory = new CatalogFactory(); var args = Args(scratch, "queue", new(QueueCount: 2));
        using var owner = factory.Create(args); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var admitted = owner.Admitted;
        Throws<OperationCanceledException>(() => owner.AddAsync(0, [Row("pre-cancel")], token: cancellation.Token));
        Check(owner.Admitted == admitted, "pre-cancel no queue entry");
        var gate = new OwnerGate(); var hold = owner.HoldAsync(gate); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var queuedCancel = new CancellationTokenSource();
        Task<long> queued; Task<AssetRecord[]> read;
        try
        {
            queued = owner.AddAsync(0, [Row("queued-cancel")], token: queuedCancel.Token); read = owner.ReadAsync();
            Throws<InvalidOperationException>(() => owner.ReadAsync());
            queuedCancel.Cancel(); Check(!queued.IsCompleted, "cancelling await does not abandon queued command owner");
        }
        finally { gate.Release.Set(); }
        await hold; await ThrowsAsync<TaskCanceledException>(() => queued); Check((await read).Length == 0 && owner.Header.Generation == 0, "worker observes cancel before any transaction");
        await owner.AddAsync(0, [Row("after-cancel")]);
        owner.Dispose(); owner.Dispose();
        Throws<ObjectDisposedException>(() => owner.ReadAsync());
        Check(owner.IsClosed && factory.Count == 0, "stop joins and releases owner once");
        var byteArgs = Args(scratch, "queue-bytes", new(QueueBytes: 1)); using var byteOwner = factory.Create(byteArgs);
        Throws<InvalidOperationException>(() => byteOwner.AddAsync(0, [Row("cannot-fit")]));
        Check(byteOwner.Admitted == 0 && byteOwner.Header.Generation == 0, "queued byte budget before native write");
        var first = Row("exact-queue-a"); var second = Row("exact-queue-b");
        var exactBytes = CatalogCodec.Encode(first).Length + CatalogCodec.Encode(second).Length;
        var exactArgs = Args(scratch,"queue-exact-bytes",new(QueueCount:3,QueueBytes:exactBytes)); using var exactOwner = factory.Create(exactArgs);
        var exactGate = new OwnerGate(); var exactHold = exactOwner.HoldAsync(exactGate); await exactGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<long> firstWrite; Task<long> secondWrite;
        try
        {
            firstWrite = exactOwner.AddAsync(0,[first]); secondWrite = exactOwner.AddAsync(1,[second]);
            Throws<InvalidOperationException>(()=>exactOwner.AddAsync(2,[Row("no-room")]));
            Check(exactOwner.Admitted==3 && exactOwner.Header.Generation==0, "queued bytes before/at cap accepted, over cap rejected");
        }
        finally { exactGate.Release.Set(); }
        await exactHold; Check(await firstWrite==1 && await secondWrite==2 && (await exactOwner.ReadAsync()).Length==2, "exact-cap queue drains in order");
    }

    private static async Task DetachedResponseBoundaries(string scratch)
    {
        using var factory = new CatalogFactory(); var args = Args(scratch,"response-count");
        using (var owner = factory.Create(args))
        {
            await owner.AddAsync(0, Enumerable.Range(0,256).Select(i => Row("r-"+i)).ToArray());
            Check((await owner.ReadAsync()).Length == 256, "query count exactly256");
            await owner.AddAsync(1,[Row("r-over")]);
            await ThrowsAsync<InvalidDataException>(() => owner.ReadAsync());
            Check(owner.Header.Generation == 2, "257-row query refuses partial response without changing store");
        }
        for (var delta = -1; delta <= 1; delta++)
        {
            var rows = Enumerable.Range(0,15).Select(i => Row("s-"+i.ToString("D2"),CatalogCodec.MaximumMetadata)).ToList();
            var remaining = 1024*1024 + delta - rows.Sum(r=>CatalogCodec.Encode(r).Length) - CatalogCodec.Encode(Row("s-15",0)).Length;
            Check(remaining > 0 && remaining <= CatalogCodec.MaximumMetadata, "response boundary fixture fits per-record budget");
            rows.Add(Row("s-15",remaining));
            Check(rows.Sum(r=>CatalogCodec.Encode(r).Length) == 1024*1024+delta, "independent serialized response byte sum");
            var bounded = Args(scratch,"response-bytes-"+delta);
            using var owner = factory.Create(bounded); await owner.AddAsync(0,rows);
            if(delta <= 0) Check((await owner.ReadAsync()).Length == 16, "query bytes before/at1MiB");
            else await ThrowsAsync<InvalidDataException>(()=>owner.ReadAsync());
        }
    }

    private static async Task FailedStartupAndFactoryCapacity(string scratch)
    {
        using var factory = new CatalogFactory(); var args = Args(scratch, "failed-start");
        var unstarted = Args(scratch,"failed-before-thread");
        Throws<InvalidDataException>(()=>factory.Create(unstarted with { FailBeforeThreadStart = true }));
        Check(factory.Count == 0 && !Directory.Exists(unstarted.Root), "failed thread start releases registry without waiting for nonexistent worker");
        Throws<InvalidDataException>(() => factory.Create(args with { FailAfterOpen = true }));
        Check(factory.Count == 0, "failed startup closes DB/root lock and joins worker");
        using (var recovered = factory.Create(args with { Create = false }))
        { Check(recovered.Header.Generation == 0 && (await recovered.ReadAsync()).Length == 0, "failed startup persisted explicit identity, reopen recovers"); }
        var values = Enumerable.Range(0,4).Select(i => factory.Create(Args(scratch,"capacity-"+i))).ToArray();
        try
        {
            var fifth = Args(scratch,"capacity-over"); Throws<InvalidOperationException>(() => factory.Create(fifth));
            Check(factory.Count == 4 && !Directory.Exists(fifth.Root), "store capacity rejects before root/thread creation");
        }
        finally { foreach (var value in values) value.Dispose(); }
        Check(factory.Count == 0, "all capacity owners closed");
        TrueMoon.IFactory<CatalogOwner> typed = factory;
        Throws<InvalidOperationException>(()=>typed.Create()); Throws<ArgumentException>(()=>typed.Create("wrong-arguments"));
        Check(factory.Count == 0, "real typed-factory argument contract rejects unsupported creation");
        factory.Dispose(); Throws<ObjectDisposedException>(() => factory.Create(Args(scratch,"after-factory-stop")));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var pre = Args(scratch,"pre-create");
        using var second = new CatalogFactory(); Throws<OperationCanceledException>(() => second.Create(pre with { Token = cancelled.Token }));
        Check(!Directory.Exists(pre.Root) && second.Count == 0, "pre-cancel Create no resources");
    }

    private static async Task FactoryStartupStopAndDrain(string scratch)
    {
        using(var factory = new CatalogFactory())
        {
            var startup = new OwnerGate(); var args = Args(scratch,"stop-during-open") with { StartupGate = startup };
            var opening = Task.Run(()=>factory.Create(args));
            await startup.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = Task.Run(factory.Dispose);
            try { await startup.StopRequested.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { startup.Release.Set(); }
            await ThrowsAsync<ObjectDisposedException>(()=>opening); await stopping;
            Check(factory.Count==0 && !Directory.Exists(args.Root), "startup/stop race cannot launch an untracked late owner");
        }
        using var live = new CatalogFactory(); var firstArgs=Args(scratch,"factory-drain-a"); var secondArgs=Args(scratch,"factory-drain-b");
        var first = live.Create(firstArgs); var second = live.Create(secondArgs); var gate = new OwnerGate();
        try
        {
            var holding=first.HoldAsync(gate); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var write=first.AddAsync(0,[Row("drained-write")]); await second.AddAsync(0,[Row("second-live")]);
            var draining=Task.Run(live.Dispose); gate.Release.Set();
            await holding; Check(await write==1, "factory stop drains already admitted write"); await draining;
            Check(first.IsClosed && second.IsClosed && live.Count==0, "factory directly drains every live owner");
            Check((await Program.ChildProbe("raw",firstArgs)).Contains("opened",StringComparison.Ordinal) && (await Program.ChildProbe("raw",secondArgs)).Contains("opened",StringComparison.Ordinal), "factory drain releases both native Direct handles");
        }
        finally { gate.Release.Set(); first.Dispose(); second.Dispose(); }
    }
}
