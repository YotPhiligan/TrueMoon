using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.ModelCaptureConsumer;

internal static class Program
{
    private static readonly Guid SourceId = new("5c1bc6f0-c312-4cf6-ad16-908e1e96ad74");
    private static readonly string Gltf = """{"asset":{"version":"2.0"},"buffers":[{"uri":"mesh.bin","byteLength":4}]}""";
    private static async Task<int> Main(string[] arguments)
    {
        if (arguments.Length != 2 || arguments[0] is not ("cobalt" or "microsoft")) return 2;
        var evidence = Path.GetFullPath(arguments[1]);
        if (Directory.Exists(evidence)) throw new IOException("Use a new evidence directory.");
        Directory.CreateDirectory(evidence);
        var report = new Report { Provider = arguments[0] };
        var root = Path.Combine(evidence, "inputs");
        try
        {
            await using (var app = App.Builder(builder => { if (arguments[0] == "microsoft") builder.UseDI(); })
                             .Setup(context => context.UseAetherModelSourceCapture()).Build())
            {
                await app.StartAsync();
                var factory = app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!;
                await using (var lazy = factory.Create(new(SourceId, root, "models/scene.gltf"))!)
                    Check(!Directory.Exists(root) && lazy.OpenHandleCount == 0, "lazy factory", report);
                report.Groups.Add("AppFactoryAndOwnership");
                Directory.CreateDirectory(Path.Combine(root, "models"));
                await File.WriteAllTextAsync(Path.Combine(root, "models/scene.gltf"), Gltf);
                await File.WriteAllBytesAsync(Path.Combine(root, "models/mesh.bin"), new byte[] { 1, 2, 3, 4 });
                async Task<CapturedModelSource> Capture(string name, ModelSourceCaptureLimits? limits = null)
                {
                    var owner = factory.Create(new(SourceId, root, name, limits))!; report.OwnersCreated++;
                    try { return await owner.CaptureAsync(); }
                    finally { await owner.DisposeAsync(); report.OwnersDisposed++; Check(owner.OpenHandleCount == 0, "capture cleanup", report); }
                }
                var source = await Capture("models/scene.gltf");
                Check(source.SourceId == SourceId && source.Files.Select(file => file.Name).SequenceEqual(new[] { "models/mesh.bin", "models/scene.gltf" }), "closure names", report);
                Check(source.GetFile("models/mesh.bin").CopyBytes().SequenceEqual(new byte[] { 1, 2, 3, 4 }), "literal dependency bytes", report);
                report.FirstRevision = source.InputRevision;
                Check((await Capture("models/scene.gltf")).InputRevision == source.InputRevision, "repeat revision", report);
                var copy = source.GetFile("models/mesh.bin").CopyBytes(); copy[0] = 99;
                using (var stream = source.GetFile("models/mesh.bin").OpenRead()) Check(!stream.CanWrite && stream.ReadByte() == 1, "detached immutable bytes", report);
                report.Groups.Add("GltfClosureAndImmutableRevision");
                await File.WriteAllBytesAsync(Path.Combine(root, "models/mesh.bin"), new byte[] { 4, 3, 2, 1 });
                Check((await Capture("models/scene.gltf")).InputRevision != source.InputRevision, "dependency invalidates revision", report);
                File.Delete(Path.Combine(root, "models/mesh.bin"));
                Check(source.GetFile("models/mesh.bin").CopyBytes()[0] == 1, "source deletion preserves snapshot", report);
                await Expect<FileNotFoundException>(() => Capture("models/scene.gltf"), report);
                await File.WriteAllBytesAsync(Path.Combine(root, "models/mesh.bin"), new byte[] { 1, 2, 3, 4 });
                await Expect<InvalidDataException>(() => Capture("models/scene.gltf", new() { Files = 1 }), report);
                await File.WriteAllTextAsync(Path.Combine(root, "unsafe.gltf"), Gltf.Replace("mesh.bin", "../mesh.bin", StringComparison.Ordinal));
                await Expect<NotSupportedException>(() => Capture("unsafe.gltf"), report);
                report.Groups.Add("FailureLimitsAndNoFallback");
                var json = Encoding.UTF8.GetBytes("""{"asset":{"version":"2.0"},"buffers":[{"byteLength":4}]}""");
                var padded = (json.Length + 3) & ~3; var glb = new byte[20 + padded + 12];
                BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546c67); BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(4), 2);
                BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(8), glb.Length); BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(12), padded);
                BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4e4f534a); glb.AsSpan(20, padded).Fill(32); json.CopyTo(glb, 20);
                BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(20 + padded), 4); BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(24 + padded), 0x004e4942);
                glb.AsSpan(28 + padded).Fill(7); await File.WriteAllBytesAsync(Path.Combine(root, "embedded.glb"), glb);
                Check((await Capture("embedded.glb")).Files.Count == 1, "embedded GLB", report);
                await File.WriteAllTextAsync(Path.Combine(root, "scene.obj"), "mtllib first.mtl second.mtl\nv 0 0 0\n");
                await File.WriteAllTextAsync(Path.Combine(root, "first.mtl"), "newmtl A\nKd 1 0 0\n");
                await File.WriteAllTextAsync(Path.Combine(root, "second.mtl"), "newmtl B\nKd 0 1 0\n");
                Check((await Capture("scene.obj")).Files.Count == 3, "OBJ/MTL closure", report);
                var material = new byte[1024 * 1024]; material.AsSpan().Fill((byte)' ');
                for (var index = 4095; index < material.Length; index += 4096) material[index] = (byte)'\n';
                await File.WriteAllBytesAsync(Path.Combine(root, "repeated.mtl"), material);
                await File.WriteAllTextAsync(Path.Combine(root, "repeated.obj"), string.Concat(Enumerable.Repeat("mtllib repeated.mtl\n", 1000)));
                var before = GC.GetTotalAllocatedBytes(precise: true);
                var repeated = await Capture("repeated.obj");
                report.DuplicateMaterialAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - before;
                Check(repeated.Files.Count == 2 && report.DuplicateMaterialAllocatedBytes < 16 * 1024 * 1024, "duplicate material discovery stays bounded", report);
                report.Groups.Add("GlbAndObjDependencies");
                var cancelled = factory.Create(new(SourceId, root, "models/scene.gltf"))!; report.OwnersCreated++;
                using var stop = new CancellationTokenSource(); stop.Cancel();
                try { await Expect<OperationCanceledException>(() => cancelled.CaptureAsync(stop.Token), report); }
                finally { await cancelled.DisposeAsync(); report.OwnersDisposed++; Check(cancelled.OpenHandleCount == 0, "cancel cleanup", report); }
                Check(report.OwnersCreated == report.OwnersDisposed, "all factory products disposed", report);
                await app.StopAsync();
                report.Groups.Add("CancellationAndAppStop");
            }
            foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Check(file.CanWrite, "physical source unlocked", report);
            }
            Directory.Delete(root, recursive: true);
            report.Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetName().Name!.StartsWith("TrueMoon", StringComparison.Ordinal))
                .Select(assembly => new AssemblyProof(assembly.GetName().Name!, assembly.Location, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))))).ToArray();
            report.Completed = true; return 0;
        }
        catch (Exception error) { report.Failure = error.ToString(); Console.Error.WriteLine(error); return 1; }
        finally { await File.WriteAllTextAsync(Path.Combine(evidence, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
    }
    private static void Check(bool condition, string requirement, Report report)
    { if (!condition) throw new InvalidOperationException(requirement); report.Assertions++; }
    private static async Task Expect<T>(Func<Task> action, Report report) where T : Exception
    { try { await action(); } catch (T) { report.Assertions++; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private sealed record AssemblyProof(string Name, string Path, string Sha256);
    private sealed class Report
    {
        public bool Completed { get; set; }
        public string Provider { get; set; } = "";
        public List<string> Groups { get; } = [];
        public int Assertions { get; set; }
        public int OwnersCreated { get; set; }
        public int OwnersDisposed { get; set; }
        public string? FirstRevision { get; set; }
        public long DuplicateMaterialAllocatedBytes { get; set; }
        public AssemblyProof[] Assemblies { get; set; } = [];
        public string? Failure { get; set; }
    }
}
