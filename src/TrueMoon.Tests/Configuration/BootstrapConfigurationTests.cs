using System.Text.Json;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Tests.Configuration;

public sealed class BootstrapConfigurationTests
{
    private static readonly ConfigurationKey[] Keys =
    [
        new("Boot:Count", typeof(int), new(Aliases: ["-c"])),
        new("Boot:Enabled", typeof(bool), new(Aliases: ["-e"], Argument: ConfigurationArgumentKind.Flag)),
        new("Boot:Tags", typeof(string[]), new(Aliases: ["-t"], Argument: ConfigurationArgumentKind.Repeated))
    ];

    [Fact]
    public void SelectedParserRetainsUnknownArgumentsAndUsesCommonKnownGrammar()
    {
        var result = ConfigurationCommandLine.ParseKnown(
            ["--foreign=secret", "loose", "-c", "-12", "-e", "-t=first", "--BOOT:COUNT=21", "-t", "second", "--", "-c=99"], Keys);

        Assert.Equal(new[] { "--foreign=secret", "loose", "-c=99" }, result.Positionals);
        Assert.Equal("21", Entry(result, "Boot:Count").GetString());
        Assert.True(Entry(result, "Boot:Enabled").GetBoolean());
        Assert.Equal(new[] { "first", "second" }, Entry(result, "Boot:Tags").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(3, result.Entries.Count);
    }

    [Fact]
    public void UnknownOptionDoesNotConsumeFollowingKnownOption()
    {
        var result = ConfigurationCommandLine.ParseKnown(["--foreign", "foreign-value", "-c=-7"], Keys);
        Assert.Equal(new[] { "--foreign", "foreign-value" }, result.Positionals);
        Assert.Equal("-7", Assert.Single(result.Entries).Value.GetString());
    }

    [Theory]
    [InlineData("-c")]
    [InlineData("-c", "--foreign=secret")]
    [InlineData("-c", "--")]
    public void SelectedParserStillRejectsMissingKnownValue(params string[] arguments)
    {
        var error = Assert.Throws<ConfigurationException>(() => ConfigurationCommandLine.ParseKnown(arguments, Keys));
        Assert.Contains(error.Issues, issue => issue.Code == "MissingArgument" && issue.Path == "Boot:Count");
        Assert.DoesNotContain("secret", error.ToString());
    }

    [Fact]
    public void StrictParserStillRejectsUnknownOption()
    {
        var error = Assert.Throws<ConfigurationException>(() => ConfigurationCommandLine.Parse(["--foreign=secret"], Keys));
        Assert.Contains(error.Issues, issue => issue.Code == "UnknownOption");
        Assert.All(error.Issues, issue => Assert.DoesNotContain("secret", issue.Message));
    }

    [Fact]
    public void PathArgumentsUseSelectedParserAndLastKnownRootWinsWithoutCreatingFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "truemoon-core74-never-created-" + Guid.NewGuid().ToString("N"));
        var resolver = new PathResolver(["--foreign=ignored", "-root=unused", "--Paths:Root", root]);

        Assert.Equal(Path.GetFullPath(root), resolver.ResolvePath(Paths.Root));
        Assert.Equal(Path.Combine(root, "Configuration"), resolver.ResolvePath(Paths.Configuration));
        Assert.Equal(Path.Combine(root, "Assets"), resolver.ResolvePath(Paths.Assets));
        Assert.Equal(Path.Combine(root, "Data"), resolver.ResolvePath(Paths.Data));
        Assert.Equal(Path.Combine(root, "Logs"), resolver.ResolvePath(Paths.Logs));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void TypedPathSettingsAreCapturedAndNormalized()
    {
        var relative = Path.Combine("core74", "..", "core74-root");
        var settings = new PathSettings { Root = relative };
        var resolver = new PathResolver(settings);
        settings.Root = "different";
        Assert.Equal(Path.GetFullPath(relative), resolver.ResolvePath(Paths.Root));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\u0000")]
    public void InvalidWinningRootKeepsLegacyArgumentException(string invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => new PathResolver(["-root=valid", "-root=" + invalid]));
    }

    [Fact]
    public async Task ExplicitValuesSourceCopiesCallerEntriesBeforeLoading()
    {
        var entries = new List<ConfigurationEntry> { new("App:Name", JsonSerializer.SerializeToElement("copied")) };
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(CoreConfigurationSchemas.App);
            builder.AddValues("explicit", ConfigurationSourcePriority.Arguments, entries);
        });
        entries.Clear();
        var snapshot = await profile.LoadAsync();
        Assert.Equal("copied", CoreConfigurationSchemas.App.Bind(snapshot).Name);
        Assert.Equal("explicit", snapshot.Read<string>("App:Name").Origin!.SourceId);
        Assert.Equal("copied", CoreConfigurationSchemas.App.Bind(await profile.LoadAsync()).Name);
    }

    private static JsonElement Entry(ConfigurationCommandLineResult result, string path)
        => Assert.Single(result.Entries, entry => entry.Path == path).Value;
}
