#if NETTERLOW
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModelImportResearch;

internal static class CapturedProof
{
    private static readonly List<string> passed = [];
    private static void Check(bool condition, string message) => Projection.Require(condition, message);
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
    private static CapturedInput Bytes(byte[] bytes, ImportLimits? limits = null) => new("a.glb", [new("a.glb", bytes)], limits ?? new());
    private static string Import(CapturedInput input, ImportLimits? limits = null)
    {
        using var io = new CapturedIo(input, limits ?? new());
        var model = Candidate.Import(input.Root, io.Pointer, limits);
        io.ThrowIfFailed(); Check(io.LiveStreams == 0 && Candidate.LiveImports == 0, "import drained");
        return Projection.Fingerprint(model);
    }
    internal static async Task<int> Run(string fixtures, string output)
    {
        var scratch = Path.Combine(Path.GetDirectoryName(output)!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var completed = false; string? failure = null; object? native = null;
        try
        {
            Candidate.Activate(); native = Program.NativeInfo();
            CapturePathsAndPacket(); passed.Add(nameof(CapturePathsAndPacket));
            CaptureBudgetBoundaries(); passed.Add(nameof(CaptureBudgetBoundaries));
            CallbackReadSeekAndQuotas(); passed.Add(nameof(CallbackReadSeekAndQuotas));
            CapturedOnlyAndMutation(fixtures, scratch); passed.Add(nameof(CapturedOnlyAndMutation));
            NativeProjectionBudgets(fixtures); passed.Add(nameof(NativeProjectionBudgets));
            AssetIdentityReimport(); passed.Add(nameof(AssetIdentityReimport));
            await WorkerOutputBoundaries(); passed.Add(nameof(WorkerOutputBoundaries));
            await WorkerCancellationAndPublication(fixtures, scratch); passed.Add(nameof(WorkerCancellationAndPublication));
            completed = true;
        }
        catch (Exception ex) { failure = ex.ToString(); Console.Error.WriteLine(ex); }
        finally
        {
            Candidate.Shutdown();
            File.WriteAllText(output, JsonSerializer.Serialize(new { Completed = completed, Candidate = Candidate.Name, Native = native,
                LiveImports = Candidate.LiveImports, Passed = passed, WorkerStarts = ImportWorker.Starts, WorkersReaped = ImportWorker.Reaped,
                Failure = failure }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return completed ? 0 : 1;
    }

    private static void CapturePathsAndPacket()
    {
        foreach (var invalidPath in new[] { "", "../a", "a/../b", "/a", "\\\\host\\a", "C:\\a", "https://x/a", "a%20b", "a?x", "a//b", "a/", "a. ", "a\0b" })
            Throws<InvalidDataException>(() => CapturedInput.Normalize(invalidPath));
        Check(CapturedInput.Normalize("./folder\\a.glb") == "folder/a.glb", "relative normalization");
        Throws<InvalidDataException>(() => new CapturedInput("a", [new("a", []), new("A", [])], new()));
        Throws<InvalidDataException>(() => new CapturedInput("missing", [new("a", [])], new()));
        Throws<InvalidDataException>(() => new CapturedInput("a", [], new()));
        var original = new byte[] { 1, 2, 3 }; var input = Bytes(original); original[0] = 9;
        Check(input.Get("a.glb").Span.SequenceEqual(new byte[] { 1, 2, 3 }), "capture copies caller memory");
        var reversed = new CapturedInput("a", [new("z", [3]), new("a", [1, 2])], new());
        var sorted = new CapturedInput("a", [new("a", [1, 2]), new("z", [3])], new());
        Check(reversed.Revision == sorted.Revision && reversed.Revision != new CapturedInput("z", [new("a", [1, 2]), new("z", [3])], new()).Revision, "closure order invariant, root participates");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".capture");
        try
        {
            input.Write(path); var roundtrip = CapturedInput.Read(path, new());
            Check(roundtrip.Revision == input.Revision && roundtrip.Get("a.glb").Span.SequenceEqual(input.Get("a.glb").Span), "bounded packet roundtrip");
            File.AppendAllText(path, "x"); Throws<InvalidDataException>(() => CapturedInput.Read(path, new()));
            input.Write(path); using (var stream = File.OpenWrite(path)) stream.SetLength(stream.Length - 1);
            Throws<EndOfStreamException>(() => CapturedInput.Read(path, new()));
            using (var writer = new BinaryWriter(File.Create(path))) { writer.Write(1); writer.Write7BitEncodedInt(257); }
            Throws<InvalidDataException>(() => CapturedInput.Read(path, new()));
        }
        finally { File.Delete(path); }
    }

    private static void CaptureBudgetBoundaries()
    {
        var limits = new ImportLimits(Files: 2, FileBytes: 3, TotalBytes: 5);
        foreach (var n in new[] { 0, 2, 3 }) Check(Bytes(new byte[n], limits).TotalBytes == n, "file before/at quota and empty file");
        Throws<InvalidDataException>(() => Bytes(new byte[4], limits));
        Check(new CapturedInput("a", [new("a", [1, 2, 3]), new("b", [4, 5])], limits).TotalBytes == 5, "total/file count at quota");
        Throws<InvalidDataException>(() => new CapturedInput("a", [new("a", [1, 2, 3]), new("b", [4, 5, 6])], limits));
        Throws<InvalidDataException>(() => new CapturedInput("a", [new("a", []), new("b", []), new("c", [])], limits));
        foreach (var bad in new[] { new ImportLimits(Files: 0), new ImportLimits(FileBytes: 0), new ImportLimits(ReadBytes: -1), new ImportLimits(Depth: 0), new ImportLimits(OutputBytes: 8388609) })
            Throws<InvalidDataException>(bad.Validate);
        Throws<OperationCanceledException>(() => CapturedInput.FromDirectory("missing", "a", [], new(), new(true)));
    }

    private static unsafe void CallbackReadSeekAndQuotas()
    {
        using var io = new CapturedIo(Bytes([1, 2, 3, 4, 5]), new());
        Check(sizeof(CapturedIo.FileIo) == 24 && sizeof(CapturedIo.NativeFile) == 56, "x64 aiFileIO/aiFile ABI");
        var native = (CapturedIo.FileIo*)io.Pointer;
        var path = "a.glb\0"u8.ToArray(); var mode = "rb\0"u8.ToArray();
        fixed (byte* p = path) fixed (byte* m = mode)
        {
            var file = native->Open(native, p, m); Check(file != null, "callback open");
            byte* buffer = stackalloc byte[8];
            Check(file->Size(file) == 5 && file->Read(file, buffer, 0, nuint.MaxValue) == 0 && file->Tell(file) == 0, "zero item no-op");
            Check(file->Read(file, buffer, 2, 3) == 2 && file->Tell(file) == 5 && new ReadOnlySpan<byte>(buffer, 5).SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }), "fread whole items and partial EOF");
            Check(file->Read(file, buffer, 1, 1) == 0, "EOF");
            Check(file->Seek(file, unchecked((nuint)(-2L)), 2) == 0 && file->Tell(file) == 3, "end relative signed offset");
            Check(file->Seek(file, 1, 1) == 0 && file->Tell(file) == 4 && file->Seek(file, 0, 0) == 0, "current and start seek");
            Check(file->Seek(file, 6, 0) == -1 && file->Seek(file, 0, 99) == -1 && file->Tell(file) == 0, "invalid seek preserves cursor");
            Check(file->Read(file, buffer, nuint.MaxValue, 2) == 0 && io.Error is OverflowException, "size_t overflow fails before copy/allocation");
            native->Close(native, file); Check(io.LiveStreams == 0, "closed cursor");
        }
        using var quota = new CapturedIo(Bytes([1, 2, 3, 4]), new(ReadBytes: 3, Opens: 2, Streams: 1));
        native = (CapturedIo.FileIo*)quota.Pointer;
        fixed (byte* p = path) fixed (byte* m = mode)
        {
            var file = native->Open(native, p, m); byte* buffer = stackalloc byte[4];
            Check(file->Read(file, buffer, 1, 2) == 2 && file->Read(file, buffer, 1, 1) == 1 && quota.ReadBytes == 3, "read before and at quota");
            Check(file->Read(file, buffer, 1, 1) == 0 && quota.ReadBytes == 3 && file->Tell(file) == 3, "read over quota preserves state");
            Check(native->Open(native, p, m) == null && quota.LiveStreams == 1, "concurrent stream quota");
            native->Close(native, file);
            Check(native->Open(native, p, m) == null && quota.OpenCalls == 3, "cumulative open quota");
        }
        using var write = new CapturedIo(Bytes([1]), new()); native = (CapturedIo.FileIo*)write.Pointer;
        var writeMode = "wb\0"u8.ToArray();
        fixed (byte* p = path) fixed (byte* m = writeMode)
            Check(native->Open(native, p, m) == null && write.Error is InvalidDataException && write.LiveStreams == 0, "deny write modes");
        var disposal = new CapturedIo(Bytes([1]), new()); native = (CapturedIo.FileIo*)disposal.Pointer;
        fixed (byte* p = path) fixed (byte* m = mode)
        {
            var file = native->Open(native, p, m); GC.Collect(); GC.WaitForPendingFinalizers();
            Check(file->Size(file) == 1 && file->Write(file, null, 1, 1) == 0 && disposal.Error is InvalidDataException, "rooted callback state and read-only file");
        }
        disposal.Dispose(); disposal.Dispose(); Check(disposal.LiveStreams == 0 && disposal.Pointer == 0, "owner closes remaining native cursor exactly once");
    }

    private static void CapturedOnlyAndMutation(string fixtures, string scratch)
    {
        foreach (var (root, dependencies) in new[] { ("scene.gltf", new[] { "scene.bin" }), ("scene.obj", new[] { "scene.mtl" }), ("scene.glb", Array.Empty<string>()), ("grid.glb", Array.Empty<string>()) })
        {
            var folder = Path.Combine(scratch, Path.GetFileNameWithoutExtension(root) + Path.GetExtension(root)); Directory.CreateDirectory(folder);
            foreach (var name in new[] { root }.Concat(dependencies)) File.Copy(Path.Combine(fixtures, name), Path.Combine(folder, name));
            var capture = CapturedInput.FromDirectory(folder, root, dependencies, new());
            string before;
            using (var io = new CapturedIo(capture, new()))
            {
                var model = Candidate.Import(capture.Root, io.Pointer); io.ThrowIfFailed(); Projection.Verify(model, root);
                before = Projection.Fingerprint(model);
                Check(io.Used.SetEquals(new[] { root }.Concat(dependencies)) && io.LiveStreams == 0, "native consumes exact captured dependency closure");
            }
            foreach (var name in new[] { root }.Concat(dependencies)) { File.WriteAllText(Path.Combine(folder, name), "changed"); File.Delete(Path.Combine(folder, name)); }
            Check(Import(capture) == before, "native import unchanged after source/dependency mutation and deletion");
        }
        foreach (var (root, dependency) in new[] { ("scene.gltf", "scene.bin"), ("scene.obj", "scene.mtl") })
        {
            var capture = CapturedInput.FromDirectory(fixtures, root, [], new());
            using var io = new CapturedIo(capture, new());
            try { Candidate.Import(capture.Root, io.Pointer); } catch (InvalidDataException) { }
            Check(io.Error != null && File.Exists(Path.Combine(fixtures, dependency)), "missing capture fails while dependency exists on disk");
            Throws<InvalidDataException>(io.ThrowIfFailed);
            Check(Candidate.LiveImports == 0 && io.LiveStreams == 0, "failed native import drained");
        }
    }

    private static void NativeProjectionBudgets(string fixtures)
    {
        var input = CapturedInput.FromDirectory(fixtures, "scene.glb", [], new());
        var exact = new ImportLimits(Meshes: 2, Vertices: 3, Faces: 1, Nodes: 3, Depth: 1, Instances: 4, OutputBytes: 368);
        Check(Import(input, exact) == Import(input), "exact native projection quotas");
        foreach (var over in new[] { exact with { Meshes = 1 }, exact with { Vertices = 2 }, exact with { Nodes = 2 }, exact with { Instances = 3 }, exact with { OutputBytes = 367 } })
        {
            Throws<InvalidDataException>(() => Import(input, over));
            Check(Candidate.LiveImports == 0, "quota rejection releases aiScene");
        }
        using var reads = new CapturedIo(input, new(ReadBytes: 1));
        try { Candidate.Import(input.Root, reads.Pointer); } catch (InvalidDataException) { }
        Check(reads.Error is InvalidDataException && Candidate.LiveImports == 0 && reads.LiveStreams == 0, "native read-budget failure contained");
        var grid = CapturedInput.FromDirectory(fixtures, "grid.glb", [], new());
        Check(Import(grid, new(Faces: 32258)) == Import(grid), "faces at quota");
        Throws<InvalidDataException>(() => Import(grid, new(Faces: 32257)));
        var deeper = JsonNode.Parse(File.ReadAllText(Path.Combine(fixtures, "scene.gltf")))!;
        deeper["nodes"]!.AsArray().Add(new JsonObject { ["name"] = "wrapper", ["children"] = new JsonArray(0) });
        deeper["scenes"]![0]!["nodes"] = new JsonArray(3);
        var deepInput = new CapturedInput("scene.gltf", [new("scene.gltf", Encoding.UTF8.GetBytes(deeper.ToJsonString())), new("scene.bin", File.ReadAllBytes(Path.Combine(fixtures, "scene.bin")))], new());
        Check(Import(deepInput, new(Depth: 2)) == Import(input), "identity wrapper preserves geometry/transforms at depth quota");
        Throws<InvalidDataException>(() => Import(deepInput, new(Depth: 1)));
        Check(Candidate.LiveImports == 0, "faces/depth rejection releases aiScene");
    }

    private static void AssetIdentityReimport()
    {
        var mesh = new AssetLocator("mesh", "Body"); var material = new AssetLocator("material", "Red");
        var first = new AssetLedger(Guid.NewGuid(), []).Reconcile([mesh, material]);
        var saved = first.Save(); var restored = AssetLedger.Load(saved);
        var reordered = restored.Reconcile([material, mesh]);
        Check(reordered.Save() == saved && restored.SourceId == first.SourceId, "persistent IDs independent of discovery order/content revision");
        Check(Bytes([1]).Revision != Bytes([2]).Revision && first.Reconcile([mesh, material]).Save() == saved, "revision changes without identity change");
        var renamed = new AssetLocator("mesh", "Hull");
        var rename = first.Reconcile([renamed, material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = renamed });
        Check(rename.Entries.Single(e => e.Locator == "Hull").Id == first.Entries.Single(e => e.Locator == "Body").Id, "explicit rename preserves ID");
        var removed = first.Reconcile([material]); Check(!removed.Entries.Single(e => e.Locator == "Body").Active, "delete keeps tombstone");
        Throws<InvalidDataException>(() => removed.Reconcile([mesh, material]));
        Check(removed.Reconcile([mesh, material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = mesh }).Save() == saved, "explicit restore preserves ID");
        Throws<InvalidDataException>(() => first.Reconcile([mesh, mesh]));
        Throws<InvalidDataException>(() => first.Reconcile([material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = material }));
        Throws<InvalidDataException>(() => first.Reconcile([renamed], new Dictionary<AssetLocator, AssetLocator> { [renamed] = renamed }));
        Throws<InvalidDataException>(() => new AssetLedger(first.SourceId, [first.Entries[0], first.Entries[0]]));
        var second = new AssetLocator("mesh", "Roof"); var pair = first.Reconcile([mesh, second, material]);
        Throws<InvalidDataException>(() => pair.Reconcile([second, material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = second }));
        Throws<InvalidDataException>(() => pair.Reconcile([renamed, material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = renamed, [second] = renamed }));
        Throws<InvalidDataException>(() => pair.Reconcile([mesh, renamed, material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = renamed }));
        Check(removed.Reconcile([renamed, material], new Dictionary<AssetLocator, AssetLocator> { [mesh] = renamed }).Entries.Single(e => e.Locator == "Hull").Id == first.Entries.Single(e => e.Locator == "Body").Id, "explicit rename restores tombstoned identity");
        Throws<InvalidDataException>(() => new AssetLedger(first.SourceId, [first.Entries[0], first.Entries[0] with { Locator = "another" }]));
        Throws<InvalidDataException>(() => new AssetLedger(Guid.Empty, []));
        Check(pair.Entries.Single(e => e.Locator == "Roof").Id != first.Entries.Single(e => e.Locator == "Body").Id, "new asset gets distinct ID");
        var unicode = new AssetLedger(first.SourceId, Enumerable.Range(0, 2048).Select(i => new AssetEntry(new string('\u4E2D', 32), new string('\u6587', 240) + i, Guid.NewGuid(), false)));
        var escaped = unicode.Save(); Check(escaped.Length > 2 * 1024 * 1024 && AssetLedger.Load(escaped).Save() == escaped, "escaped Unicode ledger fits bounded persistence roundtrip");
        Check(first.Save() == saved, "failed reconciliation does not mutate published ledger");
    }

    private static async Task WorkerOutputBoundaries()
    {
        Check(await ImportWorker.ReadLineLimited(new StringReader(""), default) == null, "empty stdout EOF");
        Check(await ImportWorker.ReadLineLimited(new StringReader("abc\r\n"), default) == "abc", "stdout CRLF");
        Check((await ImportWorker.ReadLineLimited(new StringReader(new string('a', 16384)), default))!.Length == 16384, "stdout exact cap before allocation growth");
        await ThrowsAsync<InvalidDataException>(() => ImportWorker.ReadLineLimited(new StringReader(new string('a', 16385)), default));
        foreach (var length in new[] { 8191, 8192, 8193 })
        {
            using var reader = new StringReader(new string('e', length)); var result = await ImportWorker.ReadErrors(reader);
            Check(result.Text == new string('e', Math.Min(length, 8192)) && result.Overflow == (length > 8192) && reader.Peek() == -1, "bounded stderr before/at/over cap and tail drained");
        }
    }

    private static async Task WorkerCancellationAndPublication(string fixtures, string scratch)
    {
        var capture = CapturedInput.FromDirectory(fixtures, "scene.glb", [], new());
        var ledger = new AssetLedger(Guid.NewGuid(), []).Reconcile([new("mesh", "Body")]);
        var initial = new PublishedImport(7, "old", "old", ledger); var publication = new ImportPublication(initial);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var starts = ImportWorker.Starts;
        await ThrowsAsync<OperationCanceledException>(() => ImportWorker.Run(capture, scratch, "normal", cancelled.Token, TimeSpan.FromSeconds(10)));
        Check(ImportWorker.Starts == starts && ReferenceEquals(publication.Current, initial), "pre-cancel no worker/publication");
        await ThrowsAsync<ArgumentOutOfRangeException>(() => ImportWorker.Run(capture, scratch, "normal", default, Timeout.InfiniteTimeSpan));
        Check(ImportWorker.Starts == starts, "unbounded deadline denied before worker admission");
        await ThrowsAsync<System.ComponentModel.Win32Exception>(() => ImportWorker.Run(capture, scratch, "normal", default, TimeSpan.FromSeconds(10), executable: Path.Combine(scratch, "missing-worker.exe")));
        Check(ImportWorker.Starts == starts && ImportWorker.Reaped == starts, "failed process creation releases admission without ghost owner");
        var memory = await ImportWorker.Run(capture, scratch, "memory", default, TimeSpan.FromSeconds(10));
        Check(memory.Fingerprint == "memory-denied", "actual committed-memory denial after Job admission");
        for (var repeat = 0; repeat < 3; repeat++)
        {
            using var cancellation = new CancellationTokenSource(); var entered = false; Task<WorkerResult>? competing = null; var admitted = ImportWorker.Starts;
            await ThrowsAsync<OperationCanceledException>(() => ImportWorker.Run(capture, scratch, "block", cancellation.Token, TimeSpan.FromSeconds(10), message =>
            { Check(message == "read", "native callback handshake"); entered = true; competing = ImportWorker.Run(capture, scratch, "normal", default, TimeSpan.FromSeconds(10)); cancellation.Cancel(); }));
            Check(competing != null && ImportWorker.Starts == admitted + 1, "second worker rejected before launch");
            await ThrowsAsync<InvalidOperationException>(() => competing!);
            Check(entered && ImportWorker.Reaped == ImportWorker.Starts && ReferenceEquals(publication.Current, initial), "in-native cancel reaps before return and preserves previous result");
        }
        var deadlineEntered = false;
        await ThrowsAsync<TimeoutException>(() => ImportWorker.Run(capture, scratch, "block", default, TimeSpan.FromSeconds(2), message => { Check(message == "read", "deadline read handshake"); deadlineEntered = true; }));
        Check(deadlineEntered && ImportWorker.Reaped == ImportWorker.Starts && ReferenceEquals(publication.Current, initial), "deadline kills and reaps without publication");
        var broken = CapturedInput.FromDirectory(fixtures, "bad.gltf", [], new());
        await ThrowsAsync<InvalidDataException>(() => ImportWorker.Run(broken, scratch, "normal", default, TimeSpan.FromSeconds(10)));
        Check(ImportWorker.Reaped == ImportWorker.Starts && ReferenceEquals(publication.Current, initial), "failed import no partial result");
        var success = await ImportWorker.Run(capture, scratch, "normal", default, TimeSpan.FromSeconds(10));
        Check(success.Fingerprint == Import(capture) && success.Used.SequenceEqual(new[] { "scene.glb" }) && ImportWorker.Reaped == ImportWorker.Starts, "recovery import matches in-process detached data and drains");
        foreach (var (root, dependency) in new[] { ("scene.gltf", "scene.bin"), ("scene.obj", "scene.mtl") })
        {
            var external = CapturedInput.FromDirectory(fixtures, root, [dependency], new());
            var imported = await ImportWorker.Run(external, scratch, "normal", default, TimeSpan.FromSeconds(10));
            Check(imported.Fingerprint == Import(external) && imported.Used.ToHashSet(StringComparer.Ordinal).SetEquals(new[] { root, dependency }), "worker consumes complete multi-file packet");
        }
        Check(ImportWorker.Starts == ImportWorker.Reaped && Directory.GetFiles(scratch, "*.capture").Length == 0, "all workers reaped and packets released");
        Throws<OperationCanceledException>(() => publication.Commit(7, success.Revision, success.Fingerprint, ledger, cancelled.Token));
        Check(ReferenceEquals(publication.Current, initial), "post-parse pre-publication cancel");
        publication.Commit(7, success.Revision, success.Fingerprint, ledger, default);
        Check(publication.Current.Generation == 8 && publication.Current.InputRevision == capture.Revision && publication.Current.Ledger.Save() == ledger.Save(), "atomic successful commit");
        Throws<InvalidOperationException>(() => publication.Commit(7, "stale", "stale", ledger, default));
        Throws<InvalidOperationException>(() => publication.Commit(8, success.Revision, success.Fingerprint, new AssetLedger(Guid.NewGuid(), []), default));
        Check(publication.Current.Generation == 8 && publication.Current.Fingerprint == success.Fingerprint, "stale completion cannot overwrite");
    }
}
#endif
