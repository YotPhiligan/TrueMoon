using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Resources;

internal sealed class ResourceGate
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly ManualResetEventSlim Release = new();
}
internal sealed record ResourceProofOptions(ResourceGate? BeforeCommit = null, ResourceGate? AfterCommit = null,
    string? CrashPoint = null, bool ThrowAfterCommit = false)
{
    internal void At(string point)
    {
        var gate = point == "before-commit" ? BeforeCommit : point == "after-commit" ? AfterCommit : null;
        if (gate != null) { gate.Entered.TrySetResult(); if (!gate.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Resource proof gate."); }
        if (point == CrashPoint) { Console.WriteLine("cut:" + point); Console.Out.Flush(); Thread.Sleep(Timeout.Infinite); }
    }
}

internal static class ResourceCodec
{
    internal const int BufferBytes = 64 * 1024;
    internal const long MaximumObject = 512L * 1024 * 1024;
    internal const long MaximumBatch = 1024L * 1024 * 1024;
    internal static void Digest(string digest)
    {
        if (digest is null || digest.Length != 64 || digest.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidDataException("Canonical SHA256 digest required.");
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string Revision(byte[] metadata, string digest, long length)
    { _=CatalogCodec.Decode(metadata); return NomResourceMetadataCodec.HashCanonicalMetadata(metadata,digest,length); }
    internal static async Task<string> HashStream(Stream stream, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[BufferBytes];
        int count; while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0) hash.AppendData(buffer.AsSpan(0,count));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
