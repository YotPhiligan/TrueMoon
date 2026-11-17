using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Importing;

public sealed class ModelSourceCaptureTests
{
    private static readonly Guid SourceId = new("38ea9f40-fb94-4ef6-b734-a1b61bb482fe");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppFactory_IsLazyCallerOwnedAndIndependent(bool microsoft)
    {
        using var fixture = new Fixture(); fixture.Gltf();
        await using var app = Build(microsoft); await app.StartAsync();
        var factory = app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!;
        await using var first = factory.Create(new(SourceId, fixture.Root, "scene.gltf"))!;
        await using var second = factory.Create(new(SourceId, fixture.Root, "absent.gltf"))!;
        Assert.NotSame(first, second); Assert.Equal(0, first.OpenHandleCount); Assert.Equal(0, second.OpenHandleCount);
        var result = await first.CaptureAsync(); Assert.Equal(2, result.Files.Count);
        await Assert.ThrowsAsync<FileNotFoundException>(() => second.CaptureAsync());
        Assert.Equal(0, first.OpenHandleCount); Assert.Equal(0, second.OpenHandleCount);
        Assert.Throws<InvalidOperationException>(() => { _ = first.CaptureAsync(); });
        await first.DisposeAsync(); await first.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => { _ = first.CaptureAsync(); });
        await app.StopAsync(); fixture.AssertUnlocked();
    }

    [Fact]
    public void DuplicateAppRegistration_IsRejected()
        => Assert.Throws<InvalidOperationException>(() => App.Build(context => context.UseAetherModelSourceCapture().UseAetherModelSourceCapture()));

    [Fact]
    public async Task InvalidFactoryArguments_AreRejectedBeforeAnyFileIo()
    {
        using var fixture = new Fixture(); await using var app = Build();
        Assert.Throws<ArgumentException>(() => Create(app, fixture, id: Guid.Empty));
        Assert.Throws<NotSupportedException>(() => Create(app, fixture, "../scene.obj"));
        Assert.Throws<NotSupportedException>(() => Create(app, fixture, "scene.fbx"));
        Assert.Throws<InvalidDataException>(() => Create(app, fixture, new string('a', 253) + ".obj"));
        foreach (var limits in new[] { new ModelSourceCaptureLimits { Files = 0 }, new ModelSourceCaptureLimits { Files = 33 },
                     new ModelSourceCaptureLimits { FileBytes = 0 }, new ModelSourceCaptureLimits { FileBytes = 16 * 1024 * 1024 + 1 },
                     new ModelSourceCaptureLimits { TotalBytes = 0 }, new ModelSourceCaptureLimits { TotalBytes = 32 * 1024 * 1024 + 1 } })
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(app, fixture, limits: limits));
        Assert.Empty(Directory.GetFiles(fixture.Root));
        var deep = Path.Combine(fixture.Root, string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("child", 129)));
        await using var deepCapture = app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!
            .Create(new(SourceId, deep, "scene.obj"))!;
        await Assert.ThrowsAsync<InvalidDataException>(() => deepCapture.CaptureAsync());
        Assert.Equal(0, deepCapture.OpenHandleCount);
    }

    [Fact]
    public async Task CapturedBytes_AreDetachedReadOnlyAndRevisionTracksIdentityAndDependencies()
    {
        using var fixture = new Fixture(); fixture.Gltf("models/scene.gltf", "data/mesh.bin");
        var original = await fixture.Capture("models/scene.gltf");
        Assert.Equal(new[] { "models/data/mesh.bin", "models/scene.gltf" }, original.Files.Select(file => file.Name));
        var bytes = original.GetFile("models/data/mesh.bin"); var copy = bytes.CopyBytes(); copy[0] = 255;
        using (var stream = bytes.OpenRead())
        {
            Assert.False(stream.CanWrite); Assert.Equal(1, stream.ReadByte());
            Assert.False(Assert.IsType<MemoryStream>(stream).TryGetBuffer(out _));
            Assert.Throws<NotSupportedException>(() => stream.WriteByte(0));
        }
        Assert.Throws<NotSupportedException>(() => ((IList<CapturedModelFile>)original.Files).Clear());
        var repeated = await fixture.Capture("models/scene.gltf"); Assert.Equal(original.InputRevision, repeated.InputRevision);
        var otherIdentity = await fixture.Capture("models/scene.gltf", id: Guid.NewGuid()); Assert.NotEqual(original.InputRevision, otherIdentity.InputRevision);
        fixture.Write("models/data/mesh.bin", new byte[] { 4, 3, 2, 1 });
        var changed = await fixture.Capture("models/scene.gltf"); Assert.NotEqual(original.InputRevision, changed.InputRevision);
        File.Delete(Path.Combine(fixture.Root, "models/data/mesh.bin"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes.CopyBytes()); fixture.AssertUnlocked();
    }

    [Fact]
    public async Task Revision_DoesNotDependOnPhysicalDirectoryOrFileCreationOrder()
    {
        using var first = new Fixture(); using var second = new Fixture(); first.Gltf();
        second.Write("data.bin", new byte[] { 1, 2, 3, 4 }); second.Write("scene.gltf", File.ReadAllBytes(Path.Combine(first.Root, "scene.gltf")));
        Assert.Equal((await first.Capture()).InputRevision, (await second.Capture()).InputRevision);
    }

    [Theory]
    [InlineData(125, true)] [InlineData(126, true)] [InlineData(127, false)]
    public async Task UnicodeNames_RespectNormalizedUtf8ByteBudget(int letters, bool accepted)
    {
        using var fixture = new Fixture(); var name = new string('\u00e9', letters) + ".obj";
        if (accepted) { fixture.Write(name, "v 0 0 0\n"); Assert.Equal(name, (await fixture.Capture(name)).RootName); }
        else { await using var app = Build(); Assert.Throws<InvalidDataException>(() => Create(app, fixture, name)); }
    }

    [Theory]
    [InlineData(508, true)] [InlineData(509, true)] [InlineData(510, false)]
    public async Task RawSpelling_IsBoundedBeforePathSplitting(int dotPrefixes, bool accepted)
    {
        using var fixture = new Fixture(); await using var app = Build();
        var raw = string.Concat(Enumerable.Repeat("./", dotPrefixes)) + "xx.obj";
        if (accepted) { await using var owner = Create(app, fixture, raw); Assert.Equal(0, owner.OpenHandleCount); }
        else Assert.Throws<InvalidDataException>(() => Create(app, fixture, raw));
        fixture.Write("scene.gltf", GltfJson(new string('a', 1025) + ".bin"));
        await fixture.Fails<InvalidDataException>();
    }

    [Fact]
    public async Task DefaultFileCount_AcceptsThirtyTwoAndRejectsThirtyThree()
    {
        using var fixture = new Fixture();
        for (var index = 0; index < 32; index++) fixture.Write($"data{index}.bin", new byte[] { 1 });
        string Json(int count) => "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[" + string.Join(',', Enumerable.Range(0, count)
            .Select(index => $"{{\"uri\":\"data{index}.bin\",\"byteLength\":1}}")) + "]}";
        fixture.Write("scene.gltf", Json(31)); Assert.Equal(32, (await fixture.Capture()).Files.Count);
        fixture.Write("scene.gltf", Json(32)); await fixture.Fails<InvalidDataException>(); fixture.AssertUnlocked();
    }

    [Fact]
    public async Task Discovery_BoundsDistinctReferencesBeforeOpeningMissingDependencies()
    {
        using var fixture = new Fixture();
        fixture.Write("scene.gltf", "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[" + string.Join(',', Enumerable.Range(0, 32)
            .Select(index => $"{{\"uri\":\"missing{index}.bin\",\"byteLength\":1}}")) + "]}");
        await fixture.Fails<InvalidDataException>();
        fixture.Write("scene.obj", string.Concat(Enumerable.Range(0, 32).Select(index => $"mtllib missing{index}.mtl\n")));
        await fixture.Fails<InvalidDataException>("scene.obj");
    }

    [Fact]
    public async Task RepeatedBufferReferences_UseGreatestDeclaredLength()
    {
        using var fixture = new Fixture(); fixture.Write("data.bin", new byte[] { 1, 2, 3, 4 });
        string Json(int last) => $"{{\"asset\":{{\"version\":\"2.0\"}},\"buffers\":[{{\"uri\":\"data.bin\",\"byteLength\":4}},{{\"uri\":\"./data.bin\",\"byteLength\":{last}}}]}}";
        fixture.Write("scene.gltf", Json(1)); Assert.Equal(2, (await fixture.Capture()).Files.Count);
        fixture.Write("scene.gltf", Json(5)); await fixture.Fails<InvalidDataException>();
    }

    [Theory]
    [InlineData("file", -1, false)] [InlineData("file", 0, true)] [InlineData("file", 1, true)]
    [InlineData("total", -1, false)] [InlineData("total", 0, true)] [InlineData("total", 1, true)]
    [InlineData("count", -1, false)] [InlineData("count", 0, true)] [InlineData("count", 1, true)]
    public async Task Budgets_RejectBelowAndAcceptAtOrAboveExactInput(string budget, int delta, bool accepted)
    {
        using var fixture = new Fixture(); fixture.Gltf();
        var rootLength = (int)new FileInfo(Path.Combine(fixture.Root, "scene.gltf")).Length;
        var limits = budget switch
        {
            "file" => new ModelSourceCaptureLimits { FileBytes = rootLength + delta },
            "total" => new ModelSourceCaptureLimits { TotalBytes = rootLength + 4 + delta },
            _ => new ModelSourceCaptureLimits { Files = 2 + delta }
        };
        await using var app = Build(); await using var capture = Create(app, fixture, limits: limits);
        if (accepted) Assert.Equal(2, (await capture.CaptureAsync()).Files.Count);
        else await Assert.ThrowsAsync<InvalidDataException>(() => capture.CaptureAsync());
        Assert.Equal(0, capture.OpenHandleCount); fixture.AssertUnlocked();
    }

    [Theory]
    [InlineData("../outside.bin")] [InlineData("/absolute.bin")] [InlineData("C:drive.bin")]
    [InlineData("https://host/data.bin")] [InlineData("data:application/octet-stream;base64,AA==")]
    [InlineData("name%20.bin")] [InlineData("data.bin?query")] [InlineData("data.bin#fragment")]
    [InlineData("a//b.bin")] [InlineData("NUL.bin")] [InlineData("COM1.dat")] [InlineData("trailing./x.bin")]
    [InlineData("COM\u00b9.bin")] [InlineData("CONOUT$.bin")]
    public async Task UnsafeDependencies_AreRejectedWithoutFallback(string uri)
    {
        using var fixture = new Fixture(); fixture.Write("scene.gltf", GltfJson(uri));
        await fixture.Fails<NotSupportedException>(); fixture.AssertUnlocked();
    }

    [Fact]
    public async Task MissingAndCaseCollidingDependencies_FailWithoutBasenameLookup()
    {
        using var fixture = new Fixture(); fixture.Write("model/scene.gltf", GltfJson("data.bin")); fixture.Write("data.bin", new byte[] { 1, 2, 3, 4 });
        await fixture.Fails<FileNotFoundException>("model/scene.gltf");
        fixture.Write("scene.gltf", """{"asset":{"version":"2.0"},"buffers":[{"uri":"data.bin","byteLength":4},{"uri":"DATA.bin","byteLength":4}]}""");
        await fixture.Fails<InvalidDataException>(); fixture.AssertUnlocked();
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[],\"buffers\":[]}")]
    [InlineData("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":4}]}")]
    [InlineData("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"data.bin\",\"byteLength\":5}]}")]
    [InlineData("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":null,\"byteLength\":4}]}")]
    public async Task MalformedJsonAndBufferLengths_NeverReturnPartialCapture(string json)
    {
        using var fixture = new Fixture(); fixture.Write("scene.gltf", json); fixture.Write("data.bin", new byte[] { 1, 2, 3, 4 });
        await fixture.Fails<InvalidDataException>(); fixture.AssertUnlocked();
    }

    [Theory]
    [InlineData("{\"asset\":{\"version\":\"1.0\"}}")]
    [InlineData("{\"asset\":{\"version\":\"2.0\",\"minVersion\":\"2.1\"}}")]
    [InlineData("{\"asset\":{\"version\":\"2.0\"},\"images\":[{\"uri\":\"x.png\"}]}")]
    [InlineData("{\"asset\":{\"version\":\"2.0\"},\"extensionsUsed\":[\"KHR_draco_mesh_compression\"]}")]
    public async Task UnsupportedProfiles_AreExplicit(string json)
    {
        using var fixture = new Fixture(); fixture.Write("scene.gltf", json); await fixture.Fails<NotSupportedException>();
    }

    [Fact]
    public async Task ObjMaterialDiscovery_IsRelativeDeduplicatedAndRejectsTextureOrIncludeDirectives()
    {
        using var fixture = new Fixture(); fixture.Write("models/scene.obj", "\uFEFFmtllib first.mtl ./second.mtl first.mtl # comment\nv 0 0 0\n");
        fixture.Write("models/first.mtl", "newmtl A\nKd 1 0 0\n"); fixture.Write("models/second.mtl", "newmtl B\nKd 0 1 0\n");
        var captured = await fixture.Capture("models/scene.obj"); Assert.Equal(3, captured.Files.Count);
        Assert.Contains(captured.Files, file => file.Name == "models/second.mtl");
        fixture.Write("models/first.mtl", "map_Kd texture.png\n"); await fixture.Fails<NotSupportedException>("models/scene.obj");
        fixture.Write("models/scene.obj", "call other.obj\n"); await fixture.Fails<NotSupportedException>("models/scene.obj");
        fixture.Write("models/scene.obj", "mtllib missing.mtl\n"); await fixture.Fails<FileNotFoundException>("models/scene.obj"); fixture.AssertUnlocked();
    }

    [Fact]
    public async Task TextAndJsonBudgets_RejectLongLinesDeepJsonAndExcessTokens()
    {
        using var fixture = new Fixture(); fixture.Write("scene.obj", new string(' ', 4096)); Assert.Single((await fixture.Capture("scene.obj")).Files);
        fixture.Write("scene.obj", new string(' ', 4097)); await fixture.Fails<InvalidDataException>("scene.obj");
        fixture.Write("scene.obj", new byte[] { 0xff, 0xfe, 0xff }); await fixture.Fails<InvalidDataException>("scene.obj");
        fixture.Write("scene.gltf", "{\"asset\":{\"version\":\"2.0\"},\"extras\":" + new string('[', 65) + "0" + new string(']', 65) + "}");
        await fixture.Fails<InvalidDataException>();
        fixture.Write("scene.gltf", "{\"asset\":{\"version\":\"2.0\"},\"extras\":[" + string.Join(',', Enumerable.Repeat("0", 131072)) + "]}");
        await fixture.Fails<InvalidDataException>();
    }

    [Fact]
    public async Task Glb_EmbeddedAndExternalBuffersAreCapturedAndMalformedContainersRejected()
    {
        using var fixture = new Fixture();
        fixture.Write("scene.glb", Glb("""{"asset":{"version":"2.0"},"buffers":[{"byteLength":3}]}""", new byte[] { 1, 2, 3, 0 }));
        Assert.Single((await fixture.Capture("scene.glb")).Files);
        fixture.Write("data.bin", new byte[] { 1, 2, 3, 4 }); fixture.Write("scene.glb", Glb(GltfJson("data.bin")));
        Assert.Equal(2, (await fixture.Capture("scene.glb")).Files.Count);
        var valid = File.ReadAllBytes(Path.Combine(fixture.Root, "scene.glb"));
        foreach (var offset in new[] { 0, 4, 8, 12, 16 })
        {
            var broken = (byte[])valid.Clone(); broken[offset] ^= 1; fixture.Write("scene.glb", broken);
            await using var app = Build(); await using var capture = Create(app, fixture, "scene.glb");
            var error = await Record.ExceptionAsync(() => capture.CaptureAsync());
            Assert.True(error is InvalidDataException or NotSupportedException, error?.ToString()); Assert.Equal(0, capture.OpenHandleCount);
        }
    }

    [Fact]
    public async Task LongPhysicalDirectories_UseBoundedFinalNameResizeAndReleasePins()
    {
        using var fixture = new Fixture();
        var directory = Path.Combine(fixture.Root, string.Join(Path.DirectorySeparatorChar,
            Enumerable.Range(0, 10).Select(index => new string('d', 49) + index)));
        Directory.CreateDirectory(directory); await File.WriteAllTextAsync(Path.Combine(directory, "scene.obj"), "v 0 0 0\n");
        Assert.True(directory.Length > 512);
        await using var app = Build();
        await using var operation = app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!
            .Create(new(SourceId, directory, "scene.obj"))!;
        Assert.Equal("scene.obj", Assert.Single((await operation.CaptureAsync()).Files).Name);
        Assert.Equal(0, operation.OpenHandleCount); fixture.AssertUnlocked();
    }

    [Fact]
    public async Task ReparseDirectoriesAndWriterConflicts_AreRejectedAndHandlesReleased()
    {
        using var fixture = new Fixture(); using var outside = new Fixture(); outside.Write("data.bin", new byte[] { 1, 2, 3, 4 });
        var link = Path.Combine(fixture.Root, "linked");
        var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, outside.Root }) info.ArgumentList.Add(argument);
        using (var process = Process.Start(info)!)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(10000)); Assert.Equal(0, process.ExitCode); await stdout; await stderr;
        }
        try
        {
            fixture.Write("scene.gltf", GltfJson("linked/data.bin")); await fixture.Fails<NotSupportedException>();
        }
        finally { Directory.Delete(link); }
        fixture.Gltf();
        using (var writer = new FileStream(Path.Combine(fixture.Root, "data.bin"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            await fixture.Fails<IOException>();
        Assert.Equal(2, (await fixture.Capture()).Files.Count); fixture.AssertUnlocked();
    }

    [Fact]
    public async Task PreCancellationAndDispose_AreTerminalAndReleaseResources()
    {
        using var fixture = new Fixture(); fixture.Gltf(); await using var app = Build();
        await using var cancelled = Create(app, fixture); using var token = new CancellationTokenSource(); token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.CaptureAsync(token.Token)); Assert.Equal(0, cancelled.OpenHandleCount);
        await using var idle = Create(app, fixture); await idle.DisposeAsync(); Assert.Equal(0, idle.OpenHandleCount);
        Assert.Throws<ObjectDisposedException>(() => { _ = idle.CaptureAsync(); });
        fixture.Write("large.obj", Enumerable.Repeat((byte)'\n', 16 * 1024 * 1024).ToArray());
        await using var active = Create(app, fixture, "large.obj");
        var work = active.CaptureAsync(); await active.DisposeAsync();
        var failure = await Record.ExceptionAsync(() => work);
        Assert.True(failure is OperationCanceledException || failure == null, failure?.ToString());
        if (failure == null) Assert.Equal(16 * 1024 * 1024, Assert.Single((await work).Files).Length);
        Assert.True(work.IsCompleted); Assert.Equal(0, active.OpenHandleCount); fixture.AssertUnlocked();
    }

    private static IApp Build(bool microsoft = false) => App.Builder(builder => { if (microsoft) builder.UseDI(); })
        .Setup(context => context.UseAetherModelSourceCapture()).Build();
    private static ModelSourceCapture Create(IApp app, Fixture fixture, string root = "scene.gltf", ModelSourceCaptureLimits? limits = null, Guid? id = null)
        => app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!.Create(new(id ?? SourceId, fixture.Root, root, limits))!;
    private static string GltfJson(string uri) => "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":" + System.Text.Json.JsonSerializer.Serialize(uri) + ",\"byteLength\":4}]}";
    private static byte[] Glb(string json, byte[]? bin = null)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json); var length = (jsonBytes.Length + 3) & ~3;
        var bytes = new byte[20 + length + (bin == null ? 0 : 8 + bin.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546c67); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), bytes.Length); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4e4f534a); bytes.AsSpan(20, length).Fill(32); jsonBytes.CopyTo(bytes, 20);
        if (bin != null) { BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + length), bin.Length); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24 + length), 0x004e4942); bin.CopyTo(bytes, 28 + length); }
        return bytes;
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-ModelCapture-" + Guid.NewGuid().ToString("N"));
        internal Fixture() => Directory.CreateDirectory(Root);
        internal void Write(string name, string text) => Write(name, Encoding.UTF8.GetBytes(text));
        internal void Write(string name, byte[] bytes) { var path = Path.Combine(Root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); }
        internal void Gltf(string root = "scene.gltf", string bin = "data.bin")
        { Write(root, GltfJson(bin)); Write(Path.Combine(Path.GetDirectoryName(root) ?? "", bin), new byte[] { 1, 2, 3, 4 }); }
        internal async Task<CapturedModelSource> Capture(string root = "scene.gltf", ModelSourceCaptureLimits? limits = null, Guid? id = null)
        { await using var app = Build(); await using var capture = Create(app, this, root, limits, id); var result = await capture.CaptureAsync(); Assert.Equal(0, capture.OpenHandleCount); return result; }
        internal async Task Fails<T>(string root = "scene.gltf") where T : Exception
        { await using var app = Build(); await using var capture = Create(app, this, root); await Assert.ThrowsAsync<T>(() => capture.CaptureAsync()); Assert.Equal(0, capture.OpenHandleCount); }
        internal void AssertUnlocked() { foreach (var path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories)) { using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.True(file.CanWrite); } }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
