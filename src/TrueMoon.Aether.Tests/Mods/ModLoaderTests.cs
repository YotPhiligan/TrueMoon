using System.Runtime.CompilerServices;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Mods;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Mods;

public sealed class ModLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TrueMoon-ARCH36-tests-" + Guid.NewGuid().ToString("N"));
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "ModFixture.dll");
    private static string Private => Path.Combine(AppContext.BaseDirectory, "ModPrivateDependency.dll");
    private static ModManifest Manifest(string id = "root", string entry = "Entry") => new()
    { Id = id, Version = "1.0.0", ReloadGroup = "test-group", EntryAssembly = "ModFixture.dll", EntryType = "TrueMoon.ManualTests.ModFixture." + entry };
    private string Publish(ModManifest? manifest = null, params string[] files) => ModArtifactPublisher.Publish(_root, manifest ?? Manifest(), files.Length == 0 ? [Fixture, Private] : files);
    private string Rewrite(string directory, Func<ModManifest, ModManifest> transform)
    {
        var manifest = transform(ModManifestCodec.Decode(File.ReadAllBytes(Path.Combine(directory, "manifest.json"))));
        var bytes = ModManifestCodec.Encode(manifest); var target = Path.Combine(_root, ModManifestCodec.Hash(bytes)); Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(directory, "*.dll")) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        File.WriteAllBytes(Path.Combine(target, "manifest.json"), bytes); return target;
    }
    private static IApp Build(bool di = false, List<string>? events = null, Func<string, bool>? fail = null, Action<ModLoaderOptions>? options = null) =>
        App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(s => s.Singleton<Action<string>>(_ => value => events?.Add(value)).Singleton<Func<string, bool>>(_ => fail ?? (_ => false)));
            c.UseAetherEcs(); c.UseAetherMods(options);
        }).Build();
    private static ModHost Host(IApp app) => app.Services.Resolve<ModHost>()!;
    private static SceneDocument Scene()
    {
        var type = new ComponentTypeId(new("f89ee66e-2568-4542-8797-874790d173a9"));
        var schema = new ComponentSchema(type, 1, [new ComponentPropertySchema("value", PropertyValueKind.Integer, PropertyValue.From(0L))]);
        var definition = new EntityDefinition(new(Guid.NewGuid()), new EntityPatch(addedComponents:
            [new ComponentData(new(Guid.NewGuid()), type, 1, [new("value", PropertyValue.From(7L))])]));
        return new(new(Guid.NewGuid()), 0, [new EntityInstance(new(Guid.NewGuid()), definition.Id)], [definition], [schema]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SdkSystemAndPrivateComponent_ExecuteThenReleaseAllRoots_EvenWithRetainedDisposedHandles(bool di)
    {
        var directory = Publish(); var events = new List<string>();
        var (ticket, group, session) = ExecuteAndUnload(directory, di, events);
        Assert.Equal(["root:register", "service:start", "root:start", "execute:7", "execute:8", "root:stop", "service:stop", "system:stop", "system:dispose", "service:dispose", "root:dispose"], events);
        Assert.True(group.IsClosed); Assert.True(session.IsDisposed); Assert.True(ticket.UnloadRequested);
        Collect(ticket); Assert.False(ticket.IsAlive);
        GC.KeepAlive(group); GC.KeepAlive(session);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ModUnloadTicket, ModGroup, ModWorldSession) ExecuteAndUnload(string directory, bool di, List<string> events)
    {
        var app = Build(di, events); app.StartAsync().GetAwaiter().GetResult();
        try
        {
            var result = Host(app).LoadAsync(new("root", [directory])).GetAwaiter().GetResult();
            Assert.True(result.Success, result.Diagnostic.ToString()); var group = Assert.IsType<ModGroup>(result.Group);
            Assert.Equal(["root"], group.ModuleIds); var scene = group.CreateWorld(Scene());
            Assert.Equal(1, scene.EntityCount); Assert.True(scene.Tick(TimeSpan.FromMilliseconds(20))); Assert.True(scene.Tick(TimeSpan.FromMilliseconds(20)));
            Assert.Equal(ModLoadError.None, group.UnloadAsync().GetAwaiter().GetResult().Error);
            Assert.Equal(0, Host(app).OwnedGroupCount);
            Assert.Throws<ObjectDisposedException>(() => scene.Tick(TimeSpan.FromMilliseconds(20)));
            return (group.UnloadTicket, group, scene);
        }
        finally { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    private static void Collect(ModUnloadTicket ticket)
    { for (var i = 0; ticket.IsAlive && i < 20; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(10); } }

    [Theory]
    [InlineData("api", ModLoadError.UnsupportedApi)]
    [InlineData("format", ModLoadError.UnsupportedApi)]
    [InlineData("version", ModLoadError.InvalidManifest)]
    [InlineData("id", ModLoadError.InvalidManifest)]
    [InlineData("policy", ModLoadError.InvalidManifest)]
    [InlineData("traversal", ModLoadError.InvalidManifest)]
    [InlineData("shared-hash", ModLoadError.SharedContractMismatch)]
    [InlineData("shared-missing", ModLoadError.SharedContractMismatch)]
    [InlineData("file-hash", ModLoadError.HashMismatch)]
    [InlineData("file-length", ModLoadError.HashMismatch)]
    [InlineData("duplicate-file", ModLoadError.InvalidManifest)]
    [InlineData("no-files", ModLoadError.LimitExceeded)]
    public async Task InvalidManifest_RejectsBeforeAnyModuleCallback(string mutation, ModLoadError expected)
    {
        var good = Publish(); var bad = Rewrite(good, m => mutation switch
        {
            "api" => m with { ApiVersion = 999 }, "format" => m with { FormatVersion = 999 },
            "version" => m with { Version = "01.0.0" }, "id" => m with { Id = "../root" },
            "policy" => m with { ReloadPolicy = "Independent" }, "traversal" => m with { EntryAssembly = "../ModFixture.dll" },
            "shared-hash" => m with { SharedAssemblies = [m.SharedAssemblies[0] with { Sha256 = new('0', 64) }, m.SharedAssemblies[1]] },
            "shared-missing" => m with { SharedAssemblies = [] },
            "file-hash" => m with { Files = [m.Files[0] with { Sha256 = new('0', 64) }, m.Files[1]] },
            "file-length" => m with { Files = [m.Files[0] with { Length = m.Files[0].Length - 1 }, m.Files[1]] },
            "duplicate-file" => m with { Files = [m.Files[0], m.Files[0]] },
            "no-files" => m with { Files = [] }, _ => throw new InvalidOperationException()
        });
        var events = new List<string>(); await using var app = Build(events: events);
        var result = await Host(app).LoadAsync(new("root", [bad]));
        Assert.False(result.Success); Assert.Equal(expected, result.Diagnostic.Error); Assert.Null(result.Group); Assert.Empty(events); Assert.Equal(0, Host(app).OwnedGroupCount);
    }

    [Fact]
    public async Task MissingPrivateReference_DoesNotFallBackToFixtureAlreadyAvailableInDefaultContext()
    {
        _ = System.Reflection.Assembly.LoadFrom(Private);
        var directory = Publish(null, Fixture); await using var app = Build();
        Assert.Equal(ModLoadError.MissingDependency, (await Host(app).LoadAsync(new("root", [directory]))).Diagnostic.Error);
    }
    [Theory]
    [InlineData("missing", ModLoadError.MissingDependency)]
    [InlineData("range", ModLoadError.DependencyConflict)]
    [InlineData("cycle", ModLoadError.DependencyConflict)]
    [InlineData("group", ModLoadError.DependencyConflict)]
    [InlineData("unrelated", ModLoadError.DependencyConflict)]
    public async Task DependencyClosure_IsValidatedBeforeActivation(string mutation, ModLoadError expected)
    {
        var root = Manifest() with { Dependencies = [new("dependency", "1.0.0", mutation == "range" ? "1.0.1" : "2.0.0")] };
        var dependency = Manifest("dependency", "DependencyEntry") with
        { Version = mutation == "range" ? "1.1.0" : "1.0.0", ReloadGroup = mutation == "group" ? "other" : "test-group",
            Dependencies = mutation == "cycle" ? [new("root", "1.0.0", "2.0.0")] : [] };
        if (mutation == "unrelated") root = root with { Dependencies = [] };
        var paths = mutation == "missing" ? new[] { Publish(root) } : [Publish(root), Publish(dependency)];
        var events = new List<string>(); await using var app = Build(events: events);
        Assert.Equal(expected, (await Host(app).LoadAsync(new("root", paths))).Diagnostic.Error); Assert.Empty(events);
    }
    [Fact]
    public async Task DependentGroup_StartsDependenciesFirst_AndCleansThemLast()
    {
        var dependency = Publish(Manifest("dependency", "DependencyEntry"));
        var root = Publish(Manifest() with { Dependencies = [new("dependency", "1.0.0", "2.0.0")] });
        var events = new List<string>(); await using var app = Build(events: events);
        var result = await Host(app).LoadAsync(new("root", [root, dependency])); Assert.True(result.Success, result.Diagnostic.ToString());
        Assert.Equal(["dependency", "root"], result.Group!.ModuleIds);
        Assert.Equal(ModLoadError.None, (await result.Group.UnloadAsync()).Error);
        Assert.True(events.IndexOf("dependency:start") < events.IndexOf("root:start"));
        Assert.True(events.IndexOf("root:stop") < events.IndexOf("dependency:stop"));
        Assert.True(events.IndexOf("root:dispose") < events.IndexOf("dependency:dispose"));
    }
    [Theory]
    [InlineData("startup")] [InlineData("service-start")] [InlineData("late-resolution")] [InlineData("private-resolution")]
    [InlineData("provider-resolution")] [InlineData("app-resolution")]
    public async Task FailedStartup_CleansUnpublishedOwners_AndReturnsOnlyNeutralDiagnostic(string stage)
    {
        var events = new List<string>(); await using var app = Build(events: events, fail: key => key == stage);
        var result = await Host(app).LoadAsync(new("root", [Publish()]));
        Assert.Equal(ModLoadError.ActivationFailed, result.Diagnostic.Error); Assert.Equal(0, Host(app).OwnedGroupCount);
        if (!stage.EndsWith("-resolution", StringComparison.Ordinal) || stage == "late-resolution") { Assert.Contains("root:stop", events); Assert.Contains("root:dispose", events); Assert.True(result.Group!.UnloadTicket.UnloadRequested); }
    }
    [Theory]
    [InlineData("entry-cleanup")] [InlineData("service-cleanup")] [InlineData("stop")] [InlineData("system-cleanup")]
    [InlineData("service-stop")]
    public async Task CleanupFailure_RetainsOwnerUntilExplicitRetry(string stage)
    {
        var fail = true; var events = new List<string>(); await using var app = Build(events: events, fail: key => fail && key == stage);
        var result = await Host(app).LoadAsync(new("root", [Publish()])); Assert.True(result.Success);
        var session = result.Group!.CreateWorld(Scene());
        Assert.Equal(ModLoadError.CleanupFailed, (await result.Group.UnloadAsync()).Error);
        Assert.False(result.Group.UnloadTicket.UnloadRequested); Assert.True(result.Group.UnloadTicket.IsAlive); Assert.Equal(1, Host(app).OwnedGroupCount);
        fail = false; Assert.Equal(ModLoadError.None, (await result.Group.UnloadAsync()).Error);
        Assert.True(session.IsDisposed); Assert.True(result.Group.UnloadTicket.UnloadRequested); Assert.Equal(0, Host(app).OwnedGroupCount);
        Assert.Equal(stage == "system-cleanup" ? 2 : 1, events.Count(e => e == "system:dispose"));
        Assert.Equal(stage == "entry-cleanup" ? 2 : 1, events.Count(e => e == "root:dispose"));
    }
    [Fact]
    public async Task FailedStartupAndCleanup_ReturnsRetainedOwnerWithOriginalOutcome()
    {
        var fail = true; await using var app = Build(fail: key => fail && key is "startup" or "entry-cleanup");
        var result = await Host(app).LoadAsync(new("root", [Publish()]));
        Assert.Equal(ModLoadError.CleanupFailed, result.Diagnostic.Error); Assert.Contains(nameof(ModLoadError.ActivationFailed), result.Diagnostic.Message);
        Assert.Equal(1, Host(app).OwnedGroupCount); fail = false; Assert.Equal(ModLoadError.None, (await Host(app).StopAsync()).Error);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppStop_CancelsPendingActivation_DrainsAndClosesAdmission(bool di)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = Build(di, fail: key => { if (key == "cancel-start") { started.TrySetResult(); return true; } return false; });
        await app.StartAsync(); var host = Host(app); var request = new ModLoadRequest("root", [Publish()]); var pending = host.LoadAsync(request);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10)); await app.StopAsync();
        Assert.Equal(ModLoadError.Cancelled, (await pending).Diagnostic.Error); Assert.Equal(0, host.OwnedGroupCount);
        Assert.Equal(ModLoadError.AdmissionClosed, (await host.LoadAsync(request)).Diagnostic.Error);
    }
    [Fact]
    public async Task Publisher_ReusesMatchingPublication_RejectsMutationWithoutOverwriting()
    {
        var first = Publish(); Assert.Equal(first, Publish());
        var file = Path.Combine(first, "ModFixture.dll"); File.WriteAllBytes(file, [1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => Publish()); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
        await using var app = Build(); Assert.Equal(ModLoadError.HashMismatch, (await Host(app).LoadAsync(new("root", [first]))).Diagnostic.Error);
    }
    [Fact]
    public void Registration_RequiresEcsAndRejectsDuplicateModule()
    {
        Assert.Throws<InvalidOperationException>(() => App.Build(c => c.UseAetherMods()));
        Assert.Throws<InvalidOperationException>(() => App.Build(c => { c.UseAetherEcs(); c.UseAetherMods(); c.UseAetherMods(); }));
        Assert.Throws<ArgumentOutOfRangeException>(() => App.Build(c => c.UseAetherMods(o => o.MaximumModules = 0)));
    }

    [Theory]
    [InlineData("address", ModLoadError.HashMismatch)]
    [InlineData("extra-file", ModLoadError.InvalidManifest)]
    [InlineData("entry", ModLoadError.InvalidEntry)]
    [InlineData("root", ModLoadError.MissingDependency)]
    [InlineData("budget", ModLoadError.LimitExceeded)]
    public async Task ArtifactEnvelope_RejectsInvalidInput(string mutation, ModLoadError expected)
    {
        var directory = Publish();
        if (mutation == "address") { var wrong = Path.Combine(_root, new string('a', 64)); Directory.Move(directory, wrong); directory = wrong; }
        if (mutation == "extra-file") File.WriteAllText(Path.Combine(directory, "extra.dll"), "undeclared");
        if (mutation == "entry") directory = Rewrite(directory, m => m with { EntryType = "Missing.Entry" });
        await using var app = Build(options: mutation == "budget" ? o => o.MaximumArtifactBytes = 1024 : null);
        var result = await Host(app).LoadAsync(new(mutation == "root" ? "missing" : "root", [directory]));
        Assert.Equal(expected, result.Diagnostic.Error); Assert.Equal(0, Host(app).OwnedGroupCount);
    }
    [Theory]
    [InlineData("startup")] [InlineData("system-create")] [InlineData("execute")]
    [InlineData("private-resolution")] [InlineData("provider-resolution")] [InlineData("app-resolution")]
    public void Failures_DoNotExportExceptionRoots_AndStillCollect(string failure)
    {
        var ticket = FailAndUnload(Publish(), failure); Assert.True(ticket.UnloadRequested); Collect(ticket); Assert.False(ticket.IsAlive);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ModUnloadTicket FailAndUnload(string directory, string failure)
    {
        var app = Build(fail: key => key == failure);
        try
        {
            var result = Host(app).LoadAsync(new("root", [directory])).GetAwaiter().GetResult();
            if (failure == "startup" || failure.EndsWith("-resolution", StringComparison.Ordinal)) Assert.Equal(ModLoadError.ActivationFailed, result.Diagnostic.Error);
            else
            {
                Assert.True(result.Success);
                var error = Assert.Throws<ModOperationException>(() =>
                {
                    var session = result.Group!.CreateWorld(Scene()); session.Tick(TimeSpan.FromMilliseconds(20));
                });
                Assert.Equal(ModLoadError.ActivationFailed, error.Diagnostic.Error); Assert.Null(error.InnerException);
                Assert.Equal(ModLoadError.None, result.Group!.UnloadAsync().GetAwaiter().GetResult().Error);
            }
            return result.Group!.UnloadTicket;
        }
        finally { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    [Fact]
    public async Task Unload_WaitsForActiveTick_AndRejectsNewWork()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        await using var app = Build(fail: key => { if (key == "execute") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); } return false; });
        var result = await Host(app).LoadAsync(new("root", [Publish()])); Assert.True(result.Success);
        var session = result.Group!.CreateWorld(Scene()); var tick = Task.Run(() => session.Tick(TimeSpan.FromMilliseconds(20)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); var unload = result.Group.UnloadAsync();
            Assert.True(result.Group.IsClosed); Assert.False(unload.IsCompleted);
            Assert.Equal(ModLoadError.AdmissionClosed, Assert.Throws<ModOperationException>(() => result.Group.CreateWorld(Scene())).Diagnostic.Error);
            release.Set(); Assert.True(await tick); Assert.Equal(ModLoadError.None, (await unload).Error); Assert.True(session.IsDisposed);
        }
        finally { release.Set(); await tick; }
    }
    [Theory]
    [InlineData("{\"formatVersion\":1,\"apiVersion\":1,\"apiVersion\":2}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData("null")]
    public void Codec_RejectsAmbiguousOrImplicitProtocolVersions(string json)
        => Assert.Throws<InvalidDataException>(() => ModManifestCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json)));

    [Fact]
    public async Task NonManagedFile_RejectsBeforeEntryActivation()
    {
        var good = Publish(); byte[] bytes = [0, 1, 2, 3]; File.WriteAllBytes(Path.Combine(good, "ModFixture.dll"), bytes);
        var bad = Rewrite(good, m => m with { Files = m.Files.Select(f => f.Name == "ModFixture.dll" ?
            f with { Length = bytes.Length, Sha256 = ModManifestCodec.Hash(bytes) } : f).ToArray() });
        var events = new List<string>(); await using var app = Build(events: events);
        Assert.Equal(ModLoadError.InvalidManifest, (await Host(app).LoadAsync(new("root", [bad]))).Diagnostic.Error); Assert.Empty(events);
    }
    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        if (!resolved.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
