using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Probe;

// Instrumentation is confined to this existing friend probe and its per-factory IO seam.
// No exception injection: hooks perform actual IO and then hold for the external cut.
internal static class NomVmCrashProof
{
    internal static readonly string[] Phases = ["commit-ack", "cas-finalized", "journal-partial", "checkpoint-ready", "root-switched", "backup-published", "restore-published"];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal sealed record Configuration(int Schema, Guid TrialId, string Phase, string Root, string Signal, string BootId,
        Guid ProjectId, Guid StoreId, Guid OldId, Guid NewId, Guid SourceId, Guid KindId, int HoldSeconds = 120);
    internal sealed record Witness(int Schema, Configuration Configuration, int ProcessId, string ProcessStartUtc,
        string ModuleSha256, AssetWriteTicket OldTicket, AssetResourceVersion OldVersion,
        AssetWriteTicket? NewTicket, AssetResourceVersion CandidateVersion, long AcknowledgedGeneration);

    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    private static T Read<T>(string path)
    {
        Require(new FileInfo(path).Length is > 0 and <= 65536, "VM protocol document budget");
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException("Null VM document");
    }
    private static void Save(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, Json); stream.Flush(true);
    }
    private static void Validate(Configuration c)
    {
        Require(c.Schema == 1 && Phases.Contains(c.Phase) && c.TrialId != Guid.Empty && c.HoldSeconds is >= 30 and <= 180, "VM configuration discriminator/budget");
        Require(new[] { c.ProjectId, c.StoreId, c.OldId, c.NewId, c.SourceId, c.KindId }.All(id => id != Guid.Empty) && c.OldId != c.NewId, "VM explicit identities");
        Require(Path.IsPathFullyQualified(c.Root) && Path.IsPathFullyQualified(c.Signal) && !c.Root.StartsWith(@"\\", StringComparison.Ordinal) && !c.Signal.StartsWith(@"\\", StringComparison.Ordinal), "VM local absolute paths");
        Require(!c.Signal.StartsWith(Path.TrimEndingDirectorySeparator(c.Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && c.Signal != c.Root && !string.IsNullOrWhiteSpace(c.BootId), "Signal outside catalog and explicit boot");
    }
    private static byte[] Pattern(bool candidate) => Enumerable.Range(0, candidate ? 65553 : 32785).Select(i => (byte)(i * 73 + (candidate ? 43 : 19))).ToArray();
    private static AssetRecord Row(Configuration c, bool candidate) => new(candidate ? c.NewId : c.OldId, c.SourceId, c.KindId, candidate ? "vm/new" : "vm/old", candidate ? [7, 8, 9] : [4, 5, 6]);
    private static AssetResourceVersion Expected(Configuration c, bool candidate)
    {
        var bytes = Pattern(candidate); var row = Row(c, candidate); var digest = ResourceCodec.Hash(bytes);
        return new(row.Id, row.Kind, ResourceCodec.Revision(CatalogCodec.Encode(row), digest, bytes.Length), digest, bytes.Length);
    }
    private static string ModuleHash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(IProjectAssetStore).Assembly.Location)));

    internal static async Task<int> Child(string configuration)
    {
        try
        {
            var c = Read<Configuration>(configuration); Validate(c);
            Require(!Directory.Exists(c.Root) && !File.Exists(c.Root) && !File.Exists(c.Signal), "New VM trial paths required");
            AssetWriteTicket? oldTicket = null, newTicket = null; AssetResourceVersion? old = null;
            void Hold(string phase)
            {
                Require(phase == c.Phase && oldTicket != null && old != null, "Exact initialized VM cut");
                var process = Process.GetCurrentProcess();
                var witness = new Witness(1, c, process.Id, process.StartTime.ToUniversalTime().ToString("O"), ModuleHash(), oldTicket!, old!, newTicket, Expected(c, true), c.Phase == "commit-ack" ? 2 : 1);
                var pending = c.Signal + ".pending"; Save(pending, witness); File.Move(pending, c.Signal, false);
                Console.WriteLine("ready:" + c.TrialId.ToString("D")); Console.Out.Flush();
                using var never = new ManualResetEvent(false);
                never.WaitOne(TimeSpan.FromSeconds(c.HoldSeconds));
                throw new TimeoutException("External VM cut did not arrive within hold budget.");
            }
            var files = new CutFiles(c.Phase, Hold);
            using var factory = new CatalogFactory(fileOperations: files);
            var arguments = new OpenArguments(c.Root, c.ProjectId, c.StoreId, true);
            var owner = factory.Create(arguments);
            oldTicket = await owner.ReserveResourcesAsync(0, [new(Row(c, false), 32785, MaximumBytes: 32785)]);
            using (var input = new MemoryStream(Pattern(false), false)) old = (await owner.PublishResourcesAsync(oldTicket, new Dictionary<Guid, Stream> { [c.OldId] = input })).Resources.Single();
            Require(old == Expected(c, false), "Seed matches independently constructed expected bytes/version");
            if (c.Phase is "commit-ack" or "cas-finalized" or "journal-partial")
            {
                newTicket = await owner.ReserveResourcesAsync(1, [new(Row(c, true), 65553, MaximumBytes: 65553)]); files.Armed = true;
                using var input = new MemoryStream(Pattern(true), false);
                await owner.PublishResourcesAsync(newTicket, new Dictionary<Guid, Stream> { [c.NewId] = input });
                if (c.Phase == "commit-ack") Hold(c.Phase);
            }
            else if (c.Phase is "checkpoint-ready" or "root-switched") { files.Armed = true; await owner.CompactAsync(); }
            else if (c.Phase == "backup-published") { files.Armed = true; await owner.ExportBackupAsync(c.Root + "-backup"); }
            else
            {
                await owner.ExportBackupAsync(c.Root + "-backup"); owner.Dispose(); files.Armed = true;
                using var restored = factory.Create(arguments with { Root = c.Root + "-restored", Create = false, Restore = new(c.Root + "-backup", c.Root + "-restored", c.ProjectId, c.StoreId) });
            }
            throw new InvalidOperationException("VM cutpoint was not reached");
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    internal static async Task<int> Verify(string configuration, string hostWitness, string report, bool logFailure = true)
    {
        var checks = new List<string>(); string? failure = null;
        void Check(bool value, string name) { Require(value, name); checks.Add(name); }
        try
        {
            var c = Read<Configuration>(configuration); Validate(c); var w = Read<Witness>(hostWitness);
            Check(w.Schema == 1 && w.Configuration == c && w.ProcessId > 0 && w.ModuleSha256 == ModuleHash(), "ExactExternalWitnessAndModule");
            Check(w.OldVersion == Expected(c, false) && w.CandidateVersion == Expected(c, true) && w.OldTicket.StoreId == c.StoreId &&
                w.AcknowledgedGeneration == (c.Phase == "commit-ack" ? 2 : 1), "IndependentGoldenBytesAndAcknowledgedGeneration");
            using var factory = new CatalogFactory(); using var owner = factory.Create(new(c.Root, c.ProjectId, c.StoreId, false));
            await State(owner, c.Phase == "commit-ack");
            if (w.NewTicket != null)
            {
                var outcome = await owner.LookupResourceOperationAsync(w.NewTicket);
                Check(outcome.Status == (c.Phase == "commit-ack" ? "committed" : "aborted"), "ExactCutTicketOutcomeAfterRecovery");
            }
            if (c.Phase is "checkpoint-ready" or "root-switched")
            {
                var maintenance = await owner.CompactAsync(); Check(maintenance.Generation == 1 && !maintenance.CleanupPending, "CheckpointRecoveryAndRetry");
            }
            if (c.Phase == "backup-published")
            {
                // The directory move is unacknowledged. It may be absent or a complete verified backup.
                Check(!File.Exists(c.Root + "-backup"), "BackupPublicationCannotBecomeRegularFile");
                if (Directory.Exists(c.Root + "-backup"))
                { using var restored = factory.Create(new(c.Root + "-verify-backup", c.ProjectId, c.StoreId, false, Restore: new(c.Root + "-backup", c.Root + "-verify-backup", c.ProjectId, c.StoreId))); await State(restored, false); }
            }
            if (c.Phase == "restore-published")
            {
                Check(!File.Exists(c.Root + "-restored"), "RestorePublicationCannotBecomeRegularFile");
                if (Directory.Exists(c.Root + "-restored")) { using var restored = factory.Create(new(c.Root + "-restored", c.ProjectId, c.StoreId, false)); await State(restored, false); }
            }
            var retryId = Guid.NewGuid(); var row = new AssetRecord(retryId, c.SourceId, c.KindId, "vm/retry", [1]);
            var next = await owner.ReserveResourcesAsync(owner.Header.Generation, [new(row, 1, MaximumBytes: 1)]);
            using (var input = new MemoryStream([99], false)) Check((await owner.PublishResourcesAsync(next, new Dictionary<Guid, Stream> { [retryId] = input })).Generation == w.AcknowledgedGeneration + 1, "AppendLivenessAfterGuestRecovery");

            async Task State(CatalogOwner store, bool newCommitted)
            {
                Check(store.Header.ProjectId == c.ProjectId && store.Header.StoreId == c.StoreId && store.Header.Generation == (newCommitted ? 2 : 1), "PersistentIdentityAndWholeGeneration");
                Check(await store.FindResourceAsync(c.OldId) == w.OldVersion && await store.FindResourceAsync(c.NewId) == (newCommitted ? w.CandidateVersion : null), "WholeOldOrAcknowledgedNewHeads");
                var acknowledged = await store.LookupResourceOperationAsync(w.OldTicket);
                Check(acknowledged.Status == "committed" && acknowledged.Generation == 1 && acknowledged.Resources.SequenceEqual([w.OldVersion]), "OldAcknowledgedOutcomeNeverLost");
                await Exact(store, w.OldVersion, Pattern(false));
                if (newCommitted) await Exact(store, w.CandidateVersion, Pattern(true));
            }
            async Task Exact(CatalogOwner store, AssetResourceVersion exact, byte[] bytes)
            {
                await using var source = await store.OpenResourceAsync(exact); var buffer = new byte[65536]; var position = 0;
                while (true) { var count = await source.ReadAsync(buffer); if (count == 0) break; Check(position <= bytes.Length - count && buffer.AsSpan(0, count).SequenceEqual(bytes.AsSpan(position, count)), "ExactResourceBytes"); position += count; }
                Check(position == bytes.Length && source.Length == bytes.Length, "ExactResourceLengthAndEof");
            }
        }
        catch (Exception error) { failure = error.ToString(); if (logFailure) Console.Error.WriteLine(error); }
        Save(report, new { Completed = failure == null, Checks = checks, Failure = failure, VmCrashTested = false, HardwarePowerLossTested = false,
            Scope = "Recovery assertions only; the external controller separately attests actual VM TurnOff and distinct boot" });
        return failure == null ? 0 : 1;
    }

    internal static async Task<int> ProtocolProof(string report)
    {
        var passed = new List<string>(); string? failure = null; var children = 0; var reaped = 0;
        var scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "protocol-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
        try
        {
            foreach (var phase in Phases)
            {
                var folder = Path.Combine(scratch, phase); Directory.CreateDirectory(folder);
                var c = new Configuration(1, Guid.NewGuid(), phase, Path.Combine(folder, "store"), Path.Combine(folder, "ready.json"), "host-process-protocol-only", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
                var configuration = Path.Combine(folder, "case.json"); Save(configuration, c);
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { typeof(Program).Assembly.Location, "vm-child", configuration }) start.ArgumentList.Add(argument);
                using var process = new Process { StartInfo = start }; var started = false; var observedExit = false;
                try
                {
                    Require(process.Start(), "Protocol child starts"); started = true; children++; var errors = process.StandardError.ReadToEndAsync();
                    Require(await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)) == "ready:" + c.TrialId.ToString("D") && !process.HasExited, "Exact live protocol cut");
                    // Persist a separate controller-side copy before killing; never use a guest-local signal after recovery.
                    var witness = Path.Combine(folder, "host-witness.json"); Save(witness, Read<Witness>(c.Signal));
                    process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); reaped++; observedExit = true;
                    Require((await errors).Length == 0, "No child failure before controlled kill");
                    if (phase == "commit-ack")
                    {
                        var wrong = Path.Combine(folder, "wrong-module-witness.json"); Save(wrong, Read<Witness>(witness) with { ModuleSha256 = new string('0', 64) });
                        var before = Snapshot(c.Root);
                        Require(await Verify(configuration, wrong, Path.Combine(folder, "rejected-witness.json"), false) == 1 && Snapshot(c.Root) == before, "Foreign witness rejected before recovery mutation");
                        passed.Add("ForeignWitnessRejectedWithoutMutation");
                    }
                    if (phase == "backup-published")
                    {
                        var backup = c.Root + "-backup"; var preserved = backup + "-evidence";
                        Directory.Move(backup, preserved); File.WriteAllBytes(backup, [1]);
                        try { Require(await Verify(configuration, witness, Path.Combine(folder, "invalid-publication.json"), false) == 1, "Regular file cannot masquerade as absent/valid backup"); }
                        finally { File.Delete(backup); Directory.Move(preserved, backup); }
                        passed.Add("RegularBackupPublicationRejected");
                    }
                    Require(await Verify(configuration, witness, Path.Combine(folder, "recovery.json")) == 0, "Recovery protocol passes: " + phase);
                    passed.Add(phase);
                }
                finally { if (started && !observedExit) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); reaped++; } }
            }
            var invalid = new Configuration(1, Guid.NewGuid(), "unknown", scratch, scratch, "boot", Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
            var rejected = false; try { Validate(invalid); } catch (InvalidDataException) { rejected = true; } Require(rejected, "Invalid protocol discriminator/identity rejected"); passed.Add("InvalidProtocolRejected");
            Require(children == 7 && reaped == children, "All protocol children reaped");
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        Save(report, new { Completed = failure == null, Passed = passed, StartedChildren = children, ReapedChildren = reaped, Failure = failure, VmCrashTested = false,
            HardwarePowerLossTested = false, Scope = "Host process-cut protocol regression, never a VM crash proof" });
        return failure == null ? 0 : 1;
    }

    private static string Snapshot(string root) => string.Join("\n", Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(p => Path.GetFileName(p) != "owner.lock").Order(StringComparer.Ordinal)
        .Select(p => Path.GetRelativePath(root, p) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))));

    private sealed class CutFiles(string phase, Action<string> hold) : NomFileOperations
    {
        internal bool Armed; private bool hit;
        private void At(string point) { if (Armed && !hit && phase == point) { hit = true; hold(point); } }
        internal override void MoveObject(string source, string destination) { base.MoveObject(source, destination); At("cas-finalized"); }
        internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
        {
            if (Armed && phase == "journal-partial" && Path.GetFileName(Path.GetDirectoryName(file.Name)) == "journal")
            { base.Write(file, bytes[..Math.Min(100, bytes.Length)]); At(phase); }
            else base.Write(file, bytes);
        }
        internal override void Flush(FileStream file) { base.Flush(file); if (Path.GetFileName(Path.GetDirectoryName(file.Name)) == "checkpoints") At("checkpoint-ready"); }
        internal override void ReplaceRoot(string source, string destination) { base.ReplaceRoot(source, destination); At("root-switched"); }
        internal override void PublishBackupDirectory(string source, string destination) { base.PublishBackupDirectory(source, destination); At(destination.EndsWith("-restored", StringComparison.Ordinal) ? "restore-published" : "backup-published"); }
    }
}
