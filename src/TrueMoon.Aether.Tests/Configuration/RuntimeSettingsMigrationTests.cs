using System.Globalization;
using System.Reflection;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Sessions;
using TrueMoon.Configuration;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;
using Xunit;

namespace TrueMoon.Aether.Tests.Configuration;

[Collection(nameof(RuntimeBootstrapTests))]
public sealed class RuntimeSettingsMigrationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("TrUe", true)]
    [InlineData("false", false)]
    public void StartupFlagsUseCommonConversionWithoutReadingIdentity(string? value, bool expected)
    {
        var settings = RuntimeStartupSettings.Parse(new Dictionary<string, string?>
        {
            [RuntimeStartupSettings.LegacyValidationKey] = value,
            [NativeRuntimeFactory.BootstrapKey] = "unrelated-invalid-identity",
            ["-p"] = "invalid-parent"
        });
        Assert.Equal(expected, settings.GpuValidation);
    }

    [Theory]
    [InlineData("1", "false", false)]
    [InlineData("0", "true", true)]
    [InlineData("invalid-lower-value", "true", true)]
    public void CanonicalStartupFlagWinsOverLegacyAlias(string legacy, string canonical, bool expected)
    {
        var environment = new Dictionary<string, string?>
        { [RuntimeStartupSettings.LegacyValidationKey] = legacy, [RuntimeStartupSettings.ValidationKey] = canonical };
        var settings = RuntimeStartupSettings.Parse(environment);
        environment[RuntimeStartupSettings.ValidationKey] = expected ? "false" : "true";
        Assert.Equal(expected, settings.GpuValidation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("private-invalid-flag")]
    [InlineData("2")]
    public void InvalidWinningStartupFlagDoesNotFallBackAndIsRedacted(string invalid)
    {
        var error = Assert.Throws<ConfigurationException>(() => RuntimeStartupSettings.Parse(new Dictionary<string, string?>
        { [RuntimeStartupSettings.LegacyValidationKey] = "true", [RuntimeStartupSettings.ValidationKey] = invalid }));
        Assert.Contains(error.Issues, issue => issue.Path == "AetherRuntime:GpuValidation");
        Assert.All(error.Issues, issue => Assert.DoesNotContain("private-invalid-flag", issue.Message));
        Assert.DoesNotContain("private-invalid-flag", error.ToString());
    }

    [Fact]
    public void ProtectedBootstrapCapturesIdentityParentControlAndFlagsIndependentlyOfLaterInputChanges()
    {
        var launch = Preview();
        var environment = EnvironmentFor(launch);
        environment[RuntimeStartupSettings.ValidationKey] = "true";
        environment[UnitParentProcessEventsHandler.ControlPipeKey] = "core75-parent-control";
        environment["TRUEMOON_AETHER__HOSTCONTRACTID"] = new string('f', 64);

        var bootstrap = RuntimeBootstrap.ParseEnvironment(environment);

        Assert.Equal(launch, bootstrap.Launch);
        Assert.Equal(ParentId, bootstrap.ParentProcessId);
        Assert.Equal(new UnitLaunchSettings(ParentProcessId: ParentId, ControlPipe: "core75-parent-control"), bootstrap.ParentLaunch);
        Assert.True(bootstrap.Settings.GpuValidation);
        environment[NativeRuntimeFactory.BootstrapKey] = "invalid-after-capture";
        environment["-p"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        environment[RuntimeStartupSettings.ValidationKey] = "false";
        environment[UnitParentProcessEventsHandler.ControlPipeKey] = "mutated";
        Assert.Equal(launch.Identity, bootstrap.Launch.Identity);
        Assert.Equal(ParentId, bootstrap.ParentLaunch!.ParentProcessId);
        Assert.Equal("core75-parent-control", bootstrap.ParentLaunch.ControlPipe);
        Assert.True(bootstrap.Settings.GpuValidation);
        Assert.False(Directory.Exists(launch.ReportDirectory));
        Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.ParseEnvironment(environment));
    }

    [Theory]
    [InlineData("flags")]
    [InlineData("pipe")]
    [InlineData("oversized-envelope")]
    [InlineData("self-parent")]
    public void SelectedBootstrapRejectsInvalidInputBeforeAppOrResourceActivation(string invalid)
    {
        var launch = Preview();
        var environment = EnvironmentFor(launch);
        switch (invalid)
        {
            case "flags": environment[RuntimeStartupSettings.ValidationKey] = "private-invalid-flag"; break;
            case "pipe": environment[UnitParentProcessEventsHandler.ControlPipeKey] = "private/endpoint"; break;
            case "oversized-envelope": environment[NativeRuntimeFactory.BootstrapKey] = new string('x', 16385); break;
            case "self-parent": environment["-p"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture); break;
        }
        var error = Assert.Throws<InvalidDataException>(() => RuntimeBootstrap.ParseEnvironment(environment));
        Assert.DoesNotContain("private-invalid-flag", error.ToString());
        Assert.DoesNotContain("private/endpoint", error.ToString());
        Assert.False(Directory.Exists(launch.ReportDirectory));
    }

    [Fact]
    public void NativeCommandSettingsAreIndependentAndAffectOnlyChildStartInfo()
    {
        var beforeLegacy = Environment.GetEnvironmentVariable(RuntimeStartupSettings.LegacyValidationKey);
        var beforeCanonical = Environment.GetEnvironmentVariable(RuntimeStartupSettings.ValidationKey);
        var descriptor = NativeRuntimeLaunch.CurrentProcess("--runtime", "two words");
        var enabled = descriptor.WithStartupSettings(new(true));
        var disabled = enabled.WithStartupSettings(new(false));
        Assert.NotSame(descriptor, enabled);
        Assert.NotSame(enabled, disabled);
        Assert.Null(descriptor.StartupSettings);
        Assert.True(enabled.StartupSettings!.GpuValidation);
        Assert.False(disabled.StartupSettings!.GpuValidation);
        Assert.Equal(descriptor.Arguments, disabled.Arguments);
        Assert.Equal(descriptor.RuntimeAssemblyPath, disabled.RuntimeAssemblyPath);
        var info = enabled.StartInfo();
        Assert.Equal("1", info.Environment[RuntimeStartupSettings.LegacyValidationKey]);
        Assert.Equal("true", info.Environment[RuntimeStartupSettings.ValidationKey]);
        Assert.Equal("0", disabled.StartInfo().Environment[RuntimeStartupSettings.LegacyValidationKey]);
        Assert.Equal("false", disabled.StartInfo().Environment[RuntimeStartupSettings.ValidationKey]);
        info.Environment[RuntimeStartupSettings.ValidationKey] = "mutated";
        Assert.Equal("true", enabled.StartInfo().Environment[RuntimeStartupSettings.ValidationKey]);
        Assert.Equal(beforeLegacy, Environment.GetEnvironmentVariable(RuntimeStartupSettings.LegacyValidationKey));
        Assert.Equal(beforeCanonical, Environment.GetEnvironmentVariable(RuntimeStartupSettings.ValidationKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeRegistrationUsesCapturedParentInsteadOfMutableLegacyConfiguration(bool microsoft)
    {
        var bootstrap = RuntimeBootstrap.ParseEnvironment(EnvironmentFor(Preview()));
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseAether(options =>
            {
                options.HostContractId = bootstrap.Launch.Identity.HostContractId;
                options.GameBuildId = bootstrap.Launch.Identity.GameBuildId;
                options.ReportDirectory = bootstrap.Launch.ReportDirectory;
            });
            context.UseAetherPreviewRuntime(bootstrap);
            context.Configuration(configuration => configuration.Set("-p", Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
        }).Build();
        await using var handler = Assert.IsAssignableFrom<IFactory<UnitParentProcessEventsHandler>>(
            app.Services.GetService(typeof(IFactory<UnitParentProcessEventsHandler>))).Create()!;
        Assert.True(handler.Completion.IsCompletedSuccessfully);
        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), app.Configuration.Get<string>("-p"));
        Assert.False(Directory.Exists(bootstrap.Launch.ReportDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredRuntimeBindsBudgetsAndPreservesProtectedLaunchDeliveryAndStartupFlags(bool microsoft)
    {
        var launch = Preview() with { DiagnosticDelivery = new ChildDiagnosticDeliveryOptions { MaxBatchRecords = 4 } };
        var environment = EnvironmentFor(launch);
        environment[RuntimeStartupSettings.ValidationKey] = "true";
        var bootstrap = RuntimeBootstrap.ParseEnvironment(environment);
        var spoofedReport = Path.Combine(launch.ReportDirectory, "ordinary-profile");
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(AetherConfigurationSchemas.Engine);
            builder.Override("Aether:ReportDirectory", spoofedReport);
            builder.Override("Aether:ChildDiagnostics:MaxBatchRecords", 12);
            builder.Override("Aether:BuildTimeout", TimeSpan.FromSeconds(7));
        });
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(profile).UseConfiguredAetherRuntime(bootstrap, value =>
            {
                value.ReportDirectory = "callback-spoof";
                value.ChildDiagnostics.MaxBatchRecords = 17;
                value.GpuValidation = false;
            });
            bootstrap.Launch.DiagnosticDelivery!.MaxBatchRecords = 31;
            context.Configuration(configuration => configuration.Set("-p", Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
        }).Build();
        var reader = Assert.IsAssignableFrom<IConfigurationReader>(app.Services.GetService(typeof(IConfigurationReader)));
        var options = reader.Current.GetOptions(AetherConfigurationSchemas.Engine);
        Assert.Equal(bootstrap.Launch.ReportDirectory, options.ReportDirectory);
        Assert.Equal(4, options.ChildDiagnostics.MaxBatchRecords);
        Assert.Equal(TimeSpan.FromSeconds(7), options.BuildTimeout);
        Assert.Equal(spoofedReport, reader.Current.Read<string>("Aether:ReportDirectory").Value);
        Assert.Equal(12, reader.Current.Read<int>("Aether:ChildDiagnostics:MaxBatchRecords").Value);
        Assert.True(Assert.IsType<RuntimeStartupSettings>(app.Services.GetService(typeof(RuntimeStartupSettings))).GpuValidation);
        await using var parent = Assert.IsAssignableFrom<IFactory<UnitParentProcessEventsHandler>>(
            app.Services.GetService(typeof(IFactory<UnitParentProcessEventsHandler>))).Create()!;
        Assert.True(parent.Completion.IsCompletedSuccessfully);
        Assert.False(Directory.Exists(launch.ReportDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyRuntimeOverloadCapturesFlagsAtRegistrationAndRetainsCompatibility(bool microsoft)
    {
        var names = new[] { RuntimeStartupSettings.LegacyValidationKey, RuntimeStartupSettings.ValidationKey, UnitParentProcessEventsHandler.ControlPipeKey };
        var original = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(RuntimeStartupSettings.LegacyValidationKey, "1");
            Environment.SetEnvironmentVariable(RuntimeStartupSettings.ValidationKey, null);
            Environment.SetEnvironmentVariable(UnitParentProcessEventsHandler.ControlPipeKey, null);
            var launch = Preview();
            await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
            {
                context.UseAether(options =>
                {
                    options.HostContractId = launch.Identity.HostContractId;
                    options.GameBuildId = launch.Identity.GameBuildId;
                    options.ReportDirectory = launch.ReportDirectory;
                });
                context.UseAetherPreviewRuntime(launch, ParentId);
                Environment.SetEnvironmentVariable(RuntimeStartupSettings.LegacyValidationKey, "0");
                Environment.SetEnvironmentVariable(RuntimeStartupSettings.ValidationKey, "false");
            }).Build();
            Assert.True(Assert.IsType<RuntimeStartupSettings>(app.Services.GetService(typeof(RuntimeStartupSettings))).GpuValidation);
            Assert.False(RuntimeStartupSettings.ReadEnvironment().GpuValidation);
            Assert.False(Directory.Exists(launch.ReportDirectory));
        }
        finally
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, original[name]);
        }
    }

    private static int ParentId => Environment.ProcessId == 1 ? 2 : 1;
    private static SessionLaunch Preview()
    {
        var entry = Assembly.GetEntryAssembly()!.Location;
        return new(new(Guid.NewGuid(), 1, BuildIdentity.HostId, BuildIdentity.PreviewId, new string('a', 64)), "engine-core75-settings",
            Path.Combine(Path.GetTempPath(), "core75-runtime-" + Guid.NewGuid().ToString("N")), Role: RuntimeRole.EditPreview)
        { RuntimeIdentity = new(entry, entry) };
    }
    private static Dictionary<string, string?> EnvironmentFor(SessionLaunch launch) => new()
    {
        [NativeRuntimeFactory.BootstrapKey] = RuntimeBootstrap.Encode(launch),
        ["-p"] = ParentId.ToString(CultureInfo.InvariantCulture)
    };
}
