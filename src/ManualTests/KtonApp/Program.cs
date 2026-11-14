using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bepu.Hosting;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;

namespace KtonAppProbe;

internal static class Program
{
    private static readonly List<object> Checks = [];
    private static readonly List<object> Owners = [];
    private static string _provider = "";
    internal static void Check(string name, bool passed, object? actual = null)
    {
        Checks.Add(new { Provider = _provider, Name = name, Passed = passed, Actual = actual });
        if (!passed) throw new InvalidOperationException(name + ": " + actual);
    }
    private static void Reject<T>(string name, Action action, Func<T, bool>? predicate = null) where T : Exception
    {
        try { action(); }
        catch (T error) { Check(name, predicate?.Invoke(error) ?? true, error.Message); return; }
        Check(name, false, "No rejection");
    }
    private static async Task RejectAsync<T>(string name, Func<Task> action, Func<T, bool>? predicate = null) where T : Exception
    {
        try { await action(); }
        catch (T error) { Check(name, predicate?.Invoke(error) ?? true, error.Message); return; }
        Check(name, false, "No rejection");
    }
    private static IApp Build(bool microsoft, Action<IAppConfigurationContext> setup)
        => App.Builder(builder => { if (microsoft) builder.UseDI(); })
            .Configuration(configuration =>
            {
                configuration.RemoveProvider<EnvironmentVariablesProvider>();
                configuration.RemoveProvider<CommandLineArgsProvider>();
            }).Setup(setup).Build();
    private static T Get<T>(IApp app) where T : class => app.Services.Resolve<T>() ?? throw new InvalidOperationException(typeof(T).FullName);
    private static void Clean(string name, BepuPhysicsHost host)
    {
        var resources = host.Resources;
        Check(name, resources.ActiveResourceOwners == 0 && resources.PublishedWorlds == 0 &&
            !resources.PendingFailedCreationCleanup && resources.MaximumPoolBytesAfterCleanup == 0, resources);
        Owners.Add(new { Provider = _provider, Name = name, host.IsReleased, Resources = resources });
    }
    internal static PhysicsBodyId Ball(IPhysicsWorld world)
        => world.CreateBody(new(world.CreateShape(new PhysicsSphere(.2f)), PhysicsBodyMotion.Dynamic,
            new(new Vector3(0, 10, 0), Quaternion.Identity)) { Mass = 1 });

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1) return 2;
        string? failure = null;
        try
        {
            foreach (var microsoft in new[] { false, true })
            {
                _provider = microsoft ? "MicrosoftDI" : "Cobalt";
                Composition(microsoft);
                await Basic(microsoft);
                await Configuration(microsoft);
                await AllConfigurationFields(microsoft);
                await SessionLifetime(microsoft, failStartup: false);
                await SessionLifetime(microsoft, failStartup: true);
                await Startup(microsoft);
                await CleanupFailures(microsoft);
                await DisposeWithoutStart(microsoft);
            }
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        var assemblies = new[] { typeof(IPhysicsWorld).Assembly, typeof(BepuPhysicsHost).Assembly, typeof(IFactory<>).Assembly,
            typeof(App).Assembly, typeof(TrueMoon.Cobalt.CobaltServiceResolver).Assembly,
            typeof(TrueMoon.Extensions.DependencyInjection.ServiceResolver).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.ServiceProvider).Assembly,
            typeof(BepuPhysics.Simulation).Assembly, typeof(BepuUtilities.Vector3Wide).Assembly }.Distinct().Select(a => new
            {
                Name = a.GetName().Name, Path = a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))),
                SharedFramework = a.Location.StartsWith(RuntimeEnvironment.GetRuntimeDirectory(), StringComparison.OrdinalIgnoreCase),
                InformationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                References = a.GetReferencedAssemblies().Select(r => r.Name).Order().ToArray()
            }).ToArray();
        File.WriteAllText(args[0], JsonSerializer.Serialize(new { Completed = failure is null, Failure = failure, Utc = DateTimeOffset.UtcNow,
            CheckCount = Checks.Count, Checks, Owners, Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Assemblies = assemblies,
            Scope = "PHYS4 App/configuration/DI/lifetime, Cobalt and Microsoft DI, real BEPU worlds. No ECS/GPU/window/native physics/platform matrix." },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Completed={failure is null}; checks={Checks.Count}");
        return failure is null ? 0 : 1;
    }

    private static void Composition(bool microsoft)
    {
        Reject<ArgumentNullException>("null-context", () => TrueMoon.Kton.Bepu.AppConfigurationExtensions.UseBepuPhysics(null!));
        Reject<PhysicsException>("invalid-defaults-before-DI", () => Build(microsoft, c => c.UseBepuPhysics(o => o.DefaultWorld = new() { Gravity = new(float.NaN, 0, 0) })),
            e => e.Error.Code == PhysicsErrorCode.InvalidArgument);
        Reject<PhysicsException>("null-defaults-before-DI", () => Build(microsoft, c => c.UseBepuPhysics(o => o.DefaultWorld = null!)));
        Reject<PhysicsException>("null-limits-before-DI", () => Build(microsoft, c => c.UseBepuPhysics(o => o.DefaultWorld = new() { Limits = null! })));
        Reject<InvalidOperationException>("duplicate-callback-registration", () => Build(microsoft, c => c.UseBepuPhysics().UseBepuPhysics()));
        Reject<InvalidOperationException>("existing-base-factory-conflict", () => Build(microsoft, c => c
            .Services(s => s.Singleton<IFactory<IPhysicsWorld>>(_ => new NeverFactory())).UseBepuPhysics()));
        Reject<InvalidOperationException>("existing-typed-factory-conflict", () => Build(microsoft, c => c
            .Services(s => s.Singleton<IFactory<IPhysicsWorld, PhysicsWorldOptions>>(_ => new NeverFactory())).UseBepuPhysics()));
        Reject<InvalidOperationException>("existing-low-level-owner-conflict", () => Build(microsoft, c => c
            .Services(s => s.Singleton<BepuPhysicsWorldFactory>()).UseBepuPhysics()));
        var profile = ConfigurationProfiles.Create(b => b.AddSchema(BepuPhysicsConfigurationSchema.Default));
        Reject<InvalidOperationException>("duplicate-configured-registration", () => Build(microsoft, c => c
            .UseConfiguration(profile).UseConfiguredBepuPhysics().UseConfiguredBepuPhysics()));
        Reject<InvalidOperationException>("mixed-configured-callback-registration", () => Build(microsoft, c => c
            .UseConfiguration(profile).UseBepuPhysics().UseConfiguredBepuPhysics()));
        Reject<ArgumentException>("missing-schema-rejected", () => Build(microsoft, c => c
            .UseConfiguration(ConfigurationProfiles.Create(_ => { })).UseConfiguredBepuPhysics()));
        var invalid = ConfigurationProfiles.Create(b =>
        {
            b.AddSchema(BepuPhysicsConfigurationSchema.Default);
            b.Override("Kton:DefaultWorld:Limits:MaxBodies", 1025);
        });
        Reject<ConfigurationException>("invalid-bound-limits-rejected", () => Build(microsoft, c => c.UseConfiguration(invalid).UseConfiguredBepuPhysics()));
    }

    private static async Task Basic(bool microsoft)
    {
        BepuPhysicsOptions? captured = null;
        var app = Build(microsoft, c => c.UseBepuPhysics(o =>
        {
            captured = o;
            o.DefaultWorld = new() { Gravity = Vector3.Zero, Limits = new() { MaxBodies = 2 } };
        }));
        var host = Get<BepuPhysicsHost>(app);
        var typed = Get<IFactory<IPhysicsWorld, PhysicsWorldOptions>>(app);
        var defaults = Get<IFactory<IPhysicsWorld>>(app);
        try
        {
            Check("same-factory-aliases", ReferenceEquals(typed, defaults));
            Check("borrowed-factory-not-disposable", typed is not IDisposable && typed is not IAsyncDisposable);
            Check("low-level-factory-hidden", app.Services.GetService(typeof(BepuPhysicsWorldFactory)) is null);
            Reject<InvalidOperationException>("create-before-start-rejected", () => defaults.Create());
            Clean("build-no-world-pool-or-worker", host);
            captured!.DefaultWorld = new() { Gravity = new(0, -100, 0), Limits = new() { MaxBodies = 1 } };
            await app.StartAsync(); await app.StartAsync();
            Clean("repeated-start-no-world-pool-or-worker", host);
            using var a = defaults.Create()!;
            var arguments = new PhysicsWorldOptions { Gravity = new(0, -9.81f, 0), Limits = new() { MaxBodies = 3 } };
            using var b = typed.Create(arguments);
            Check("detached-callback-world-defaults", a.Options.Gravity == Vector3.Zero && a.Options.Limits.MaxBodies == 2);
            Check("explicit-arguments-replace-defaults", b.Options == arguments && !ReferenceEquals(b.Options, arguments) &&
                !ReferenceEquals(b.Options.Limits, arguments.Limits));
            Check("independent-worlds-through-DI", a.Id != b.Id && host.Resources.PublishedWorlds == 2);
            var first = Ball(a); var second = Ball(b); a.Step(1); b.Step(1);
            Check("default-world-zero-gravity", a.GetBodyState(first).Pose.Position.Y == 10);
            Check("explicit-world-gravity", b.GetBodyState(second).LinearVelocity.Y < 0);
            Reject<PhysicsException>("DI-factory-foreign-generation", () => a.GetBodyState(second), e => e.Error.Code == PhysicsErrorCode.ForeignWorld);
            var before = host.Resources;
            Reject<PhysicsException>("invalid-runtime-options-no-owner", () => typed.Create(new() { Quality = (PhysicsQualityProfile)99 }), e => e.Error.Code == PhysicsErrorCode.UnsupportedProfile);
            Reject<PhysicsException>("null-runtime-options-no-owner", () => typed.Create(null!));
            Reject<ArgumentException>("base-factory-wrong-runtime-type", () => defaults.Create(42));
            Reject<ArgumentException>("base-factory-null-runtime-type", () => defaults.Create<PhysicsWorldOptions>(null));
            Check("invalid-arguments-did-not-allocate", host.Resources == before);
            using (var generic = defaults.Create(new PhysicsWorldOptions { Gravity = Vector3.Zero })!)
                Check("base-generic-explicit-arguments", generic.Options.Gravity == Vector3.Zero && generic.Options.Limits.MaxBodies == 1024);
            b.Dispose();
            Check("early-world-disposal-independent", a.State == PhysicsWorldState.Ready && host.Resources.PublishedWorlds == 1);
            await app.StopAsync(); await app.StopAsync();
            Check("stop-releases-host-only", host.IsReleased && host.Resources.PublishedWorlds == 1 && a.State == PhysicsWorldState.Ready);
            Reject<ObjectDisposedException>("create-after-stop-rejected", () => typed.Create(new()));
            Reject<InvalidOperationException>("stopped-app-cannot-restart", () => app.StartAsync().GetAwaiter().GetResult());
            await app.DisposeAsync(); await app.DisposeAsync(); app.Dispose();
            a.Step(2);
            Check("caller-world-remains-independent-after-App-dispose", a.State == PhysicsWorldState.Ready && a.LastCompletedTick == 2);
            a.Dispose(); a.Dispose(); host.Dispose(); await host.StopAsync(new CancellationToken(true));
            Clean("caller-world-and-repeated-host-cleanup", host);
            Check("each-successful-world-cleaned-once", host.Resources.CompletedCleanups == 3);
        }
        finally { await app.DisposeAsync(); }

        var left = Build(microsoft, c => c.UseBepuPhysics());
        var right = Build(microsoft, c => c.UseBepuPhysics());
        try
        {
            await left.StartAsync(); await right.StartAsync();
            var leftFactory = Get<IFactory<IPhysicsWorld>>(left); var rightFactory = Get<IFactory<IPhysicsWorld>>(right);
            using var world = rightFactory.Create()!;
            await left.DisposeAsync(); world.Step(1);
            Check("app-local-factory-and-owner-isolation", !ReferenceEquals(leftFactory, rightFactory) && world.LastCompletedTick == 1);
            world.Dispose();
            Clean("second-app-world-clean", Get<BepuPhysicsHost>(right));
        }
        finally { await left.DisposeAsync(); await right.DisposeAsync(); }
    }

    private static async Task Configuration(bool microsoft)
    {
        const string prefix = "Kton:DefaultWorld:";
        var profile = ConfigurationProfiles.Create(b =>
        {
            b.AddSchema(BepuPhysicsConfigurationSchema.Default);
            b.AddSource("file-equivalent", ConfigurationSourcePriority.Files, new ValuesSource([
                new(prefix + "Gravity:Y", JsonSerializer.SerializeToElement(-3)),
                new(prefix + "Limits:MaxBodies", JsonSerializer.SerializeToElement(4))]));
            b.AddArguments("args", ["--Kton:DefaultWorld:Gravity:Y=-5", "--Kton:DefaultWorld:Quality=FastLinear60"]);
            b.Override(prefix + "Limits:MaxBodies", 6);
        });
        BepuPhysicsOptions? captured = null;
        await using var app = Build(microsoft, c => c.UseConfiguration(profile).UseConfiguredBepuPhysics(o =>
        {
            captured = o; o.DefaultWorld = o.DefaultWorld with { Gravity = new(o.DefaultWorld.Gravity.X, -7, o.DefaultWorld.Gravity.Z) };
        }));
        var reader = Get<IConfigurationReader>(app);
        Check("common-source-priority-and-provenance", reader.Current.Read<float>(prefix + "Gravity:Y").Value == -5 &&
            reader.Current.Read<float>(prefix + "Gravity:Y").Origin!.SourceId == "args" && reader.Current.Read<int>(prefix + "Limits:MaxBodies").Value == 6);
        var copy = reader.Current.GetOptions(BepuPhysicsConfigurationSchema.Default);
        copy.DefaultWorld = new() { Gravity = Vector3.Zero };
        captured!.DefaultWorld = new() { Gravity = Vector3.Zero };
        await app.StartAsync();
        using (var world = Get<IFactory<IPhysicsWorld>>(app).Create()!)
            Check("configured-callback-wins-and-snapshot-detaches", world.Options.Gravity.Y == -7 &&
                world.Options.Quality == PhysicsQualityProfile.FastLinear60 && world.Options.Limits.MaxBodies == 6);
        await app.StopAsync();
        Clean("configured-world-clean", Get<BepuPhysicsHost>(app));
        Check("all-world-settings-declared", BepuPhysicsConfigurationSchema.Default.Keys.Count == 16);
    }

    private static async Task SessionLifetime(bool microsoft, bool failStartup)
    {
        var app = Build(microsoft, c =>
        {
            c.UseBepuPhysics();
            c.Services(s =>
            {
                s.Singleton<ProbeSession>(); // Requires real Cobalt generated constructor activation.
                s.Singleton<ProbeLifecycle>(r => new ProbeLifecycle(r.Resolve<ProbeSession>()));
                s.Singleton<IStartable>(r => r.Resolve<ProbeLifecycle>());
                s.Singleton<IStoppable>(r => r.Resolve<ProbeLifecycle>());
                if (failStartup) s.Singleton<IStartable>(_ => new FailingStartup());
            });
        });
        var host = Get<BepuPhysicsHost>(app);
        var session = Get<ProbeSession>(app);
        try
        {
            if (failStartup)
                await RejectAsync<InvalidOperationException>("later-startup-failure-propagated", () => app.StartAsync(), e => e.Message == "Later startup failure");
            else
            {
                await app.StartAsync();
                Check("injected-session-started-after-Kton", session.World is { State: PhysicsWorldState.Ready } && host.Resources.PublishedWorlds == 1);
                session.World!.Step(1);
                await app.StopAsync(new CancellationToken(true));
            }
            Check(failStartup ? "startup-rollback-reverse-order" : "session-stop-before-Kton-stop",
                session.StopCount == 1 && session.FactoryWasOpenAtStop && session.World is { State: PhysicsWorldState.Disposed } && host.IsReleased);
            Clean(failStartup ? "failed-startup-owned-session-clean" : "normal-stop-owned-session-clean", host);
            await app.DisposeAsync(); await app.DisposeAsync();
            Check("session-DI-owner-disposed-once", session.DisposeCount == 1);
        }
        finally { await app.DisposeAsync(); }
    }

    private static async Task AllConfigurationFields(bool microsoft)
    {
        var expected = new PhysicsWorldOptions
        {
            Quality = PhysicsQualityProfile.FastLinear60, Gravity = new(1, -2, 3), StepDiagnosticBudgetMilliseconds = 2.5,
            Limits = new() { MaxBodies = 7, MaxShapes = 8, MaxJoints = 9, MaxCommandsPerTick = 10, MaxContactEvents = 11,
                MaxQueryHits = 12, MaxQueryScratchBytes = 65536, MaxMeshVertices = 15, MaxMeshTriangles = 16,
                MaxMeshInputBytes = 1024, PoolDiagnosticBudgetBytes = 2 * 1024 * 1024 }
        };
        var profile = ConfigurationProfiles.Create(b =>
        {
            b.AddSchema(BepuPhysicsConfigurationSchema.Default);
            b.Override("Kton:DefaultWorld:Quality", "FastLinear60");
            b.Override("Kton:DefaultWorld:Gravity:X", 1); b.Override("Kton:DefaultWorld:Gravity:Y", -2); b.Override("Kton:DefaultWorld:Gravity:Z", 3);
            b.Override("Kton:DefaultWorld:StepDiagnosticBudgetMilliseconds", 2.5);
            b.Override("Kton:DefaultWorld:Limits:MaxBodies", 7); b.Override("Kton:DefaultWorld:Limits:MaxShapes", 8);
            b.Override("Kton:DefaultWorld:Limits:MaxJoints", 9); b.Override("Kton:DefaultWorld:Limits:MaxCommandsPerTick", 10);
            b.Override("Kton:DefaultWorld:Limits:MaxContactEvents", 11); b.Override("Kton:DefaultWorld:Limits:MaxQueryHits", 12);
            b.Override("Kton:DefaultWorld:Limits:MaxQueryScratchBytes", 65536); b.Override("Kton:DefaultWorld:Limits:MaxMeshVertices", 15);
            b.Override("Kton:DefaultWorld:Limits:MaxMeshTriangles", 16); b.Override("Kton:DefaultWorld:Limits:MaxMeshInputBytes", 1024);
            b.Override("Kton:DefaultWorld:Limits:PoolDiagnosticBudgetBytes", 2 * 1024 * 1024);
        });
        await using var app = Build(microsoft, c => c.UseConfiguration(profile).UseConfiguredBepuPhysics());
        await app.StartAsync();
        using (var world = Get<IFactory<IPhysicsWorld>>(app).Create()!)
            Check("all-sixteen-fields-bind-to-created-world", world.Options == expected, world.Options);
        await app.StopAsync(); Clean("all-fields-world-clean", Get<BepuPhysicsHost>(app));
    }

    private static async Task Startup(bool microsoft)
    {
        var app = Build(microsoft, c => c.UseBepuPhysics()); var host = Get<BepuPhysicsHost>(app);
        host.Faults.Armed = Checkpoint.HostStarting;
        await RejectAsync<InvalidOperationException>("Kton-startup-failure-propagated", () => app.StartAsync());
        Check("failed-own-startup-closes-admission", host.IsReleased);
        Reject<ObjectDisposedException>("failed-own-startup-rejects-create", () => Get<IFactory<IPhysicsWorld>>(app).Create());
        await app.DisposeAsync(); host.Dispose(); Clean("failed-own-startup-repeated-cleanup", host);

        app = Build(microsoft, c => c.UseBepuPhysics()); host = Get<BepuPhysicsHost>(app);
        await RejectAsync<OperationCanceledException>("pre-cancelled-App-start", () => app.StartAsync(new CancellationToken(true)));
        await RejectAsync<OperationCanceledException>("pre-cancelled-host-start", () => host.StartAsync(new CancellationToken(true)));
        Check("pre-cancel-start-no-owned-cleanup", !host.IsReleased && host.Resources.ActiveResourceOwners == 0);
        await app.StartAsync(); await app.DisposeAsync();
        Clean("cancelled-start-then-successful-start-dispose", host);

        BepuPhysicsHost? retained = null;
        app = Build(microsoft, c => c.UseBepuPhysics().Services(s => s.Singleton<IStartable>(r =>
            new ActionStartup(r.Resolve<BepuPhysicsHost>(), r.Resolve<IFactory<IPhysicsWorld>>(), (owner, factory) =>
            {
                retained = owner;
                FailCandidate(retained, factory);
                retained.Faults.Armed = Checkpoint.BeforePoolClear;
                throw new InvalidOperationException("Failure after unpublished candidate");
            }))));
        await RejectAsync<AggregateException>("startup-plus-rollback-cleanup-errors-preserved", () => app.StartAsync(),
            e => e.InnerExceptions.Count == 2 && e.InnerExceptions[0].Message == "Failure after unpublished candidate" && ContainsPhysicsFailure(e.InnerExceptions[1]));
        Check("failed-startup-rollback-retains-cleanup-owner", retained is { IsReleased: false } && retained.Resources.PendingFailedCreationCleanup);
        await app.DisposeAsync();
        Clean("DI-dispose-retries-startup-rollback-failure", retained!);
    }

    private static void FailCandidate(BepuPhysicsHost host, IFactory<IPhysicsWorld> factory)
    {
        host.Faults.Armed = Checkpoint.SimulationCreated;
        host.Faults.Action = () =>
        {
            host.Faults.Armed = Checkpoint.BeforePoolClear;
            throw new InvalidOperationException("Creation failure");
        };
        Reject<PhysicsException>("creation-plus-cleanup-failure-retained", () => factory.Create(), e => e.InnerException is AggregateException a && a.InnerExceptions.Count == 2);
        Check("failed-candidate-not-published", host.Resources.PendingFailedCreationCleanup && host.Resources.ActiveResourceOwners == 1 && host.Resources.PublishedWorlds == 0);
        Check("failed-candidate-owned-real-BEPU-resources", host.Resources.MaximumSampledPoolBytes > 0);
        Reject<PhysicsException>("retained-candidate-blocks-next-create", () => factory.Create(), e => e.Error.Field == "Factory.PendingCleanup");
    }

    private static async Task CleanupFailures(bool microsoft)
    {
        var app = Build(microsoft, c => c.UseBepuPhysics()); var host = Get<BepuPhysicsHost>(app);
        await app.StartAsync(); var factory = Get<IFactory<IPhysicsWorld>>(app);
        FailCandidate(host, factory);
        host.Faults.Armed = Checkpoint.BeforePoolClear;
        await RejectAsync<PhysicsException>("App-stop-retains-cleanup-failure", () => app.StopAsync());
        Check("stop-failure-is-not-released", !host.IsReleased && host.Resources.PendingFailedCreationCleanup);
        Reject<ObjectDisposedException>("stop-failure-still-closes-admission", () => factory.Create());
        await app.DisposeAsync(); await app.DisposeAsync(); host.Dispose();
        Clean("DI-dispose-retries-failed-stop", host);
        Check("failed-candidate-cleaned-once", host.Resources.CompletedCleanups == 1);

        // A stopped App will not run Stop again during Dispose. Force the sole DI owner to fail,
        // then retry on the retained public owner; the App/provider cache disposal failures.
        app = Build(microsoft, c => c.UseBepuPhysics()); host = Get<BepuPhysicsHost>(app);
        await app.StartAsync(); factory = Get<IFactory<IPhysicsWorld>>(app); FailCandidate(host, factory);
        host.Faults.Armed = Checkpoint.BeforePoolClear;
        await RejectAsync<PhysicsException>("stop-failure-before-final-dispose", () => app.StopAsync());
        host.Faults.Armed = Checkpoint.BeforePoolClear;
        await RejectAsync<Exception>("final-App-dispose-failure-visible", () => app.DisposeAsync().AsTask(), ContainsPhysicsFailure);
        Check("final-dispose-failure-retains-owner", !host.IsReleased && host.Resources.PendingFailedCreationCleanup);
        await RejectAsync<Exception>("App-dispose-repeats-recorded-failure", () => app.DisposeAsync().AsTask(), ContainsPhysicsFailure);
        host.Dispose(); host.Dispose(); await host.StopAsync();
        Clean("explicit-retained-host-retry-after-DI-failure", host);
        Check("explicit-retry-no-double-release", host.Resources.CompletedCleanups == 1);

        app = Build(microsoft, c => c.UseBepuPhysics()); host = Get<BepuPhysicsHost>(app);
        await app.StartAsync(); var world = Get<IFactory<IPhysicsWorld>>(app).Create()!;
        host.Faults.Armed = Checkpoint.BeforeDispatcherDispose;
        Reject<PhysicsException>("caller-world-cleanup-failure", world.Dispose);
        Check("caller-world-retains-disposing-state", world.State == PhysicsWorldState.Disposing && host.Resources.PublishedWorlds == 1);
        await app.StopAsync(); await app.DisposeAsync();
        Check("App-does-not-steal-failed-world-owner", world.State == PhysicsWorldState.Disposing && host.Resources.PublishedWorlds == 1);
        world.Dispose(); world.Dispose();
        Clean("caller-world-retry-after-App-dispose", host);
    }

    private static bool ContainsPhysicsFailure(Exception error) => error is PhysicsException ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(ContainsPhysicsFailure) ||
        error.InnerException is { } inner && ContainsPhysicsFailure(inner);

    private static async Task DisposeWithoutStart(bool microsoft)
    {
        var app = Build(microsoft, c => c.UseBepuPhysics()); var host = Get<BepuPhysicsHost>(app); var factory = Get<IFactory<IPhysicsWorld>>(app);
        app.Dispose(); app.Dispose(); await app.DisposeAsync();
        Check("dispose-without-start-closes-resolved-owner", host.IsReleased);
        Reject<ObjectDisposedException>("dispose-without-start-rejects-create", () => factory.Create());
        Clean("dispose-without-start-no-resources", host);
        app = Build(microsoft, c => c.UseBepuPhysics()); host = Get<BepuPhysicsHost>(app);
        await app.StopAsync(); await app.DisposeAsync();
        Check("stop-without-start-closes-owner", host.IsReleased);
        Clean("stop-without-start-no-resources", host);
    }
}

