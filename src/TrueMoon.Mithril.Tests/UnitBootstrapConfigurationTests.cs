using TrueMoon.Configuration;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril.Tests;

public sealed class UnitBootstrapConfigurationTests
{
    [Fact]
    public void SelectedBootstrapUsesCommonAliasesAndIgnoresUnrelatedArguments()
    {
        var launch = UnitBootstrap.Parse(["--foreign=ignored", "loose", "-u=1", "--Mithril:UnitId", "2", "-p=3", "--Mithril:ControlPipe=core74-safe", "-ud=1"], EmptyEnvironment());
        Assert.Equal(new UnitLaunchSettings(2, 3, "core74-safe", true), launch);
    }

    [Theory]
    [InlineData("-ud", true)]
    [InlineData("-ud=1", true)]
    [InlineData("-ud=0", false)]
    [InlineData("--Mithril:Disabled=false", false)]
    [InlineData("--Mithril:Disabled=true", true)]
    public void DisableControlUsesCanonicalFlagsAndLegacyNumericAlias(string argument, bool expected)
    {
        var launch = UnitBootstrap.Parse([argument], EmptyEnvironment());
        Assert.Equal(expected, launch.UnitsDisabled);
        Assert.Null(launch.UnitId);
        Assert.Null(launch.ParentProcessId);
    }

    [Fact]
    public void ProtectedEnvironmentIdentityReplacesTheWholeCliGroup()
    {
        var launch = UnitBootstrap.Parse(["-u=9", "-p=10", "--Mithril:ControlPipe=cli", "-ud=0"],
            new Dictionary<string, string?> { ["-u"] = "2", ["-p"] = "3", [UnitParentProcessEventsHandler.ControlPipeKey] = "environment", ["TRUEMOON_MITHRIL__DISABLED"] = "true" });
        Assert.Equal(new UnitLaunchSettings(2, 3, "environment", false), launch);
    }

    [Fact]
    public void PartialEnvironmentIdentityDoesNotBorrowCliParentOrEndpoint()
    {
        var launch = UnitBootstrap.Parse(["-u=8", "-p=9", "--Mithril:ControlPipe=cli"], new Dictionary<string, string?> { ["-u"] = "2" });
        Assert.Equal(2, launch.UnitId);
        Assert.Null(launch.ParentProcessId);
        Assert.Null(launch.ControlPipe);
    }

    [Fact]
    public void NullEnvironmentIdentityDoesNotSuppressCliAndCapturedRecordIsDetached()
    {
        var environment = new Dictionary<string, string?> { ["-u"] = null };
        var launch = UnitBootstrap.Parse(["-u=4", "-p=3"], environment);
        environment["-u"] = "7";
        Assert.Equal(new UnitLaunchSettings(4, 3), launch);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("secret-invalid")]
    [InlineData("")]
    public void InvalidProtectedWinnerNeverFallsBackToValidCli(string invalid)
    {
        var error = Assert.Throws<ConfigurationException>(() => UnitBootstrap.Parse(["-u=1"], new Dictionary<string, string?> { ["-u"] = invalid }));
        Assert.NotEmpty(error.Issues);
        Assert.All(error.Issues, issue => Assert.DoesNotContain("secret-invalid", issue.Message));
        Assert.DoesNotContain("secret-invalid", error.ToString());
    }

    [Theory]
    [InlineData("-u=0")]
    [InlineData("-u=-1")]
    [InlineData("-p=0")]
    [InlineData("-p=-1")]
    [InlineData("-ud=invalid")]
    public void InvalidKnownBootstrapValuesAreRejected(string argument)
    {
        var error = Assert.Throws<ConfigurationException>(() => UnitBootstrap.Parse([argument], EmptyEnvironment()));
        Assert.NotEmpty(error.Issues);
    }

