using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using ModelImportResearch;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.ModelImportConsumer;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        if (args.Length != 5 || args[0] is not ("cobalt" or "microsoft")) return 2;
        var evidence = Path.GetFullPath(args[4]); if (Directory.Exists(evidence)) throw new IOException("Use fresh evidence."); Directory.CreateDirectory(evidence);
        var report = new Report { Provider = args[0] }; var sources = Path.Combine(evidence, "sources"); var scratch = Path.Combine(evidence, "scratch");
        Fixtures.Create(sources);
        try
        {
            await using var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); })
                .Setup(context => { context.UseAetherModelSourceCapture(); context.UseAetherModelImport(); }).Build();
            await app.StartAsync();
            var capture = app.Services.Resolve<IFactory<ModelSourceCapture,ModelSourceCaptureArguments>>()!;
            var imports = app.Services.Resolve<IFactory<ModelImportOperation,ModelImportArguments>>()!; var host = app.Services.Resolve<ModelImportHost>()!;
            async Task<CapturedModelSource> Capture(string name)
            {
                await using var owner = capture.Create(new(Guid.Parse("16f36df4-5e35-450a-a6ad-7a306f718ba0"), sources, name))!; return await owner.CaptureAsync();
            }
            var gltf = await Capture("scene.gltf"); var glb = await Capture("scene.glb"); var grid = await Capture("grid.glb");
            var worker = Deployment(args[1], args[3]); report.WorkerFiles = worker.Sha256;
            ModelImportOperation Create(CapturedModelSource source, ModelImportWorkerDeployment? deployment = null, double seconds = 10) => imports.Create(new(source, deployment ?? worker, scratch, TimeSpan.FromSeconds(seconds)))!;
            async Task<ModelImportCandidate> Import(CapturedModelSource source)
            {
                await using var owner = Create(source); var candidate = await owner.ImportAsync(); Reaped(owner, host, report); return candidate;
            }
            await using (var idle = Create(gltf)) Check(idle.WorkerProcessId == 0 && host.ActiveOperationCount == 0 && !Directory.Exists(scratch), "IO-free factory", report);
            File.Delete(Path.Combine(sources, "scene.bin")); File.Delete(Path.Combine(sources, "scene.gltf"));
            var result = await Import(gltf); VerifyScene(result, report);
            Check((await Import(gltf)).Revision == result.Revision, "repeat CPU revision", report);
            var binary = await Import(glb); VerifyScene(binary, report);
            Check(binary.InputRevision != result.InputRevision, "format input revision", report);
            var gridMesh = (await Import(grid)).Meshes.Single().Geometry;
            Check(gridMesh.Vertices.Length == 16384 && gridMesh.Indices.Length == 96774 && gridMesh.Bounds.Maximum == new Vector3(127,127,0), "grid geometry", report);
            report.Revision = result.Revision; report.Groups.Add("CapturedBytesGeometryMaterialsHierarchyAndRepeat");

            Fixtures.Create(sources); var json = File.ReadAllText(Path.Combine(sources, "scene.gltf"));
            File.WriteAllText(Path.Combine(sources, "linear.gltf"), json.Replace("[1,0,0,1]", "[0.25,0.5,0.75,1]", StringComparison.Ordinal));
            var colored = await Import(await Capture("linear.gltf")); var color = colored.Materials[colored.Meshes[0].MaterialIndex].BaseColorSrgb;
            Check(Vector3.Distance(color, new(.5370987f,.735357f,.880825f)) < .00001f, "linear RGB to sRGB", report);
            foreach (var (name, replacement) in new[]
            {
                ("alpha.gltf", json.Replace("\"name\":\"Red\"", "\"name\":\"Red\",\"alphaMode\":\"BLEND\"", StringComparison.Ordinal)),
                ("morph.gltf", json.Replace("\"primitives\":", "\"weights\":[0],\"primitives\":", StringComparison.Ordinal)),
                ("lines.gltf", json.Replace("\"mode\":4", "\"mode\":1", StringComparison.Ordinal)),
                ("skins.gltf", json.Replace("\"scene\":0", "\"skins\":[{}],\"scene\":0", StringComparison.Ordinal)),
                ("animation.gltf", json.Replace("\"scene\":0", "\"animations\":[{}],\"scene\":0", StringComparison.Ordinal)),
                ("texture.gltf", json.Replace("\"metallicFactor\":0", "\"baseColorTexture\":{\"index\":0},\"metallicFactor\":0", StringComparison.Ordinal)),
                ("attribute.gltf", json.Replace("\"POSITION\":0", "\"COLOR_0\":0,\"POSITION\":0", StringComparison.Ordinal)),
                ("bad-index.gltf", json.Replace("\"POSITION\":0", "\"POSITION\":9999", StringComparison.Ordinal))
            })
            {
                File.WriteAllText(Path.Combine(sources,name), replacement); var input = await Capture(name);
                await using var rejected = Create(input); await Expect<InvalidDataException>(() => rejected.ImportAsync(), report); Reaped(rejected, host, report);
            }
            report.Groups.Add("UnsupportedPolicyAndInvalidNativeInput");
            using (var process = Process.GetCurrentProcess()) Check(!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "AssimpNetter") &&
                !process.Modules.Cast<ProcessModule>().Any(module => Path.GetFileName(module.FileName).Equals("assimp.dll", StringComparison.OrdinalIgnoreCase)), "no Assimp in parent", report);

            async Task<ModelImportWorkerDeployment> Fake(string mode)
            {
                await File.WriteAllTextAsync(Path.Combine(args[2], "mode.txt"), mode); return Deployment(args[2], args[3]);
            }
            foreach (var mode in new[] { "crash", "malformed", "flood" })
            {
                await using var failed = Create(gltf, await Fake(mode));
                await Expect<InvalidDataException>(() => failed.ImportAsync(), report); Reaped(failed, host, report);
            }
            await using (var memory = Create(gltf, await Fake("memory")))
            {
                try { await memory.ImportAsync(); throw new InvalidDataException("Expected memory proof exit."); }
                catch (InvalidDataException error) { Check(error.Message.Contains("exit 42", StringComparison.Ordinal), "Job allows 1MiB and denies 512MiB commit", report); }
                Reaped(memory, host, report);
            }
            await using (var timed = Create(gltf, await Fake("block"), 2))
            {
                var task = timed.ImportAsync(); await Started(scratch, task); await Expect<TimeoutException>(() => task, report); Reaped(timed, host, report);
            }
            await using (var cancelled = Create(gltf, await Fake("block")))
            {
                using var cancellation = new CancellationTokenSource(); var task = cancelled.ImportAsync(cancellation.Token); await Started(scratch, task);
                await using var second = Create(gltf); await Expect<InvalidOperationException>(() => second.ImportAsync(), report);
                cancellation.Cancel(); await Expect<OperationCanceledException>(() => task, report); Reaped(cancelled, host, report);
            }
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); await using var pre = Create(gltf); await Expect<OperationCanceledException>(() => pre.ImportAsync(cancellation.Token), report);
                Check(pre.WorkerProcessId == 0, "pre-cancel never starts worker", report);
            }
            report.Groups.Add("CrashPacketQuotaDeadlineCancellationAndAdmission");
            var descriptor = await Fake("block"); File.AppendAllText(Path.Combine(args[2], "mode.txt"), "changed");
            await using (var changed = Create(gltf, descriptor))
            {
                await Expect<InvalidDataException>(() => changed.ImportAsync(), report); Check(changed.WorkerProcessId == 0 && host.ActiveOperationCount == 0, "manifest mismatch before start", report);
            }
            var retry = Create(gltf, await Fake("block"));
            using (var cancellation = new CancellationTokenSource())
            {
                var task = retry.ImportAsync(cancellation.Token); var directory = await Started(scratch, task);
                using (var locked = new FileStream(Path.Combine(directory, "locked"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    cancellation.Cancel(); await Expect<IOException>(() => task, report);
                    Check(retry.WorkerReaped && host.ActiveOperationCount == 1 && Directory.Exists(directory), "cleanup failure retains admission", report);
                }
                await retry.DisposeAsync(); Check(host.ActiveOperationCount == 0 && !Directory.Exists(directory), "cleanup retry", report); Reaped(retry, host, report);
            }
            report.Groups.Add("ManifestPinAndCleanupRetry");
            await using (var stopped = Create(gltf, await Fake("block")))
            {
                var task = stopped.ImportAsync(); await Started(scratch, task); await app.StopAsync();
                await Expect<OperationCanceledException>(() => task, report); Reaped(stopped, host, report);
                await using var late = Create(gltf); await Expect<ObjectDisposedException>(() => late.ImportAsync(), report);
            }
            Check(!Directory.EnumerateFileSystemEntries(scratch).Any(), "scratch children removed", report); report.Groups.Add("AppStopReapsAndClosesAdmission");
            report.LoadedFramework = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetName().Name!.StartsWith("TrueMoon", StringComparison.Ordinal))
                .ToDictionary(assembly => assembly.GetName().Name!, assembly => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))));
            report.Completed = true;
        }
        catch (Exception error) { report.Failure = error.ToString(); Console.Error.WriteLine(error); }
        finally { await File.WriteAllTextAsync(Path.Combine(evidence, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
        return report.Completed ? 0 : 1;
    }
    private static ModelImportWorkerDeployment Deployment(string directory, string dotnet) => new(directory, dotnet,
        Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(file => Path.GetRelativePath(directory,file).Replace('\\','/'), file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))));
    private static void VerifyScene(ModelImportCandidate candidate, Report report)
    {
        Check(candidate.Meshes.Count == 2 && candidate.Instances.Count == 4, "shared mesh instances", report);
        foreach (var mesh in candidate.Meshes)
        {
            Check(mesh.Geometry.Indices.SequenceEqual(new uint[] {0,1,2}), "literal topology", report);
            Check(Vector3.Cross(mesh.Geometry.Vertices[1] - mesh.Geometry.Vertices[0], mesh.Geometry.Vertices[2] - mesh.Geometry.Vertices[0]).Z == 1, "right-handed winding", report);
        }
        var origins = candidate.Instances.Select(instance => Vector3.Transform(Vector3.Zero,instance.World)).Distinct().ToArray();
        Check(origins.Contains(new(10,2,0)) && origins.Contains(new(13,0,0)) && origins.Length == 2, "parent child matrix order", report);
        Check(candidate.Materials[candidate.Meshes[0].MaterialIndex].BaseColorSrgb == Vector3.UnitX && candidate.Materials[candidate.Meshes[1].MaterialIndex].BaseColorSrgb == Vector3.UnitY, "material links", report);
    }
    private static void Reaped(ModelImportOperation operation, ModelImportHost host, Report report)
    {
        Check(operation.WorkerProcessId > 0 && operation.WorkerReaped && host.ActiveOperationCount == 0, "reaped and released admission", report);
        try { using var process = Process.GetProcessById(operation.WorkerProcessId); Check(process.HasExited, "OS child exited", report); }
        catch (ArgumentException) { Check(true, "OS child absent", report); }
        report.ReapedWorkers++;
    }
    private static async Task<string> Started(string scratch, Task task)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (Directory.Exists(scratch)) foreach (var directory in Directory.EnumerateDirectories(scratch)) if (File.Exists(Path.Combine(directory,"started"))) return directory;
            if (task.IsCompleted) { await task; throw new InvalidDataException("Missing blocked marker."); }
            await Task.Delay(10);
        }
        throw new TimeoutException("Worker start marker missing.");
    }
    private static async Task Expect<T>(Func<Task> action, Report report) where T : Exception
    {
        try { await action(); } catch (T) { report.Assertions++; return; } throw new InvalidDataException("Expected " + typeof(T).Name);
    }
    private static void Check(bool condition, string name, Report report) { if (!condition) throw new InvalidDataException(name); report.Assertions++; }
    private sealed class Report
    {
        public bool Completed {get;set;} public string? Failure {get;set;} public string Provider {get;set;} = "";
        public int Assertions {get;set;} public int ReapedWorkers {get;set;} public string? Revision {get;set;}
        public List<string> Groups {get;} = []; public IReadOnlyDictionary<string,string>? WorkerFiles {get;set;} public Dictionary<string,string>? LoadedFramework {get;set;}
    }
}
