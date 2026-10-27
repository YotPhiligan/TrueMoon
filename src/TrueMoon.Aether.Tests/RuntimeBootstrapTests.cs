using System.Globalization;
using System.Reflection;
using System.Text.Json;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests;

/// <summary>Checks the data-only runtime bootstrap boundary without starting an App or child process.</summary>
[Collection(nameof(RuntimeBootstrapTests))]
public sealed class RuntimeBootstrapTests
{
    private const int MaximumTextLength = 16384;
    private static readonly string Hash = new('A', 64);
    private static int ParentId => Environment.ProcessId == 1 ? 2 : 1;
    private static string ParentText => ParentId.ToString(CultureInfo.InvariantCulture);

    private static SessionLaunch Preview()
    {
        var entry = Assembly.GetEntryAssembly()!.Location;
        return new(new(Guid.NewGuid(), 1, BuildIdentity.HostId, BuildIdentity.PreviewId, Hash),
            "engine-bootstrap-tests", Path.Combine(Path.GetTempPath(), "TrueMoon.RuntimeBootstrap", Guid.NewGuid().ToString("N")),
            Role: RuntimeRole.EditPreview)
        {
            RuntimeIdentity = new(entry, entry)
        };
    }

    private static SessionLaunch Game()
    {
        var launch = Preview();
        return launch with
        {
            Identity = launch.Identity with { GameBuildId = Hash },
            Role = RuntimeRole.Game,
            Build = new(Path.Combine(launch.ReportDirectory, "unused-build"), Hash)
        };
    }

