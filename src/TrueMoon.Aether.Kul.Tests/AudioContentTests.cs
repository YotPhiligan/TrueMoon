using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Kul.Runtime.Content;
using Xunit;

namespace TrueMoon.Aether.Kul.Tests;

public sealed class AudioContentTests
{
    private static AudioContentAsset Descriptor(int index = 1) => new(new(new Guid($"e533469b-d324-45c9-8aaf-{index:000000000000}")), $"audio{index}.wav", new string('a', 64), 100, AudioAssetKind.Sound);

    [Fact]
    public void Manifest_CopiesCanonicalizesSortsAndRoundTripsDeterministically()
    {
        var original = new[] { Descriptor(2), Descriptor(1) }; var manifest = new AudioContentManifest(original);
        original[0] = Descriptor(3);
        Assert.Equal(new[] { Descriptor(1).Id, Descriptor(2).Id }, manifest.Assets.Select(asset => asset.Id));
        Assert.All(manifest.Assets, asset => Assert.Equal(new string('A', 64), asset.Sha256));
        Assert.Throws<NotSupportedException>(() => ((IList<AudioContentAsset>)manifest.Assets)[0] = Descriptor(9));
        var bytes = manifest.Encode(); var restored = AudioContentManifest.Decode(bytes);
        Assert.Equal(manifest.Assets, restored.Assets);
        Assert.Equal(bytes, new AudioContentManifest([Descriptor(1), Descriptor(2)]).Encode());
        Assert.Equal(bytes, restored.Encode());
        Assert.Empty(AudioContentManifest.Decode(new AudioContentManifest([]).Encode()).Assets);
    }

    [Fact]
    public void Manifest_AdmitsExactly64UniqueAssetsAndRejectsTheNext()
    {
        Assert.Equal(64, new AudioContentManifest(Enumerable.Range(1, 64).Select(Descriptor)).Assets.Count);
        Assert.Throws<InvalidDataException>(() => new AudioContentManifest(Enumerable.Range(1, 65).Select(Descriptor)));
        Assert.Throws<ArgumentNullException>(() => new AudioContentManifest(null!));
        Assert.Throws<InvalidDataException>(() => new AudioContentManifest([null!]));
        Assert.Throws<InvalidDataException>(() => new AudioContentManifest([Descriptor(), Descriptor()]));
        Assert.Throws<InvalidDataException>(() => new AudioContentManifest([Descriptor(), Descriptor(2) with { FileName = "AUDIO1.WAV" }]));
    }

    [Theory]
    [InlineData("../a.wav")] [InlineData("a/b.wav")] [InlineData("a\\b.wav")] [InlineData("C:a.wav")]
    [InlineData("NUL.wav")] [InlineData("com1.wav")] [InlineData("a.mp3")] [InlineData("a.wav ")]
    [InlineData("a.wav.")] [InlineData("")] [InlineData("a\n.wav")] [InlineData("*.wav")]
    public void Manifest_RejectsNonportableOrUnsupportedFileNames(string name)
        => Assert.Throws<InvalidDataException>(() => new AudioContentManifest([Descriptor() with { FileName = name }]));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void Manifest_RejectsInvalidIdentityHashLengthKindAndVoiceReservations(int invalid)
    {
        var value = Descriptor();
        value = invalid switch
        {
            0 => value with { Id = default }, 1 => value with { Sha256 = new string('g', 64) },
            2 => value with { Sha256 = new string('a', 63) }, 3 => value with { Length = 0 },
            4 => value with { Kind = 0 }, 5 => value with { Voices = 0 }, 6 => value with { Voices = 17 },
            7 => value with { Length = -1 }, _ => value with { Sha256 = null! }
        };
        Assert.Throws<InvalidDataException>(() => new AudioContentManifest([value]));
    }

