using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Tests.Configuration;

public sealed class ConfigurationCommandLineTests
{
    [Fact]
    public void AliasesInlineValuesNegativeNumbersFlagsAndDelimiterCompose()
    {
        var parsed = ConfigurationCommandLine.Parse(
            ["--NAME=a=b", "--count", "-12", "-V", "--", "--unknown", "-99"], Keys());

        Assert.Equal("a=b", Entry(parsed, "Editor:Name").Value.GetString());
        Assert.Equal("-12", Entry(parsed, "Editor:Count").Value.ToString());
        Assert.Equal("true", Entry(parsed, "Editor:Enabled").Value.ToString().ToLowerInvariant());
        Assert.Equal(new[] { "--unknown", "-99" }, parsed.Positionals);
        Assert.Equal(3, parsed.Entries.Count);
    }

    [Fact]
    public void CanonicalKeysAndAliasesShareOneLastScalarValue()
    {
        var parsed = ConfigurationCommandLine.Parse(["--name", "first", "--EDITOR:NAME=second", "--Name=last"], Keys());
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal("Editor:Name", entry.Path);
        Assert.Equal("last", entry.Value.GetString());
        Assert.Empty(parsed.Positionals);
    }

    [Fact]
    public void RepeatedCollectionsPreserveOccurrenceOrderWithoutCommaSplitting()
    {
        var parsed = ConfigurationCommandLine.Parse(["--tag", "one,two", "--tag=three", "--Editor:Tags", "four"], Keys());
        var values = Entry(parsed, "Editor:Tags").Value.EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Equal(new[] { "one,two", "three", "four" }, values);
        Assert.Single(parsed.Entries);
    }

    [Fact]
    public void FlagsDoNotConsumeNextPositionalAndInlineFalseIsExplicit()
    {
        var bare = ConfigurationCommandLine.Parse(["--enabled", "false"], Keys());
        Assert.Equal("true", Entry(bare, "Editor:Enabled").Value.ToString().ToLowerInvariant());
        Assert.Equal(new[] { "false" }, bare.Positionals);
        var explicitFalse = ConfigurationCommandLine.Parse(["--enabled=false"], Keys());
        Assert.Equal("false", Entry(explicitFalse, "Editor:Enabled").Value.ToString().ToLowerInvariant());
        Assert.Empty(explicitFalse.Positionals);
    }

