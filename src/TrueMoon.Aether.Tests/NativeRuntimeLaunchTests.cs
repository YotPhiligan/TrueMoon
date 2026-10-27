using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;
using Xunit;

namespace TrueMoon.Aether.Tests;

/// <summary>Checks explicit native commands, exact counterpart identities and lazy App integration without starting children.</summary>
public sealed class NativeRuntimeLaunchTests
{
    private static readonly string Hash = new('A', 64);
    private static string Entry => Assembly.GetEntryAssembly()!.Location;
    private static string Executable => Environment.ProcessPath!;
    private static bool IsDotnet => string.Equals(Path.GetFileNameWithoutExtension(Executable), "dotnet", StringComparison.OrdinalIgnoreCase);
    private static string[] Shared =>
    [
        typeof(SessionIdentity).Assembly.Location,
        typeof(IFactory<>).Assembly.Location,
        typeof(PipesInvocationClient<>).Assembly.Location,
        typeof(ChildProcessUnitHandle).Assembly.Location
    ];

    [Fact]
    public void Constructor_NormalizesPaths_AndDefaultsWorkingDirectoryToRuntimeDirectory()
    {
        var executable = Path.Combine(Path.GetDirectoryName(Executable)!, "nested", "..", Path.GetFileName(Executable));
        var runtime = Path.Combine(Path.GetDirectoryName(Entry)!, "nested", "..", Path.GetFileName(Entry));

        var descriptor = new NativeRuntimeLaunch(executable, runtime);

        Assert.Equal(Path.GetFullPath(executable), descriptor.ExecutablePath);
        Assert.Equal(Path.GetFullPath(runtime), descriptor.RuntimeAssemblyPath);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(runtime)), descriptor.WorkingDirectory);
        Assert.Empty(descriptor.Arguments);
    }

    [Fact]
    public void Constructor_NormalizesExplicitWorkingDirectory_WithoutCheckingFiles()
    {
        using var copies = new RuntimeCopies();
        var executable = Path.Combine(copies.Root, "missing.exe");
        var runtime = Path.Combine(copies.Root, "missing.dll");
        var working = Path.Combine(copies.Root, "nested", "..", "work");

        var descriptor = new NativeRuntimeLaunch(executable, runtime, workingDirectory: working);

        Assert.Equal(Path.GetFullPath(working), descriptor.WorkingDirectory);
        Assert.False(File.Exists(executable));
        Assert.False(File.Exists(runtime));
        Assert.False(Directory.Exists(descriptor.WorkingDirectory));
    }

    [Theory]
    [InlineData("executablePath", null)]
    [InlineData("runtimeAssemblyPath", null)]
    [InlineData("executablePath", "")]
    [InlineData("runtimeAssemblyPath", "")]
    [InlineData("workingDirectory", "")]
    [InlineData("executablePath", " ")]
    [InlineData("runtimeAssemblyPath", " ")]
    [InlineData("workingDirectory", " ")]
    [InlineData("executablePath", "relative")]
    [InlineData("runtimeAssemblyPath", "relative")]
    [InlineData("workingDirectory", "relative")]
    [InlineData("executablePath", "nul")]
    [InlineData("runtimeAssemblyPath", "nul")]
    [InlineData("workingDirectory", "nul")]
    public void Constructor_RejectsInvalidPaths(string parameter, string? value)
    {
        if (value == "nul") value = Path.Combine(Path.GetTempPath(), "invalid\0path");
        var error = Assert.ThrowsAny<ArgumentException>(() => new NativeRuntimeLaunch(
            parameter == "executablePath" ? value! : Executable,
            parameter == "runtimeAssemblyPath" ? value! : Entry,
            workingDirectory: parameter == "workingDirectory" ? value : null));
        Assert.Equal(parameter, error.ParamName);
    }

    [Fact]
    public void Arguments_AreCopiedReadOnly_AndPassedIndividuallyWithoutImplicitRuntimeSelector()
    {
        string[] expected = ["--profile", "two words", "\"quoted\"", "", "path\\with spaces", "'literal'"];
        var arguments = expected.ToList();
        var descriptor = new NativeRuntimeLaunch(Executable, Entry, arguments);
        arguments[0] = "changed";
        arguments.Add("added");

        Assert.Equal(expected, descriptor.Arguments);
        var readOnly = Assert.IsAssignableFrom<IList<string>>(descriptor.Arguments);
        Assert.True(readOnly.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => readOnly[0] = "changed");

        var info = descriptor.StartInfo();
        Assert.Equal(descriptor.ExecutablePath, info.FileName);
        Assert.Equal(descriptor.WorkingDirectory, info.WorkingDirectory);
        Assert.False(info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
        Assert.True(info.RedirectStandardInput);
        Assert.True(info.RedirectStandardOutput);
        Assert.True(info.RedirectStandardError);
        Assert.Empty(info.Arguments);
        Assert.Equal(expected, info.ArgumentList);
        Assert.DoesNotContain("--runtime", info.ArgumentList);

        info.ArgumentList[0] = "changed start info";
        var next = descriptor.StartInfo();
        Assert.NotSame(info, next);
        Assert.Equal(expected, next.ArgumentList);
        Assert.Equal(expected, descriptor.Arguments);
    }

    [Fact]
    public void Arguments_NullEnumerableMeansNoArguments_AndNoAutoRuntimeSelector()
    {
        var descriptor = new NativeRuntimeLaunch(Executable, Entry, arguments: null);
        Assert.Empty(descriptor.Arguments);
        Assert.Empty(descriptor.StartInfo().ArgumentList);
    }

    [Fact]
    public void Arguments_EnumerateInputOnlyOnce()
    {
        var enumerations = 0;
        IEnumerable<string> Arguments()
        {
            enumerations++;
            yield return "first";
            yield return "second";
        }
        var descriptor = new NativeRuntimeLaunch(Executable, Entry, Arguments());

        Assert.Equal(new[] { "first", "second" }, descriptor.Arguments);
        Assert.Equal(descriptor.Arguments, descriptor.StartInfo().ArgumentList);
        Assert.Equal(descriptor.Arguments, descriptor.StartInfo().ArgumentList);
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void Arguments_Accept64ValuesAnd16384TotalCharacters()
    {
        var arguments = Enumerable.Repeat("", 64).ToArray();
        arguments[^1] = new string('x', 16384);

        var descriptor = new NativeRuntimeLaunch(Executable, Entry, arguments);

        Assert.Equal(64, descriptor.Arguments.Count);
        Assert.Equal(16384, descriptor.Arguments.Sum(value => value.Length));
        Assert.Equal(arguments, descriptor.StartInfo().ArgumentList);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("single-length")]
    [InlineData("total-length")]
    [InlineData("null-value")]
    [InlineData("nul")]
    public void Arguments_RejectInvalidValuesOrBounds(string invalid)
    {
        string[] arguments = invalid switch
        {
            "count" => Enumerable.Repeat("", 65).ToArray(),
            "single-length" => [new string('x', 16385)],
            "total-length" => [new string('x', 8192), new string('y', 8193)],
            "null-value" => [null!],
            "nul" => ["invalid\0argument"],
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var error = Assert.Throws<ArgumentException>(() => new NativeRuntimeLaunch(Executable, Entry, arguments));
        Assert.Equal("arguments", error.ParamName);
    }

    [Fact]
    public void CurrentProcess_SelectsActualEntryAndExecutable_AndOnlyPrependsEntryForDotnet()
    {
        var descriptor = NativeRuntimeLaunch.CurrentProcess();

        Assert.Equal(Path.GetFullPath(Executable), descriptor.ExecutablePath);
        Assert.Equal(Path.GetFullPath(Entry), descriptor.RuntimeAssemblyPath);
        Assert.Equal(Path.GetFullPath(AppContext.BaseDirectory), descriptor.WorkingDirectory);
        Assert.Equal(IsDotnet ? new[] { Entry } : Array.Empty<string>(), descriptor.Arguments);
        Assert.DoesNotContain("--runtime", descriptor.Arguments);
    }

    [Fact]
    public void CurrentProcess_CopiesExplicitRuntimeArguments_WithoutAddingAnotherSelector()
    {
        string[] arguments = ["--runtime", "two words"];
        var expected = IsDotnet ? new[] { Entry }.Concat(arguments).ToArray() : arguments.ToArray();
        var descriptor = NativeRuntimeLaunch.CurrentProcess(arguments);
        arguments[0] = "changed";

        Assert.Equal(expected, descriptor.Arguments);
        Assert.Equal(expected, descriptor.StartInfo().ArgumentList);
        Assert.Single(descriptor.Arguments, value => value == "--runtime");
    }

    [Fact]
    public void NullInputs_AreRejectedAtPublicFactoryAndLeaseBoundaries()
    {
        Assert.Equal("arguments", Assert.Throws<ArgumentNullException>(() => NativeRuntimeLaunch.CurrentProcess(null!)).ParamName);
        Assert.Equal("runtime", Assert.Throws<ArgumentNullException>(() => new NativeRuntimeFactory((NativeRuntimeLaunch)null!)).ParamName);
        Assert.Equal("clients", Assert.Throws<ArgumentNullException>(() =>
            new NativeRuntimeFactory((IFactory<PipesInvocationClient<IEngineControl>, string>)null!)).ParamName);
        Assert.Equal("launch", Assert.Throws<ArgumentNullException>(() => new NativeRuntimeLease(null!, _ => { })).ParamName);
        Assert.Equal("observe", Assert.Throws<ArgumentNullException>(() => new NativeRuntimeLease(Launch(Path.GetTempPath()), null!)).ParamName);
        Assert.Equal("launch", Assert.Throws<ArgumentNullException>(() => BuildIdentity.ValidateRuntime(null!)).ParamName);
    }

    [Fact]
    public void SameExecutablePathComparison_UsesNormalizedPlatformPathRules()
    {
        Assert.True(BuildIdentity.SamePath(Entry, Path.Combine(Path.GetDirectoryName(Entry)!, "nested", "..", Path.GetFileName(Entry))));
        Assert.Equal(OperatingSystem.IsWindows(), BuildIdentity.SamePath(Entry.ToUpperInvariant(), Entry.ToLowerInvariant()));
    }

    [Fact]
    public void ForRuntime_SameExecutablePreservesHostIdAndEntrySharedManifest()
    {
        var expected = Manifest([Entry, .. Shared]);

        Assert.Equal(expected, BuildIdentity.HostId);
        Assert.Equal(expected, BuildIdentity.ForRuntime(Entry));
        var identity = BuildIdentity.CurrentLaunchIdentity(Entry);
        Assert.Equal(Path.GetFullPath(Entry), identity.HostAssemblyPath);
        Assert.Equal(Path.GetFullPath(Entry), identity.RuntimeAssemblyPath);
        Assert.Equal(1, identity.Version);
        BuildIdentity.ValidateRuntime(Launch(Path.GetTempPath(), expected, identity));
    }

    [Fact]
    public void CurrentLaunchIdentity_NormalizesPaths_WithoutOpeningRuntimeFiles()
    {
        using var copies = new RuntimeCopies();
        var runtime = Path.Combine(copies.Root, "nested", "..", "missing.dll");

        var identity = BuildIdentity.CurrentLaunchIdentity(runtime);

        Assert.Equal(Path.GetFullPath(Entry), identity.HostAssemblyPath);
        Assert.Equal(Path.GetFullPath(runtime), identity.RuntimeAssemblyPath);
        Assert.Equal(1, identity.Version);
        Assert.False(File.Exists(identity.RuntimeAssemblyPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative.dll")]
    [InlineData("nul")]
    public void ForRuntimeAndCurrentLaunchIdentity_RejectInvalidPaths(string? path)
    {
        if (path == "nul") path = Path.Combine(Path.GetTempPath(), "invalid\0runtime.dll");
        Assert.Throws<InvalidDataException>(() => BuildIdentity.CurrentLaunchIdentity(path!));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.ForRuntime(path!));
    }

    [Fact]
    public void ForRuntime_SeparatePairIsContentStableAfterMovingAndRenamingDeployment()
    {
        using var copies = new RuntimeCopies();
        var runtime = copies.Copy("before");
        var expected = Pair(Entry, runtime);
        var fingerprint = BuildIdentity.ForRuntime(runtime);
        Assert.Equal(Shared.Length + 1, Directory.EnumerateFiles(Path.GetDirectoryName(runtime)!).Count());
        Assert.Equal(expected, fingerprint);
        Assert.NotEqual(BuildIdentity.HostId, fingerprint);

        var movedDirectory = Path.Combine(copies.Root, "after");
        Directory.Move(Path.GetDirectoryName(runtime)!, movedDirectory);
        var movedEntry = Path.Combine(movedDirectory, Path.GetFileName(runtime));
        var renamedEntry = Path.Combine(movedDirectory, "Renamed.Runtime.dll");
        File.Move(movedEntry, renamedEntry);

        Assert.Equal(fingerprint, BuildIdentity.ForRuntime(renamedEntry));
        Assert.Equal(expected, Pair(Entry, renamedEntry));
        Assert.False(File.Exists(runtime));
    }

    [Fact]
    public void ForRuntime_EntryContentChangesFingerprint_WithoutModifyingExistingBinary()
    {
        var originalEntryHash = FileHash(Entry);
        using (var copies = new RuntimeCopies())
        {
            var runtime = copies.Copy("runtime");
            var before = BuildIdentity.ForRuntime(runtime);
            copies.Corrupt(runtime);

            var after = BuildIdentity.ForRuntime(runtime);

            Assert.NotEqual(before, after);
            Assert.Equal(Pair(Entry, runtime), after);
        }
        Assert.Equal(originalEntryHash, FileHash(Entry));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ForRuntime_RejectsEachTamperedSharedCounterpart(int sharedIndex)
    {
        using var copies = new RuntimeCopies();
        var runtime = copies.Copy("runtime");
        _ = BuildIdentity.ForRuntime(runtime);
        var originalSharedHash = FileHash(Shared[sharedIndex]);
        copies.Corrupt(Path.Combine(Path.GetDirectoryName(runtime)!, Path.GetFileName(Shared[sharedIndex])));

        var error = Assert.Throws<InvalidDataException>(() => BuildIdentity.ForRuntime(runtime));

        Assert.Contains("shared", error.Message);
        Assert.Equal(originalSharedHash, FileHash(Shared[sharedIndex]));
    }

    [Fact]
    public void ValidateRuntime_AcceptsSeparateHostContent_AndRejectsReversedEntryRoles()
    {
        using var copies = new RuntimeCopies();
        var host = copies.Copy("host");
        copies.Corrupt(host);
        var identity = new RuntimeLaunchIdentity(host, Entry);
        var expected = Pair(host, Entry);
        var launch = Launch(copies.Root, expected, identity);

        BuildIdentity.ValidateRuntime(launch);

        var reversed = BuildIdentity.ForRuntime(host);
        Assert.Equal(Pair(Entry, host), reversed);
        Assert.NotEqual(expected, reversed);
        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(launch with
        {
            Identity = launch.Identity with { HostContractId = reversed }
        }));
    }

    [Fact]
    public void ValidateRuntime_RejectsTamperedHostEntry()
    {
        using var copies = new RuntimeCopies();
        var host = copies.Copy("host");
        // The initial copies have identical entry bytes, so reversing their roles preserves the pair hash.
        var launch = Launch(copies.Root, BuildIdentity.ForRuntime(host), new(host, Entry));
        BuildIdentity.ValidateRuntime(launch);
        copies.Corrupt(host);

        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(launch));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ValidateRuntime_RejectsEachTamperedHostSharedCounterpart(int sharedIndex)
    {
        using var copies = new RuntimeCopies();
        var host = copies.Copy("host");
        var launch = Launch(copies.Root, BuildIdentity.ForRuntime(host), new(host, Entry));
        BuildIdentity.ValidateRuntime(launch);
        copies.Corrupt(Path.Combine(Path.GetDirectoryName(host)!, Path.GetFileName(Shared[sharedIndex])));

        var error = Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(launch));
        Assert.Contains("shared", error.Message);
    }

    [Fact]
    public void ValidateRuntime_RejectsWrongOwnEntry_EvenWithMatchingPairFingerprint()
    {
        using var copies = new RuntimeCopies();
        var runtime = copies.Copy("runtime");
        var launch = Launch(copies.Root, BuildIdentity.ForRuntime(runtime), BuildIdentity.CurrentLaunchIdentity(runtime));

        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(launch));
    }

    [Fact]
    public void ValidateRuntime_RejectsMissingIdentityAndChangedHostFingerprint()
    {
        var launch = Launch(Path.GetTempPath(), BuildIdentity.HostId, BuildIdentity.CurrentLaunchIdentity(Entry));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(launch with { RuntimeIdentity = null }));
        var changedHost = launch.Identity.HostContractId == Hash ? new string('B', 64) : Hash;
        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(launch with
        {
            Identity = launch.Identity with { HostContractId = changedHost }
        }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void ValidateRuntime_RejectsUnknownIdentityVersion(int version)
    {
        var identity = BuildIdentity.CurrentLaunchIdentity(Entry) with { Version = version };
        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(Launch(Path.GetTempPath(), BuildIdentity.HostId, identity)));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "")]
    [InlineData(false, "")]
    [InlineData(true, "relative.dll")]
    [InlineData(false, "relative.dll")]
    [InlineData(true, "nul")]
    [InlineData(false, "nul")]
    public void ValidateRuntime_RejectsInvalidCounterpartPaths(bool hostPath, string? path)
    {
        if (path == "nul") path = Path.Combine(Path.GetTempPath(), "invalid\0entry.dll");
        var identity = BuildIdentity.CurrentLaunchIdentity(Entry);
        identity = hostPath ? identity with { HostAssemblyPath = path! } : identity with { RuntimeAssemblyPath = path! };
        Assert.Throws<InvalidDataException>(() => BuildIdentity.ValidateRuntime(Launch(Path.GetTempPath(), BuildIdentity.HostId, identity)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultNativeWithoutDescriptor_IsLazyAndFailsClearly_WithoutStartingChild(bool di)
    {
        using var copies = new RuntimeCopies();
        await using var app = Builder(di).Setup(context =>
        {
            AetherOptions options = null!;
            context.UseAether(value => { Configure(value, copies.Root); options = value; });
            options.RuntimeLaunch = Descriptor(copies.Root, "late-selection");
        }).Build();
        Assert.Null(app.Services.GetService(typeof(NativeRuntimeLaunch)));
        var factory = Assert.IsType<NativeRuntimeFactory>(app.Services.Resolve<IRuntimeFactory>());
        Assert.Empty(factory.Leases);
        await app.StartAsync();
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>());
        Assert.Equal(SessionState.Stopped, host.Supervisor.Status.State);
        Assert.Empty(factory.Leases);

        await using var lease = Assert.IsType<NativeRuntimeLease>(factory.Create(Launch(copies.Root)));
        Assert.Same(lease, Assert.Single(factory.Leases));
        Assert.False(lease.ResourcesReleased);
        Assert.Equal(0, lease.ProcessId);
        Assert.Empty(factory.Diagnostics);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lease.LaunchAsync(timeout.Token));

        Assert.Contains("AetherOptions.RuntimeLaunch", error.Message);
        var failure = Assert.Single(factory.Diagnostics, item => item.Stage == UnitProcessStage.LaunchFailed);
        Assert.Same(error, failure.Error);
        Assert.Null(failure.ProcessId);
        Assert.DoesNotContain(factory.Diagnostics, item => item.Stage == UnitProcessStage.Started);
        Assert.Equal(0, lease.ProcessId);
        Assert.Null(lease.StandardOutputStatus);
        Assert.Null(lease.StandardErrorStatus);
        await lease.StopAsync(timeout.Token);
        await lease.DisposeAsync();
        Assert.True(lease.ResourcesReleased);
        Assert.True(lease.Reaped);
        Assert.True(lease.Exited.IsCompletedSuccessfully);
        await app.StopAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultNativeWithoutDescriptor_SupervisorCleansFailedStartup(bool di)
    {
        using var copies = new RuntimeCopies();
        await using var app = Builder(di).Setup(context => context.UseAether(options => Configure(options, copies.Root))).Build();
        await app.StartAsync();
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>());
        var factory = Assert.IsType<NativeRuntimeFactory>(app.Services.Resolve<IRuntimeFactory>());
        Assert.Empty(factory.Leases);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Supervisor.StartPreviewAsync(new SceneSnapshot("bootstrap-preview", 1, []), timeout.Token));

        Assert.Contains("AetherOptions.RuntimeLaunch", error.Message);
        Assert.Equal(SessionState.Faulted, host.Supervisor.Status.State);
        Assert.Equal("Launch", host.Supervisor.Status.Failure!.Phase);
        Assert.Same(error, host.Supervisor.Status.Failure.Cause);
        Assert.Null(host.Supervisor.Status.Failure.CleanupError);
        var lease = Assert.Single(factory.Leases);
        Assert.Equal(0, lease.ProcessId);
        Assert.True(lease.ResourcesReleased);
        Assert.True(lease.Reaped);
        Assert.True(lease.Exited.IsCompletedSuccessfully);
        Assert.DoesNotContain(factory.Diagnostics, item => item.Stage == UnitProcessStage.Started);
        await app.StopAsync();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RuntimeLaunch_RegistersImmutableSelection_IndependentOfMutatedOptions(bool di, bool clearSelection)
    {
        using var copies = new RuntimeCopies();
        var arguments = new List<string> { "--selected", "two words" };
        var selected = new NativeRuntimeLaunch(Path.Combine(copies.Root, "selected.exe"), Path.Combine(copies.Root, "selected.dll"), arguments);
        var replacement = Descriptor(copies.Root, "replacement");
        await using var app = Builder(di).Setup(context =>
        {
            AetherOptions options = null!;
            context.UseAether(value => { Configure(value, copies.Root); value.RuntimeLaunch = selected; options = value; });
            options.RuntimeLaunch = clearSelection ? null : replacement;
            options.RuntimeFactory = _ => throw new InvalidOperationException("Mutated options must not choose the factory.");
            arguments[0] = "changed";
            arguments.Add("added");
        }).Build();

        var registered = Assert.IsType<NativeRuntimeLaunch>(app.Services.Resolve<NativeRuntimeLaunch>());
        Assert.Same(selected, registered);
        Assert.Same(selected, app.Services.Resolve<NativeRuntimeLaunch>());
        Assert.Equal(new[] { "--selected", "two words" }, registered.Arguments);
        var factory = Assert.IsType<NativeRuntimeFactory>(app.Services.Resolve<IRuntimeFactory>());
        Assert.Empty(factory.Leases);
        await using var lease = Assert.IsType<NativeRuntimeLease>(factory.Create(Launch(copies.Root)));
        Assert.Equal(0, lease.ProcessId);
        Assert.Empty(factory.Diagnostics);
        await lease.DisposeAsync();
        Assert.True(lease.ResourcesReleased);
        Assert.DoesNotContain(factory.Diagnostics, item => item.Stage == UnitProcessStage.Started);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CustomFactory_IsUnaffectedByNativeDescriptor_AndRemainsAppOwned(bool di, bool nativeDescriptor)
    {
        using var copies = new RuntimeCopies();
        var descriptor = nativeDescriptor ? Descriptor(copies.Root, "unused-native") : null;
        TrackingFactory? factory = null;
        var calls = 0;
        var mutatedCalls = 0;
        await using (var app = Builder(di).Setup(context =>
        {
            AetherOptions options = null!;
            context.UseAether(value =>
            {
                Configure(value, copies.Root);
                value.RuntimeLaunch = descriptor;
                value.RuntimeFactory = resolver =>
                {
                    calls++;
                    Assert.Same(descriptor, resolver.TryResolve<NativeRuntimeLaunch>());
                    return factory = new TrackingFactory();
                };
                options = value;
            });
            options.RuntimeLaunch = Descriptor(copies.Root, "late-native");
            options.RuntimeFactory = _ => { mutatedCalls++; return new TrackingFactory(); };
        }).Build())
        {
            Assert.Equal(0, calls);
            await app.StartAsync();
            Assert.Same(factory, app.Services.Resolve<IRuntimeFactory>());
            Assert.Equal(1, calls);
            Assert.Equal(0, mutatedCalls);
            Assert.Empty(factory!.Leases);
            var launch = Launch(copies.Root);
            var leaseFactory = Assert.IsAssignableFrom<IFactory<IRuntimeLease, SessionLaunch>>(
                app.Services.Resolve<IFactory<IRuntimeLease, SessionLaunch>>());
            await using (var lease = Assert.IsType<TrackingLease>(leaseFactory.Create(launch)))
            {
                Assert.Same(launch, lease.Launch);
                Assert.Same(lease, Assert.Single(factory.Leases));
                await lease.LaunchAsync(CancellationToken.None);
                Assert.Equal(1, lease.Launches);
            }
            Assert.True(Assert.Single(factory.Leases).ResourcesReleased);
            await app.StopAsync();
            Assert.Equal(0, factory.Disposals);
        }
        Assert.NotNull(factory);
        Assert.Equal(1, factory.Disposals);
        Assert.Equal(1, Assert.Single(factory.Leases).Disposals);
        Assert.Equal(0, mutatedCalls);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });

    private static void Configure(AetherOptions options, string directory)
    {
        options.HostContractId = Hash;
        options.GameBuildId = Hash;
        options.ReportDirectory = directory;
    }

    private static NativeRuntimeLaunch Descriptor(string directory, string name)
        => new(Path.Combine(directory, name + ".exe"), Path.Combine(directory, name + ".dll"), ["--profile", name]);

    private static SessionLaunch Launch(string directory, string? hostId = null, RuntimeLaunchIdentity? runtime = null)
        => new(new(Guid.NewGuid(), 1, hostId ?? Hash, BuildIdentity.PreviewId, Hash),
            "engine-" + Guid.NewGuid().ToString("N"), directory, Role: RuntimeRole.EditPreview) { RuntimeIdentity = runtime };

    private static string Pair(string host, string runtime)
        => HashText("TrueMoon.Aether.HostRuntime.v1\nhost:" + FileHash(host) + "\nruntime:" + FileHash(runtime) + "\nshared:" + Manifest(Shared));

    private static string Manifest(IEnumerable<string> paths)
        => HashText(string.Join("\n", paths.OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path => Path.GetFileName(path) + ":" + FileHash(path))));

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed class RuntimeCopies : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("TrueMoon.NativeRuntimeLaunchTests-");
        internal string Root => _root.FullName;

        internal string Copy(string name)
        {
            var directory = Directory.CreateDirectory(Path.Combine(Root, name)).FullName;
            var entry = Path.Combine(directory, "Copied.Runtime.dll");
            File.Copy(Entry, entry);
            foreach (var shared in Shared) File.Copy(shared, Path.Combine(directory, Path.GetFileName(shared)));
            return entry;
        }

        internal void Corrupt(string copiedPath)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!Path.GetFullPath(copiedPath).StartsWith(Root + Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException("Only fixture-owned copies may be modified.");
            var bytes = File.ReadAllBytes(copiedPath);
            Assert.NotEmpty(bytes);
            bytes[^1] ^= 1;
            File.WriteAllBytes(copiedPath, bytes);
        }

        public void Dispose() => _root.Delete(recursive: true);
    }

    private sealed class TrackingFactory : IRuntimeFactory, IDisposable
    {
        internal List<TrackingLease> Leases { get; } = [];
        internal int Disposals { get; private set; }
        public IRuntimeLease Create(SessionLaunch launch)
        {
            var lease = new TrackingLease(launch);
            Leases.Add(lease);
            return lease;
        }
        public void Dispose() => Disposals++;
    }

    private sealed class TrackingLease(SessionLaunch launch) : IRuntimeLease
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal SessionLaunch Launch => launch;
        internal int Launches { get; private set; }
        internal int Disposals { get; private set; }
        public int ProcessId => 0;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Launches++; return Task.CompletedTask; }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => throw new NotSupportedException("No protocol is needed for factory selection.");
        public Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            ResourcesReleased = true;
            _exit.TrySetResult();
            return Task.CompletedTask;
        }
        public async ValueTask DisposeAsync() { Disposals++; await StopAsync(); }
    }
}