    private static void Reject(SessionLaunch? launch)
    {
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText));
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Encode(launch!));
    }

    [Fact]
    public void Preview_RoundTripsIdentityRoleAndParent_WithoutCreatingReportDirectory()
    {
        var launch = Preview();
        Assert.False(Directory.Exists(launch.ReportDirectory));

        var result = RuntimeBootstrap.Parse(RuntimeBootstrap.Encode(launch), ParentText);

        Assert.Equal(launch, result.Launch);
        Assert.Equal(ParentId, result.ParentProcessId);
        Assert.Null(result.Launch.DiagnosticDelivery);
        Assert.False(Directory.Exists(launch.ReportDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Game_WithMatchingSelectedBuild_DoesNotOpenArtifact(bool requireRestore)
    {
        var launch = Game() with { RequireRestore = requireRestore };
        Assert.False(Directory.Exists(launch.Build!.DirectoryPath));

        var result = RuntimeBootstrap.Parse(RuntimeBootstrap.Encode(launch), ParentText);

        Assert.Equal(launch, result.Launch);
        Assert.False(Directory.Exists(launch.Build.DirectoryPath));
        Assert.False(Directory.Exists(launch.ReportDirectory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("\"launch\"")]
    public void Parse_RejectsMissingNullOrWrongRootPayload(string? text)
        => Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(text!, ParentText));

    [Theory]
    [InlineData("{")]
    [InlineData("{\"Identity\":false}")]
    [InlineData("{\"Role\":\"Game\"}")]
    [InlineData("{} trailing")]
    public void Parse_WrapsJsonException(string text)
    {
        var error = Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(text, ParentText));
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
    }

    [Fact]
    public void Parse_RejectsOversizedTextBeforeJsonDeserialization()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            RuntimeBootstrap.Parse(new string('{', MaximumTextLength + 1), ParentText));
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void Parse_AcceptsExactly16384CharactersIncludingWhitespace()
    {
        var launch = Preview();
        var json = JsonSerializer.Serialize(launch);
        var text = json + new string(' ', MaximumTextLength - json.Length);

        Assert.Equal(MaximumTextLength, text.Length);
        Assert.Equal(launch, RuntimeBootstrap.Parse(text, ParentText).Launch);
    }

    [Fact]
    public void Encode_AcceptsExactly16384Characters_AndRejectsOneMore()
    {
        var launch = Preview() with { Behavior = "" };
        var remaining = MaximumTextLength - RuntimeBootstrap.Encode(launch).Length;
        launch = launch with { Behavior = new string('x', remaining) };

        var text = RuntimeBootstrap.Encode(launch);

        Assert.Equal(MaximumTextLength, text.Length);
        Assert.Equal(launch, RuntimeBootstrap.Parse(text, ParentText).Launch);
        Reject(launch with { Behavior = launch.Behavior + "x" });
    }

    [Fact]
    public void Parse_AcceptsJsonDepth32_AndWrapsFailureAtDepth33()
    {
        var launch = Preview();
        var json = JsonSerializer.Serialize(launch);
        string Nested(int arrays) => json[..^1] + ",\"Extra\":" + new string('[', arrays) + "0" + new string(']', arrays) + "}";

        Assert.Equal(launch, RuntimeBootstrap.Parse(Nested(31), ParentText).Launch);
        var error = Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(Nested(32), ParentText));
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
    }

    [Fact]
    public void SharedValidation_RejectsNullLaunchAndIdentity()
    {
        Reject(null);
        Reject(Preview() with { Identity = null! });
    }

    [Theory]
    [InlineData("session")]
    [InlineData("generation")]
    [InlineData("null-host")]
    [InlineData("null-game")]
    [InlineData("null-nonce")]
    [InlineData("host")]
    [InlineData("game")]
    [InlineData("nonce")]
    [InlineData("control-version")]
    [InlineData("frame-version")]
    [InlineData("snapshot-version")]
    public void SharedValidation_RejectsInvalidSessionIdentityAndVersions(string field)
    {
        var launch = Preview();
        var identity = launch.Identity;
        identity = field switch
        {
            "session" => identity with { SessionId = Guid.Empty },
            "generation" => identity with { Generation = 0 },
            "null-host" => identity with { HostContractId = null! },
            "null-game" => identity with { GameBuildId = null! },
            "null-nonce" => identity with { Nonce = null! },
            "host" => identity with { HostContractId = "short" },
            "game" => identity with { GameBuildId = new string('Z', 64) },
            "nonce" => identity with { Nonce = "" },
            "control-version" => identity with { ControlVersion = 4 },
            "frame-version" => identity with { FrameVersion = 2 },
            "snapshot-version" => identity with { SnapshotVersion = 3 },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        Reject(launch with { Identity = identity });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("1,000")]
    [InlineData("\u0661")]
    public void Parse_RejectsInvalidParent(string? parent)
        => Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(Preview()), parent!));

    [Fact]
    public void Parse_RejectsCurrentProcessAsParent()
        => Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(Preview()),
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));

    [Fact]
    public void Parse_UsesInvariantParentParsing_WithoutLookingUpTheProcess()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var parent = Environment.ProcessId == int.MaxValue ? int.MaxValue - 1 : int.MaxValue;
            var result = RuntimeBootstrap.Parse(JsonSerializer.Serialize(Preview()), parent.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(parent, result.ParentProcessId);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("other-endpoint")]
    [InlineData("Engine-endpoint")]
    public void SharedValidation_RejectsInvalidEndpoint(string? endpoint)
        => Reject(Preview() with { Endpoint = endpoint! });

    [Fact]
    public void SharedValidation_EnforcesEndpointLength100()
    {
        var launch = Preview() with { Endpoint = "engine-" + new string('x', 93) };
        Assert.Equal(100, launch.Endpoint.Length);
        Assert.Equal(launch, RuntimeBootstrap.Parse(RuntimeBootstrap.Encode(launch), ParentText).Launch);
        Reject(launch with { Endpoint = launch.Endpoint + "x" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative-reports")]
    [InlineData("../reports")]
    public void SharedValidation_RejectsMissingOrRelativeReportDirectory(string? directory)
        => Reject(Preview() with { ReportDirectory = directory! });

    [Fact]
    public void SharedValidation_NormalizesAbsoluteReportDirectory_WithoutChangingInput()
    {
        var launch = Preview();
        var directory = Path.Combine(launch.ReportDirectory, "nested", "..", "reports");
        launch = launch with { ReportDirectory = directory };
        var expected = Path.GetFullPath(directory);

        Assert.Equal(expected, RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText).Launch.ReportDirectory);
        var encoded = JsonSerializer.Deserialize<SessionLaunch>(RuntimeBootstrap.Encode(launch));
        Assert.Equal(expected, encoded!.ReportDirectory);
        Assert.Equal(directory, launch.ReportDirectory);
        Assert.False(Directory.Exists(expected));
    }

    [Fact]
    public void SharedValidation_WrapsInvalidReportPath()
        => Reject(Preview() with { ReportDirectory = Path.Combine(Path.GetTempPath(), "invalid\0reports") });

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void SharedValidation_RejectsUnknownRole(int role)
        => Reject(Preview() with { Role = (RuntimeRole)role });

    [Fact]
    public void Game_RequiresSelectedMatchingBuildByDefault()
    {
        var launch = Game();
        Reject(launch with { Build = null });
        Reject(launch with { Build = launch.Build! with { BuildId = new string('B', 64) } });
        Reject(launch with { Build = launch.Build! with { BuildId = null! } });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative-build")]
    [InlineData("../build")]
    [InlineData("\0")]
    public void Game_RejectsMissingRelativeOrNulArtifactDirectory(string? directory)
    {
        if (directory == "\0") directory = Path.Combine(Path.GetTempPath(), "invalid\0build");
        var launch = Game();
        Reject(launch with { Build = launch.Build! with { DirectoryPath = directory! } });
    }

    [Fact]
    public void Game_NormalizesArtifactDirectory_WithoutOpeningArtifactOrChangingInput()
    {
        var launch = Game();
        var directory = Path.Combine(launch.ReportDirectory, "staging", "..", "unused-build");
        var build = launch.Build! with { DirectoryPath = directory };
        launch = launch with { Build = build };
        var expected = Path.GetFullPath(directory);
        Assert.False(Directory.Exists(expected));

        var parsed = RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText).Launch;
        var encoded = Assert.IsType<SessionLaunch>(JsonSerializer.Deserialize<SessionLaunch>(RuntimeBootstrap.Encode(launch)));

        Assert.Equal(build with { DirectoryPath = expected }, parsed.Build);
        Assert.Equal(parsed.Build, encoded.Build);
        Assert.NotSame(build, parsed.Build);
        Assert.Same(build, launch.Build);
        Assert.Equal(directory, build.DirectoryPath);
        Assert.False(Directory.Exists(expected));
        Assert.False(Directory.Exists(launch.ReportDirectory));
    }

    [Fact]
    public void LegacyGame_RequiresExplicitPermissionAndExactLocalGameId()
    {
        var launch = Game();
        var legacy = launch with { Identity = launch.Identity with { GameBuildId = BuildIdentity.GameId }, Build = null };
        Reject(legacy);

        var result = RuntimeBootstrap.Parse(JsonSerializer.Serialize(legacy), ParentText, allowLegacyGame: true);
        Assert.Equal(legacy, result.Launch);
        Assert.Throws<InvalidDataException>(() =>
            RuntimeBootstrap.Parse(JsonSerializer.Serialize(legacy with { Identity = legacy.Identity with { GameBuildId = BuildIdentity.PreviewId } }),
                ParentText, allowLegacyGame: true));
        Assert.Throws<InvalidDataException>(() =>
            RuntimeBootstrap.Parse(JsonSerializer.Serialize(legacy with { Build = new GameBuildReference(launch.Build!.DirectoryPath, new string('B', 64)) }),
                ParentText, allowLegacyGame: true));
    }

    [Theory]
    [InlineData("build")]
    [InlineData("restore")]
    [InlineData("game-id")]
    public void Preview_ForbidsBuildRestoreAndMismatchedPreviewId_EvenWithLegacyPermission(string field)
    {
        var launch = Preview();
        launch = field switch
        {
            "build" => launch with { Build = new(launch.ReportDirectory, launch.Identity.GameBuildId) },
            "restore" => launch with { RequireRestore = true },
            "game-id" => launch with { Identity = launch.Identity with { GameBuildId = Hash } },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        Reject(launch);
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText, allowLegacyGame: true));
    }

    [Fact]
    public void Parse_RejectsMissingRuntimeIdentity()
        => Assert.Throws<InvalidDataException>(() =>
            RuntimeBootstrap.Parse(JsonSerializer.Serialize(Preview() with { RuntimeIdentity = null }), ParentText));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void Parse_RejectsUnknownRuntimeIdentityVersion(int version)
    {
        var launch = Preview();
        launch = launch with { RuntimeIdentity = launch.RuntimeIdentity! with { Version = version } };
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "")]
    [InlineData(false, "")]
    [InlineData(true, "relative.dll")]
    [InlineData(false, "relative.dll")]
    [InlineData(true, "missing")]
    [InlineData(false, "missing")]
    public void Parse_RejectsInvalidRuntimeIdentityPaths(bool hostPath, string? path)
    {
        var launch = Preview();
        if (path == "missing") path = Path.Combine(launch.ReportDirectory, "missing.dll");
        var identity = launch.RuntimeIdentity!;
        launch = launch with
        {
            RuntimeIdentity = hostPath ? identity with { HostAssemblyPath = path! } : identity with { RuntimeAssemblyPath = path! }
        };
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText));
    }

    [Fact]
    public void Parse_RejectsHostFingerprintMismatch()
    {
        var launch = Preview();
        var wrongHost = launch.Identity.HostContractId == Hash ? new string('B', 64) : Hash;
        launch = launch with { Identity = launch.Identity with { HostContractId = wrongHost } };
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(JsonSerializer.Serialize(launch), ParentText));
    }

    [Fact]
    public void Encode_DoesNotValidateChildSelfIdentity_ButParseDoes()
    {
        var runtime = typeof(RuntimeBootstrap).Assembly.Location;
        var launch = Preview();
        launch = launch with
        {
            Identity = launch.Identity with { HostContractId = BuildIdentity.ForRuntime(runtime) },
            RuntimeIdentity = BuildIdentity.CurrentLaunchIdentity(runtime)
        };

        var text = RuntimeBootstrap.Encode(launch);

        Assert.Equal(launch.RuntimeIdentity, JsonSerializer.Deserialize<SessionLaunch>(text)!.RuntimeIdentity);
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.Parse(text, ParentText));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticOptions_AreSnapshotted_AndInputRemainsUnchanged(bool enabled)
    {
        var options = new ChildDiagnosticDeliveryOptions
        {
            Enabled = enabled,
            MaxBatchRecords = 4, MaxBatchBytes = 8192, RecordsPerSecond = 32, BytesPerSecond = 65536,
            PollInterval = TimeSpan.FromMilliseconds(20), RequestTimeout = TimeSpan.FromMilliseconds(100),
            ShutdownTimeout = TimeSpan.FromMilliseconds(500)
        };
        var expected = JsonSerializer.Serialize(options);
        var launch = Preview() with { DiagnosticDelivery = options };

        var text = RuntimeBootstrap.Encode(launch);
        Assert.Same(options, launch.DiagnosticDelivery);
        Assert.Equal(expected, JsonSerializer.Serialize(options));
        options.MaxBatchRecords = 0;
        var parsed = RuntimeBootstrap.Parse(text, ParentText).Launch.DiagnosticDelivery;

        Assert.NotNull(parsed);
        Assert.NotSame(options, parsed);
        Assert.Equal(expected, JsonSerializer.Serialize(parsed));
        parsed.MaxBatchBytes = 1024;
        Assert.Equal(expected, JsonSerializer.Serialize(RuntimeBootstrap.Parse(text, ParentText).Launch.DiagnosticDelivery));
    }

    [Theory]
    [InlineData("records")]
    [InlineData("batch-bytes")]
    [InlineData("record-rate")]
    [InlineData("byte-rate")]
    [InlineData("poll")]
    [InlineData("request")]
    [InlineData("shutdown")]
    [InlineData("disabled-invalid")]
    public void SharedValidation_RejectsInvalidDiagnosticOptions(string field)
    {
        var options = new ChildDiagnosticDeliveryOptions();
        switch (field)
        {
            case "records": options.MaxBatchRecords = 33; break;
            case "batch-bytes": options.MaxBatchBytes = 1023; break;
            case "record-rate": options.RecordsPerSecond = 0; break;
            case "byte-rate": options.BytesPerSecond = 0; break;
            case "poll": options.PollInterval = TimeSpan.Zero; break;
            case "request": options.RequestTimeout = TimeSpan.FromSeconds(3); break;
            case "shutdown": options.ShutdownTimeout = TimeSpan.FromSeconds(6); break;
            case "disabled-invalid": options.Enabled = false; options.MaxBatchRecords = 0; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
        Reject(Preview() with { DiagnosticDelivery = options });
    }

    [Fact]
    public void ReadEnvironment_UsesBootstrapKeyAndParentKey()
    {
        var launch = Preview();
        WithEnvironment(RuntimeBootstrap.Encode(launch), ParentText, () =>
        {
            var result = RuntimeBootstrap.ReadEnvironment();
            Assert.Equal(launch, result.Launch);
            Assert.Equal(ParentId, result.ParentProcessId);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadEnvironment_RejectsMissingBootstrapOrParent(bool missingBootstrap)
        => WithEnvironment(missingBootstrap ? null : RuntimeBootstrap.Encode(Preview()), missingBootstrap ? ParentText : null,
            () => Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.ReadEnvironment()));

    [Fact]
    public void ReadEnvironment_ForwardsExplicitLegacyPermission()
    {
        var launch = Game();
        launch = launch with { Identity = launch.Identity with { GameBuildId = BuildIdentity.GameId }, Build = null };
        WithEnvironment(JsonSerializer.Serialize(launch), ParentText, () =>
        {
            Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.ReadEnvironment());
            Assert.Equal(launch, RuntimeBootstrap.ReadEnvironment(allowLegacyGame: true).Launch);
        });
    }

    private static void WithEnvironment(string? text, string? parent, Action action)
    {
        var previousText = Environment.GetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey);
        var previousParent = Environment.GetEnvironmentVariable("-p");
        try
        {
            Environment.SetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey, text);
            Environment.SetEnvironmentVariable("-p", parent);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey, previousText);
            Environment.SetEnvironmentVariable("-p", previousParent);
        }
    }
}

/// <summary>Serializes process-wide environment changes against other xUnit collections.</summary>
[CollectionDefinition(nameof(RuntimeBootstrapTests), DisableParallelization = true)]
public sealed class RuntimeBootstrapTestCollection { }