    [Fact]
    public void ParentCannotBeThisProcess()
    {
        Assert.Throws<ConfigurationException>(() => UnitBootstrap.Parse(["-p=" + Environment.ProcessId], EmptyEnvironment()));
        Assert.ThrowsAny<ArgumentException>(() => new UnitLaunchSettings(ParentProcessId: Environment.ProcessId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("private/endpoint")]
    [InlineData("private\\endpoint")]
    [InlineData("private:endpoint")]
    [InlineData("private\nendpoint")]
    public void UnsafeControlEndpointFailsWithRedactedDiagnostics(string endpoint)
    {
        var error = Assert.Throws<ConfigurationException>(() => UnitBootstrap.Parse(["-p=3", "--Mithril:ControlPipe=" + endpoint], EmptyEnvironment()));
        Assert.All(error.Issues, issue => Assert.DoesNotContain("private", issue.Message));
        Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public void ControlEndpointRequiresParentAndHonorsBoundedLength()
    {
        Assert.Throws<ConfigurationException>(() => UnitBootstrap.Parse(["--Mithril:ControlPipe=orphan"], EmptyEnvironment()));
        Assert.Throws<ConfigurationException>(() => UnitBootstrap.Parse(["-p=3", "--Mithril:ControlPipe=" + new string('a', 257)], EmptyEnvironment()));
        Assert.Equal(new string('a', 256), UnitBootstrap.Parse(["-p=3", "--Mithril:ControlPipe=" + new string('a', 256)], EmptyEnvironment()).ControlPipe);
    }

    [Fact]
    public void LegacyReadUsesOriginalStandardProviderInputsAndReturnsFrozenIdentity()
    {
        var environment = new Dictionary<string, string?> { ["-u"] = "2" };
        var configuration = Legacy(["-u=8", "-p=9", "-ud=1"], environment);
        var launch = UnitBootstrap.Read(configuration);
        environment["-u"] = "7";
        Assert.IsType<CommonConfiguration>(configuration).Refresh();
        configuration.Set("-u", "6");
        Assert.Equal(new UnitLaunchSettings(2, UnitsDisabled: true), launch);
        Assert.Equal(7, UnitBootstrap.Read(configuration).UnitId);
    }

    [Theory]
    [InlineData("env")]
    [InlineData("args")]
    [InlineData("default")]
    [InlineData("foreign")]
    public void ArbitraryProviderCannotSpoofProtectedInputsEvenUsingStandardSectionNames(string name)
    {
        var configuration = Legacy(["-u=1"], EmptyEnvironment(),
            new OverlayProvider(name, new() { ["-u"] = "99", ["-p"] = "98", [UnitParentProcessEventsHandler.ControlPipeKey] = "spoofed", ["-ud"] = "1" }));
        Assert.Equal("99", configuration.Get<string>("-u"));
        Assert.Equal(new UnitLaunchSettings(1), UnitBootstrap.Read(configuration));
    }

    [Fact]
    public void RemovedStandardEnvironmentIsNotRecapturedFromUnknownProvider()
    {
        var builder = new ConfigurationBuilder();
        builder.RemoveProvider<EnvironmentVariablesProvider>();
        builder.RemoveProvider<CommandLineArgsProvider>();
        builder.AddProvider(new CommandLineArgsProvider(() => ["-u=1"]));
        builder.AddProvider(new OverlayProvider("env", new() { ["-u"] = "99" }));
        Assert.Equal(new UnitLaunchSettings(1), UnitBootstrap.Read(builder.Build()));
    }

    [Fact]
    public void ExplicitDefaultIdentityRemainsCompatibleAndArgumentsTakePriority()
    {
        var defaults = Legacy([], EmptyEnvironment());
        defaults.Set("-u", "2").Set("-p", "3").Set(UnitParentProcessEventsHandler.ControlPipeKey, "default-pipe");
        Assert.Equal(new UnitLaunchSettings(2, 3, "default-pipe"), UnitBootstrap.Read(defaults));
        var arguments = Legacy(["-u=4"], EmptyEnvironment());
        arguments.Set("-u", "2");
        Assert.Equal(4, UnitBootstrap.Read(arguments).UnitId);
    }

    [Theory]
    [InlineData(3, "-u=1", "--Mithril:UnitId", "2", "-u", "3")]
    [InlineData(2, "-u", "1", "--Mithril:UnitId=2")]
    [InlineData(1, "--Mithril:UnitId=2", "-u", "1")]
    public void OriginalStandardProviderTokensPreserveSelectedAliasOrder(int expected, params string[] arguments)
    {
        var launch = UnitBootstrap.Read(Legacy(arguments, EmptyEnvironment()));
        Assert.Equal(expected, launch.UnitId);
        Assert.Null(launch.ParentProcessId);
    }

    [Theory]
    [InlineData("4", 4)]
    [InlineData(null, null)]
    public void ExplicitLegacyArgumentMutationOverridesOrErasesCapturedIdentity(string? replacement, int? expected)
    {
        var configuration = Legacy(["-u", "1", "--Mithril:UnitId=2"], EmptyEnvironment());
        Assert.Equal(2, UnitBootstrap.Read(configuration).UnitId);
        configuration.GetSection("args")!.Set("-u", replacement);
        Assert.Equal(expected, UnitBootstrap.Read(configuration).UnitId);
    }

    [Fact]
    public void CustomConfigurationWithoutBootstrapCapabilityRetainsTrustedBoundedFallback()
    {
        var custom = new DelegatedConfiguration(Legacy(["-u=1"], new Dictionary<string, string?> { ["-u"] = "2" },
            new OverlayProvider("foreign", new() { ["-u"] = "99", ["-p"] = "98" })));
        Assert.Equal("99", custom.Get<string>("-u"));
        Assert.Equal(new UnitLaunchSettings(2), UnitBootstrap.Read(custom));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppUnitSelectionCapturesTypedLaunchBeforeLaterLegacyMutation(bool microsoft)
    {
        var selected = 0;
        var ordinarySchema = ConfigurationSchemas.Create("Mithril", () => new OrdinarySettings(), value => value with { }, _ => { },
            fields => fields.Field("UnitId", value => value.UnitId, (value, next) => value.UnitId = next));
        var ordinaryProfile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(ordinarySchema);
            builder.Override("Mithril:UnitId", 99);
        });
        await using var app = Builder(microsoft).Configuration(builder =>
        {
            builder.RemoveProvider<EnvironmentVariablesProvider>();
            builder.RemoveProvider<CommandLineArgsProvider>();
            builder.AddProvider(new EnvironmentVariablesProvider(EmptyEnvironment));
            builder.AddProvider(new CommandLineArgsProvider(() => ["-u", "1"]));
        }).Setup(c => c.UseConfiguration(ordinaryProfile).AddUnit(_ => selected++).AddUnit(_ => throw new InvalidOperationException("wrong-unit"))
            .Configuration(configuration => configuration.GetSection("args")!.Set("-u", "2"))).Build();
        var launch = Assert.IsType<UnitLaunchSettings>(app.Services.GetService(typeof(UnitLaunchSettings)));
        Assert.Equal(1, selected);
        Assert.Equal(1, launch.UnitId);
        Assert.Equal(99, Assert.IsAssignableFrom<IConfigurationReader>(app.Services.GetService(typeof(IConfigurationReader)))
            .Current.Read<int>("Mithril:UnitId").Value);
        Assert.Equal("2", app.Configuration.Get<string>("-u", "args"));
        Assert.Null(app.Services.GetService(typeof(IUnitsController)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisableControlPreventsMainUnitActionAndControllerCreation(bool microsoft)
    {
        var actions = 0;
        await using var app = Builder(microsoft).Configuration(builder =>
        {
            builder.RemoveProvider<EnvironmentVariablesProvider>();
            builder.RemoveProvider<CommandLineArgsProvider>();
            builder.AddProvider(new CommandLineArgsProvider(() => ["-ud=1"]));
        }).Setup(c => c.AddUnit(_ => actions++, unit => unit.ExecuteInMainProcess())).Build();
        Assert.True(Assert.IsType<UnitLaunchSettings>(app.Services.GetService(typeof(UnitLaunchSettings))).UnitsDisabled);
        Assert.Equal(0, actions);
        Assert.Null(app.Services.GetService(typeof(IUnitsController)));
    }

    [Fact]
    public async Task TypedAndLegacyParentHandlersBorrowLifetimeWithoutStartingOnConstruction()
    {
        using var lifetime = new TestLifetime();
        var events = new TestEvents<UnitParentProcessEventsHandler>();
        await using (var typed = new UnitParentProcessEventsHandler(new UnitLaunchSettings(1), lifetime, events))
        await using (var legacy = new UnitParentProcessEventsHandler(Legacy(["-u=1"], EmptyEnvironment()), lifetime, events))
        {
            Assert.True(typed.Completion.IsCompletedSuccessfully);
            Assert.True(legacy.Completion.IsCompletedSuccessfully);
            Assert.Equal(0, lifetime.Calls);
            Assert.False(lifetime.AppCancellationToken.IsCancellationRequested);
        }
        Assert.False(lifetime.AppCancellationToken.IsCancellationRequested);
        lifetime.Cancel();
        Assert.Equal(1, lifetime.Calls);
    }

    private static Dictionary<string, string?> EmptyEnvironment() => new();
    private sealed record OrdinarySettings { public int UnitId { get; set; } }
    private static IConfiguration Legacy(IReadOnlyList<string> args, IReadOnlyDictionary<string, string?> environment, IConfigurationProvider? overlay = null)
    {
        var builder = new ConfigurationBuilder();
        builder.RemoveProvider<CommandLineArgsProvider>();
        builder.RemoveProvider<EnvironmentVariablesProvider>();
        builder.AddProvider(new CommandLineArgsProvider(() => args));
        builder.AddProvider(new EnvironmentVariablesProvider(() => environment));
        if (overlay is not null) builder.AddProvider(overlay);
        return builder.Build();
    }
    private sealed class OverlayProvider(string name, Dictionary<string, object?> values) : IConfigurationProvider
    {
        public string Name => "untrusted";
        public IReadOnlyList<IConfigurationSection> GetSections() => [new ConfigurationSection(name, values)];
    }
    private sealed class DelegatedConfiguration(IConfiguration inner) : IConfiguration
    {
        public IConfigurationSection? GetSection(string? name = default) => inner.GetSection(name);
        public Task<IConfigurationSection?> GetSectionAsync(string? name = default, CancellationToken cancellationToken = default)
            => inner.GetSectionAsync(name, cancellationToken);
        public bool TryGetSection(string? name, out IConfigurationSection? section) => inner.TryGetSection(name, out section);
        public IConfigurationSection[] GetSections() => inner.GetSections();
        public object? this[string key] { get => inner[key]; set => inner[key] = value; }
        public object? this[string key, string section] { get => inner[key, section]; set => inner[key, section] = value; }
    }
    private static IAppBuilder Builder(bool microsoft) => microsoft ? App.Builder(c => c.UseDI()) : App.Builder(_ => { });
}