    [Fact]
    public void EmptyInlineStringAndDashPrefixedInlineStringAreValues()
    {
        var empty = ConfigurationCommandLine.Parse(["--name="], Keys());
        Assert.Equal("", Entry(empty, "Editor:Name").Value.GetString());
        var dash = ConfigurationCommandLine.Parse(["--name=-ambiguous"], Keys());
        Assert.Equal("-ambiguous", Entry(dash, "Editor:Name").Value.GetString());
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--unknown=credential-do-not-echo")]
    [InlineData("-z")]
    public void UnknownOptionsFailWithoutEchoingRawValues(string argument)
    {
        var error = Assert.Throws<ConfigurationException>(() => ConfigurationCommandLine.Parse([argument], Keys()));
        Assert.NotEmpty(error.Issues);
        Assert.DoesNotContain("credential-do-not-echo", error.ToString());
    }

    [Theory]
    [InlineData("--name")]
    [InlineData("--count")]
    [InlineData("--tag")]
    public void MissingRequiredValueIsNotTreatedAsNullFlag(string option)
    {
        var error = Assert.Throws<ConfigurationException>(() => ConfigurationCommandLine.Parse([option], Keys()));
        Assert.NotEmpty(error.Issues);
    }

    [Theory]
    [InlineData("--name", "--enabled")]
    [InlineData("--name", "-ambiguous")]
    [InlineData("--count", "--enabled")]
    public void ValueOptionsDoNotConsumeAnotherOptionOrAmbiguousDashString(string option, string following)
        => Assert.Throws<ConfigurationException>(() => ConfigurationCommandLine.Parse([option, following], Keys()));

    [Fact]
    public void EmptyInputAndPositionalOnlyInputRemainDistinct()
    {
        var empty = ConfigurationCommandLine.Parse([], Keys());
        Assert.Empty(empty.Entries);
        Assert.Empty(empty.Positionals);
        var positions = ConfigurationCommandLine.Parse(["input.file", "--", "--name", "value"], Keys());
        Assert.Empty(positions.Entries);
        Assert.Equal(new[] { "input.file", "--name", "value" }, positions.Positionals);
    }

    [Fact]
    public void MetadataRejectsCaseInsensitiveAliasAndPathCollisions()
    {
        var duplicateAlias = Keys().Concat([new ConfigurationKey("Other:Name", typeof(string), new(Aliases: ["--NAME"]))]).ToArray();
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([], duplicateAlias));
        var duplicatePath = Keys().Concat([new ConfigurationKey("editor:NAME", typeof(string), new())]).ToArray();
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([], duplicatePath));
    }

    [Fact]
    public void MetadataRejectsIncompatibleArgumentShapesAndUnknownPolicies()
    {
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([],
            [new("Editor:Count", typeof(int), new(Argument: ConfigurationArgumentKind.Flag))]));
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([],
            [new("Editor:Name", typeof(string), new(Argument: ConfigurationArgumentKind.Repeated))]));
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([],
            [new("Editor:Name", typeof(string), new(Change: (ConfigurationChangeMode)999))]));
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([],
            [new("Editor:Name", typeof(string), new(Argument: (ConfigurationArgumentKind)999))]));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("--name=value")]
    [InlineData("--has space")]
    public void InvalidAliasTokensAreRejectedBeforeAnyInputIsParsed(string alias)
        => Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([],
            [new("Editor:Name", typeof(string), new(Aliases: [alias]))]));

    [Fact]
    public void ResultCollectionsCannotBeChangedThroughOriginalArgumentsOrMetadata()
    {
        var aliases = new[] { "--name" };
        var key = new ConfigurationKey("Editor:Name", typeof(string), new(Aliases: aliases));
        aliases[0] = "--changed";
        var arguments = new[] { "--name=original", "--", "positional" };
        var parsed = ConfigurationCommandLine.Parse(arguments, [key]);
        arguments[0] = "--name=mutated";
        arguments[2] = "mutated";
        Assert.Equal("original", Assert.Single(parsed.Entries).Value.GetString());
        Assert.Equal(new[] { "positional" }, parsed.Positionals);
    }

    [Fact]
    public async Task SettingsArgumentsRejectPositionalsAndCaptureTheirInput()
    {
        var schema = ConfigurationSchemas.Create("Editor", () => new Options(), value => value with { }, _ => { },
            fields => fields.Field("Name", value => value.Name, (value, name) => value.Name = name, new(Aliases: ["--name"])));
        var arguments = new[] { "--name=before" };
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.AddArguments("cli", arguments); });
        arguments[0] = "--name=after";
        Assert.Equal("before", (await profile.LoadAsync()).Read<string>("Editor:Name").Value);
        var commandArguments = ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.AddArguments("cli", ["input.file"]); });
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => commandArguments.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.SourceId == "cli");
    }

    private static ConfigurationEntry Entry(ConfigurationCommandLineResult result, string key)
        => Assert.Single(result.Entries, entry => entry.Path == key);

    private static ConfigurationKey[] Keys() =>
    [
        new("Editor:Name", typeof(string), new(Aliases: ["--name"])),
        new("Editor:Count", typeof(int), new(Aliases: ["--count"])),
        new("Editor:Enabled", typeof(bool), new(Aliases: ["--enabled", "-v"], Argument: ConfigurationArgumentKind.Flag)),
        new("Editor:Tags", typeof(string[]), new(Aliases: ["--tag"], Argument: ConfigurationArgumentKind.Repeated))
    ];

    private sealed record Options { public string Name { get; set; } = "default"; }
}