    [Theory]
    [InlineData("{}")] [InlineData("null")] [InlineData("{\"Version\":2,\"Assets\":[]}")]
    [InlineData("{\"Version\":1,\"Assets\":null}")] [InlineData("{\"Version\":1,\"Assets\":[],\"extra\":1}")]
    [InlineData("{\"Version\":1,\"Version\":1,\"Assets\":[]}")] [InlineData("{")]
    public void Manifest_RejectsMissingUnsupportedUnknownDuplicateAndMalformedMetadata(string json)
        => Assert.Throws<InvalidDataException>(() => AudioContentManifest.Decode(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Manifest_RejectsEmptyAndOversizedRawMetadata()
    {
        Assert.Throws<InvalidDataException>(() => AudioContentManifest.Decode([]));
        Assert.Throws<InvalidDataException>(() => AudioContentManifest.Decode(new byte[AudioContentManifest.MaximumBytes + 1]));
        Assert.Equal(1, new AudioContentManifest([Descriptor() with { Voices = 1 }]).Assets.Single().Voices);
        Assert.Equal(16, new AudioContentManifest([Descriptor() with { Voices = 16 }]).Assets.Single().Voices);
        Assert.Equal(AudioAssetKind.Music, new AudioContentManifest([Descriptor() with { Kind = AudioAssetKind.Music }]).Assets.Single().Kind);
    }

    [Fact]
    public void Reference_RequiresAbsoluteContentAddressedDirectoryAndFullHash()
    {
        var hash = new string('a', 64); var path = Path.Combine(Path.GetTempPath(), hash);
        var reference = new AudioContentReference(path + Path.DirectorySeparatorChar, hash);
        Assert.Equal(Path.GetFullPath(path), reference.DirectoryPath); Assert.Equal(hash.ToUpperInvariant(), reference.ManifestSha256);
        Assert.Throws<ArgumentException>(() => new AudioContentReference("relative", hash));
        Assert.Throws<ArgumentException>(() => new AudioContentReference(Path.GetTempPath(), hash));
        Assert.Throws<ArgumentException>(() => new AudioContentReference(path, "abc"));
        Assert.Throws<ArgumentException>(() => new AudioContentReference(path, new string('z', 64)));
    }

    [WindowsAudioFact]
    public async Task ContentLease_VerifiesAllFilesPinsAgainstMutationAndReleasesOnDispose()
    {
        await using var fixture = new AudioFixture();
        using var lease = AudioContentLease.Open(new(fixture.Content, 2, fixture.Manifest.Assets.Sum(asset => asset.Length)));
        Assert.Equal(fixture.Manifest.Assets, lease.Manifest.Assets);
        Assert.Equal(fixture.PathFor(AudioFixture.SoundId), lease.GetPath(AudioFixture.SoundId));
        Assert.Throws<KeyNotFoundException>(() => lease.GetPath(new(Guid.NewGuid())));
        Assert.Throws<IOException>(() => File.Open(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.Write, FileShare.Read));
        Assert.Throws<IOException>(() => File.Delete(fixture.PathFor(AudioFixture.OtherId)));
        Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(fixture.Content.DirectoryPath, AudioContentManifest.FileName), "changed"));
        lease.Dispose(); lease.Dispose();
        Assert.Throws<ObjectDisposedException>(() => lease.GetPath(AudioFixture.SoundId));
        using var writable = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(fixture.Manifest.Assets.Single(asset => asset.Id == AudioFixture.SoundId).Length, writable.Length);
    }

    [WindowsAudioTheory]
    [InlineData("hash")] [InlineData("length")] [InlineData("missing")] [InlineData("extra")]
    [InlineData("directory")] [InlineData("manifest")]
    public async Task ContentLease_RejectsChangedIncompleteAndExtraCaptureThenReleasesPartialPins(string mutation)
    {
        await using var fixture = new AudioFixture(); var file = fixture.PathFor(AudioFixture.OtherId);
        switch (mutation)
        {
            case "hash": var bytes = File.ReadAllBytes(file); bytes[^1] ^= 127; File.WriteAllBytes(file, bytes); break;
            case "length": File.AppendAllText(file, "x"); break;
            case "missing": File.Delete(file); break;
            case "extra": File.WriteAllText(Path.Combine(fixture.Content.DirectoryPath, "extra.wav"), "x"); break;
            case "directory": File.Delete(file); Directory.CreateDirectory(file); break;
            case "manifest": File.AppendAllText(Path.Combine(fixture.Content.DirectoryPath, AudioContentManifest.FileName), " "); break;
        }
        Assert.Throws<InvalidDataException>(() => AudioContentLease.Open(new(fixture.Content, 32, 1024 * 1024)));
        using var metadata = new FileStream(Path.Combine(fixture.Content.DirectoryPath, AudioContentManifest.FileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var sound = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(metadata.Length > 0); Assert.True(sound.Length > 0);
    }

    [WindowsAudioFact]
    public async Task ContentLease_EnforcesExactEncodedAndAssetBoundariesAndPrecancelledAdmission()
    {
        await using var fixture = new AudioFixture(); var bytes = fixture.Manifest.Assets.Sum(asset => asset.Length);
        using (var lease = AudioContentLease.Open(new(fixture.Content, 2, bytes))) Assert.Equal(2, lease.Manifest.Assets.Count);
        Assert.Throws<InvalidDataException>(() => AudioContentLease.Open(new(fixture.Content, 1, bytes)));
        Assert.Throws<InvalidDataException>(() => AudioContentLease.Open(new(fixture.Content, 2, bytes - 1)));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(cancel.Token, Assert.Throws<OperationCanceledException>(() => AudioContentLease.Open(new(fixture.Content, 2, bytes, cancel.Token))).CancellationToken);
        using var exclusive = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }
}
