using System.Text.Json;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildInputSnapshotTests
{
    [Fact]
    public void Fingerprint_NormalizesJobAndMetadataOrderAndTimestampsButExcludesCompileTicket()
    {
        using var f = new Files(); var first = f.Capture(job: "a", ticket: 1);
        var query = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(f.Query("b"))!;
        var reordered = JsonSerializer.Serialize(new { Items = query["Items"], Properties = query["Properties"] });
        reordered = reordered.Replace("timestamp-a", "timestamp-b");
        var second = f.Capture("b", 99, query: reordered);
        Assert.Equal(first.Fingerprint, second.Fingerprint); Assert.Equal(first.EvaluationHash, second.EvaluationHash);
        Assert.Equal(first.PreprocessedHash, second.PreprocessedHash); Assert.Equal(first.Files, second.Files);
        Assert.Equal(1, first.Request.SourceRevision); Assert.Equal(99, second.Request.SourceRevision);
    }

    [Fact]
    public void EqualLengthByteEditWithRestoredTimestamp_ChangesFingerprint()
    {
        using var f = new Files(); var first = f.Capture(); var time = File.GetLastWriteTimeUtc(f.Source);
        File.WriteAllText(f.Source, "bbbb"); File.SetLastWriteTimeUtc(f.Source, time);
        var second = f.Capture();
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.EvaluationHash, second.EvaluationHash); Assert.Equal(first.PreprocessedHash, second.PreprocessedHash);
        var old = first.Files.Single(file => file.Path == f.Source); var changed = second.Files.Single(file => file.Path == f.Source);
        Assert.Equal(old.Length, changed.Length); Assert.NotEqual(old.Sha256, changed.Sha256);
    }

    [Theory] [InlineData("property")] [InlineData("import")]
    public void EvaluatedOptionOrImportedProjectChange_ChangesFingerprintWithoutSourceEdit(string change)
    {
        using var f = new Files(); var first = f.Capture();
        var second = f.Capture(query: change == "property" ? f.Query().Replace("14.0", "13.0") : null,
            imported: change == "import" ? "different imported target" : null);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint); Assert.Equal(first.Files, second.Files);
        Assert.NotEqual(change == "property" ? first.EvaluationHash : first.PreprocessedHash,
            change == "property" ? second.EvaluationHash : second.PreprocessedHash);
    }

    [Fact]
    public void CompileAdditionAndOrder_ArePartOfFingerprint()
    {
        using var f = new Files(); var first = f.Capture();
        var other = Path.Combine(f.Root, "Other.cs"); File.WriteAllText(other, "aaaa");
        var added = f.Capture(query: f.Query(sources: [f.Source, other]));
        var reversed = f.Capture(query: f.Query(sources: [other, f.Source]));
        Assert.NotEqual(first.Fingerprint, added.Fingerprint); Assert.NotEqual(added.Fingerprint, reversed.Fingerprint);
        Assert.Equal(added.Files, reversed.Files); Assert.Contains(added.Files, file => file.Path == other);
    }

    [Fact]
    public void ReferenceBytesAndAncestorEditorConfig_AreObserved()
    {
        using var f = new Files(); var reference = Path.Combine(f.Root, "Private.dll"); File.WriteAllText(reference, "first");
        var query = f.Query(reference: reference); var first = f.Capture(query: query);
        File.WriteAllText(reference, "other"); var second = f.Capture(query: query);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Contains(first.Files, file => file.Path == reference);
        File.WriteAllText(Path.Combine(f.Root, ".editorconfig"), "root = true"); var third = f.Capture(query: query);
        Assert.NotEqual(second.Fingerprint, third.Fingerprint); Assert.Contains(third.Files, file => file.Path.EndsWith(".editorconfig", StringComparison.Ordinal));
    }

    [Theory] [InlineData("PackageReference")] [InlineData("ProjectReference")]
    public void UnsupportedReferenceGraph_FailsClosed(string kind)
    {
        using var f = new Files(); var error = Assert.Throws<NotSupportedException>(() => f.Capture(query: f.Query(unsupported: kind)));
        Assert.Contains(kind, error.Message);
    }

    [Fact]
    public void MissingInput_RejectsCaptureInsteadOfPublishingPartialWitness()
    {
        using var f = new Files(); File.Delete(f.Source);
        Assert.Throws<FileNotFoundException>(() => f.Capture());
    }

    [Fact]
    public void PotentialAnalyzerConfig_CanBeAbsentButCreationChangesWitness()
    {
        using var f = new Files(); var path = Path.Combine(f.Root, ".globalconfig");
        var query = f.Query().Replace("\"Items\":{", "\"Items\":{\"GlobalAnalyzerConfigFiles\":[{\"FullPath\":" + JsonSerializer.Serialize(path) + "}],");
        var before = f.Capture(query: query); Assert.DoesNotContain(before.Files, file => file.Path == path);
        File.WriteAllText(path, "is_global = true"); var after = f.Capture(query: query);
        Assert.NotEqual(before.Fingerprint, after.Fingerprint); Assert.Contains(after.Files, file => file.Path == path);
    }

    [Fact]
    public void FileAndDocumentLimitsAndCancellation_RejectBeforeUnboundedReads()
    {
        using var f = new Files();
        var sources = Enumerable.Range(0, GameBuildInputSnapshot.MaximumFiles + 1).Select(i => Path.Combine(f.Root, i + ".cs")).ToArray();
        Assert.Throws<InvalidDataException>(() => f.Capture(query: f.Query(sources: sources)));
        Assert.Throws<InvalidDataException>(() => f.Capture(imported: new string('x', GameBuildInputSnapshot.MaximumDocumentBytes + 1)));
        Assert.ThrowsAny<OperationCanceledException>(() => f.Capture(token: new CancellationToken(true)));
        using (var stream = File.OpenWrite(f.Source)) stream.SetLength(GameBuildInputSnapshot.MaximumFileBytes + 1);
        Assert.Throws<InvalidDataException>(() => f.Capture());
    }

    [Fact]
    public void Witness_CopiesRequestDependenciesAndExposesReadOnlyFiles()
    {
        using var f = new Files(); var names = new[] { "Private.dll" };
        var snapshot = GameBuildInputSnapshot.Create(new(f.Project, "Game.dll", "Game.Session", 1, names), f.Query(), "project", f.Job("a"));
        names[0] = "changed.dll";
        Assert.Equal(new[] { "Private.dll" }, snapshot.Request.Dependencies);
        Assert.Throws<NotSupportedException>(() => ((IList<GameBuildInputFile>)snapshot.Files).Add(new("other", 0, "hash")));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.Request.Dependencies!)[0] = "other.dll");
    }

    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-InputSnapshot-" + Guid.NewGuid().ToString("N"));
        public string Project => Path.Combine(Root, "Game.csproj");
        public string Source => Path.Combine(Root, "Game.cs");
        public Files() { Directory.CreateDirectory(Root); File.WriteAllText(Project, "<Project/>"); File.WriteAllText(Source, "aaaa"); }
        public string Job(string name) => Path.Combine(Root, name);
        public string Query(string job = "a", string[]? sources = null, string? reference = null, string? unsupported = null)
        {
            object[] Rows(string kind) => unsupported == kind ? [new { Identity = "unsupported" }] : [];
            return JsonSerializer.Serialize(new
            {
                Properties = new { TargetFramework = "net10.0", LangVersion = "14.0", Temporary = Job(job) },
                Items = new Dictionary<string, object>
                {
                    ["Compile"] = (sources ?? [Source]).Select(path => new { Identity = path, FullPath = path, ModifiedTime = "timestamp-a" }).ToArray(),
                    ["Reference"] = reference == null ? Array.Empty<object>() : [new { Identity = "Private", HintPath = reference }],
                    ["PackageReference"] = Rows("PackageReference"), ["ProjectReference"] = Rows("ProjectReference")
                }
            });
        }
        public GameBuildInputSnapshot Capture(string job = "a", long ticket = 1, string? query = null, string? imported = null, CancellationToken token = default)
            => GameBuildInputSnapshot.Create(new(Project, "Game.dll", "Game.Session", ticket), query ?? Query(job),
                imported ?? "project imports at " + Job(job), Job(job), token);
        public void Dispose()
        {
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(Root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected cleanup root.");
            Directory.Delete(Root, true);
        }
    }
}