internal sealed class NeverFactory : IFactory<IPhysicsWorld, PhysicsWorldOptions>
{
    public IPhysicsWorld Create(PhysicsWorldOptions arguments) => throw new InvalidOperationException("Unused conflict factory");
}
internal sealed class ValuesSource(IReadOnlyList<ConfigurationEntry> values) : IConfigurationSource
{
    public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(values.ToArray());
    }
}
internal sealed class FailingStartup : IStartable
{
    public Task StartAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Later startup failure");
}
internal sealed class ActionStartup(BepuPhysicsHost host, IFactory<IPhysicsWorld> factory, Action<BepuPhysicsHost, IFactory<IPhysicsWorld>> start) : IStartable
{
    public Task StartAsync(CancellationToken cancellationToken = default) { start(host, factory); return Task.CompletedTask; }
}
internal sealed class ProbeLifecycle(ProbeSession session) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default) => session.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => session.StopAsync(cancellationToken);
}

// Registered by constructor activation, without a hand-written consumer resolver or service locator.
public sealed class ProbeSession(IFactory<IPhysicsWorld, PhysicsWorldOptions> factory, IFactory<IPhysicsWorld> defaults) : IStartable, IStoppable, IDisposable
{
    public IPhysicsWorld? World { get; private set; }
    public int StopCount { get; private set; }
    public int DisposeCount { get; private set; }
    public bool FactoryWasOpenAtStop { get; private set; }
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        World = factory.Create(new() { Gravity = Vector3.Zero }); Program.Ball(World);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCount++;
        using var orderProof = defaults.Create()!; FactoryWasOpenAtStop = true;
        World?.Dispose();
        return Task.CompletedTask;
    }
    public void Dispose() { DisposeCount++; World?.Dispose(); }
}
