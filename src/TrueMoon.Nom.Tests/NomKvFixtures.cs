using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Xunit;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

internal sealed class NomKvTestDirectory : IDisposable
{
    private readonly string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-Nom-Kv-" + Guid.NewGuid().ToString("N"));
    internal NomKvOpenArguments Arguments(NomFormatLimits? limits = null) =>
        new(Root, NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId, limits);
    internal static string Journal(string root, long segment = 1) => Path.Combine(root, "journal", segment.ToString("D20") + ".nom");
    internal static byte[] ReadBytes(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); return bytes;
    }
    internal static Dictionary<string, string> Snapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
    internal static void AssertSnapshot(Dictionary<string, string> expected, string root)
    {
        var actual = Snapshot(root); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
    internal static void AssertLocked(string root) => Assert.Throws<IOException>(() =>
    { using var file = new FileStream(Path.Combine(root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
    internal static void AssertReleased(string root)
    {
        using var file = new FileStream(Path.Combine(root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(file.CanWrite);
    }
    public void Dispose()
    {
        var target = Path.GetFullPath(Root);
        if (!target.StartsWith(temporary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(target).StartsWith("TrueMoon-Nom-Kv-", StringComparison.Ordinal)) throw new InvalidOperationException("KV cleanup boundary.");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }
}

internal sealed class NomKvTestGate
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly ManualResetEventSlim Release = new();
    internal void Wait()
    { Entered.TrySetResult(); if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("KV test gate."); }
}

internal sealed class NomKvTestIo : NomKvFileOperations
{
    private sealed class Fault(Exception error, int occurrence, bool persistent, int prefix, bool after)
    {
        internal readonly Exception Error = error;
        internal readonly int Occurrence = occurrence;
        internal readonly bool Persistent = persistent;
        internal readonly int Prefix = prefix;
        internal readonly bool After = after;
        internal int Count;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Fault> faults = [];
    internal readonly ConcurrentQueue<(string Point, string Path)> Events = new();
    internal readonly ConcurrentQueue<(string Path, SafeFileHandle Handle)> Opened = new();
    internal NomKvTestGate? WriteGate;
    internal NomKvTestGate? CloseGate;
    internal string? BlockedClosePath;
    internal bool UseReadFaultStreams;
    internal Action? AfterJournalWrite;
    internal int PrefixWritten;
    internal void Arm(string point, Exception error, int occurrence = 1, bool persistent = false, int prefix = 0, bool after = false)
    { lock (gate) faults[point] = new(error, occurrence, persistent, prefix, after); }
    internal void Disarm() { lock (gate) faults.Clear(); WriteGate?.Release.Set(); CloseGate?.Release.Set(); }
    private Fault? Hit(string point, string path)
    {
        Events.Enqueue((point, path));
        lock (gate)
        {
            if (!faults.TryGetValue(point, out var fault)) return null;
            fault.Count++; return fault.Count == fault.Occurrence || fault.Persistent && fault.Count > fault.Occurrence ? fault : null;
        }
    }
    private static string Kind(string path) => Path.GetFileName(path) switch
    { "owner.lock" => "lock", "nom.root" => "root", "nom.root.pending" => "pending", _ => "journal" };
    internal override FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
    {
        if (Hit("open-" + Kind(path), path) is { } fault) throw fault.Error;
        var file = UseReadFaultStreams
            ? new NomKvReadFaultStream(path, mode, access, share, BeforeRead)
            : base.OpenFile(path, mode, access, share);
        Opened.Enqueue((path, file.SafeFileHandle)); return file;
    }
    private void BeforeRead(string path) { if (Hit("read-" + Kind(path), path) is { } fault) throw fault.Error; }
    internal override void Write(FileStream file, ReadOnlyMemory<byte> bytes)
    {
        var kind = Kind(file.Name); if (kind == "journal") WriteGate?.Wait();
        if (Hit("write-" + kind, file.Name) is { } fault)
        {
            var count = fault.After ? bytes.Length : Math.Min(fault.Prefix, bytes.Length);
            if (count > 0) base.Write(file, bytes[..count]); PrefixWritten += count; throw fault.Error;
        }
        base.Write(file, bytes); if (kind == "journal") AfterJournalWrite?.Invoke();
    }
    internal override void Flush(FileStream file)
    {
        if (Hit("flush-" + Kind(file.Name), file.Name) is { } fault)
        { if (fault.After) base.Flush(file); throw fault.Error; }
        base.Flush(file);
    }
    internal override void Truncate(FileStream file, long length)
    {
        if (Hit("truncate", file.Name) is { } fault)
        { if (fault.After) base.Truncate(file, length); throw fault.Error; }
        base.Truncate(file, length);
    }
    internal override void PublishRoot(string source, string destination)
    {
        if (Hit("publish", destination) is { } fault)
        { if (fault.After) base.PublishRoot(source, destination); throw fault.Error; }
        base.PublishRoot(source, destination);
    }
    internal override void Close(FileStream file)
    {
        if (CloseGate is { } closeGate) { BlockedClosePath = file.Name; closeGate.Wait(); }
        if (Hit("close-" + Kind(file.Name), file.Name) is { } fault)
        { if (fault.After) base.Close(file); throw fault.Error; }
        base.Close(file);
    }
}

internal sealed class NomKvReadFaultStream(string path, FileMode mode, FileAccess access, FileShare share, Action<string> beforeRead)
    : FileStream(path, mode, access, share, 1, FileOptions.RandomAccess)
{
    public override int Read(Span<byte> buffer) { beforeRead(Name); return base.Read(buffer); }
}

internal sealed class NomKvTestContext : IDisposable
{
    internal readonly NomKvTestDirectory Directory = new();
    internal readonly NomKvTestIo Io = new();
    internal readonly NomKvStoreFactory Factory;
    internal NomKvTestContext(int maximumOwners = 4) => Factory = new(Io, maximumOwners);
    public void Dispose()
    { Io.Disarm(); Factory.Dispose(); Directory.Dispose(); }
}

internal static class NomKvTestData
{
    internal static readonly Guid Operation = Guid.Parse("6be0c870-7a1f-4ec2-a10e-1e4eaf27fe4b");
    internal static NomJournalBatch Batch(long sequence, long expected, long generation, params NomJournalMutation[] mutations) =>
        new(sequence, expected, generation, Operation, mutations);
    internal static NomKvWriteReceipt Seed(NomKvStore store) => store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([1], [10, 11]), new NomJournalMutation([2], [])));
    internal static byte[] NextFrame() => NomJournalCodec.EncodeBatch(NomFormatFixtures.Header,
        Batch(2, 1, 2, new NomJournalMutation([1], [20, 21]), new NomJournalMutation([2], null), new NomJournalMutation([3], [30])));
    internal static IEnumerable<Exception> Flatten(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate)
            foreach (var inner in aggregate.InnerExceptions) foreach (var value in Flatten(inner)) yield return value;
        else if (error.InnerException is { } inner) foreach (var value in Flatten(inner)) yield return value;
    }
}
