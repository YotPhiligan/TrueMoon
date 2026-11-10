using System.Text.Json;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Tests.Configuration;

public sealed class ConfigurationSourceTests
{
    [Fact]
    public async Task JsonFlattensNestedSectionsAndKeepsArraysAsOneTypedValue()
    {
        using var directory = new TestDirectory();
        var file = directory.Write("settings.json", "{\"Editor\":{\"Count\":\"14\",\"Name\":null,\"Tags\":[\"a,b\",\"c\"]}}");
        var profile = Profile(builder => builder.AddJsonFile("settings", file));
        var snapshot = await profile.LoadAsync();
        Assert.Equal(14, snapshot.Read<int>("editor:count").Value);
        Assert.Equal(ConfigurationReadState.Null, snapshot.Read<string?>("Editor:Name").State);
        Assert.Equal(new[] { "a,b", "c" }, snapshot.Read<string[]>("Editor:Tags").Value);
        Assert.Equal("settings", snapshot.Read<int>("Editor:Count").Origin!.SourceId);
        Assert.Equal(ConfigurationSourcePriority.Files, snapshot.Read<int>("Editor:Count").Origin!.Priority);
        Assert.Equal("{\"Editor\":{\"Count\":\"14\",\"Name\":null,\"Tags\":[\"a,b\",\"c\"]}}", File.ReadAllText(file));
    }

    [Fact]
    public async Task OptionalAbsenceUsesDefaultsButRequiredAbsenceFails()
    {
        using var directory = new TestDirectory();
        var absent = Path.Combine(directory.Path, "missing", "settings.json");
        Assert.Equal(7, (await Profile(builder => builder.AddJsonFile("optional", absent, optional: true)).LoadAsync()).Read<int>("Editor:Count").Value);
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Profile(builder => builder.AddJsonFile("required", absent)).LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.SourceId == "required");
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"Editor\":{\"Count\":1,\"count\":2}}")]
    [InlineData("{\"Editor:Count\":1}")]
    [InlineData("{\"Editor\":{\"Unknown\":1}}")]
    [InlineData("{\"Editor\":{\"Count\":\"credential-do-not-echo\"}}")]
    public async Task ExistingInvalidJsonNeverUsesOptionalFallbackOrLeaksValue(string json)
    {
        using var directory = new TestDirectory();
        var file = directory.Write("bad.json", json);
        var profile = Profile(builder => builder.AddJsonFile("file", file, optional: true));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.SourceId == "file");
        Assert.DoesNotContain("credential-do-not-echo", error.ToString());
        Assert.Equal(json, File.ReadAllText(file));
    }

    [Fact]
    public async Task ExistingDirectoryIsAnIoFailureEvenForOptionalFile()
    {
        using var directory = new TestDirectory();
        var profile = Profile(builder => builder.AddJsonFile("not-file", directory.Path, optional: true));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.SourceId == "not-file");
    }

    [Fact]
    public void JsonPathsMustBeExplicitAndAbsolute()
        => Assert.ThrowsAny<ArgumentException>(() => Profile(builder => builder.AddJsonFile("file", "relative-settings.json")));

    [Fact]
    public async Task EnvironmentMapsPrefixAndDoubleUnderscoreButPreservesSingleUnderscore()
    {
        var variables = new Dictionary<string, string?>
        {
            ["MYAPP_EDITOR__COUNT"] = "23",
            ["MYAPP_EDITOR__PROJECT_PATH"] = "kept",
            ["OTHER_EDITOR__COUNT"] = "99",
            ["PATH"] = "unrelated"
        };
        var source = new ConfigurationEnvironmentSource("MYAPP_", variables: () => variables);
        var profile = Profile(builder => builder.AddSource("env", ConfigurationSourcePriority.Environment, source));
        var snapshot = await profile.LoadAsync();
        Assert.Equal(23, snapshot.Read<int>("Editor:Count").Value);
        Assert.Equal("kept", snapshot.Read<string>("Editor:Project_Path").Value);
        Assert.Equal(ConfigurationReadState.Missing, snapshot.Read<string>("PATH").State);
        Assert.Equal("env", snapshot.Read<int>("Editor:Count").Origin!.SourceId);
    }

    [Fact]
    public async Task EnvironmentAllowlistFiltersBeforeUnknownKeyValidationAndIsCopied()
    {
        var allowlist = new[] { "editor:COUNT" };
        var variables = new Dictionary<string, string?>
        {
            ["MYAPP_EDITOR__COUNT"] = "31",
            ["MYAPP_EDITOR__UNKNOWN"] = "hidden"
        };
        var source = new ConfigurationEnvironmentSource("MYAPP_", allowlist, () => variables);
        allowlist[0] = "Editor:Unknown";
        var snapshot = await Profile(builder => builder.AddSource("env", ConfigurationSourcePriority.Environment, source)).LoadAsync();
        Assert.Equal(31, snapshot.Read<int>("Editor:Count").Value);
        Assert.Equal(ConfigurationReadState.Missing, snapshot.Read<string>("Editor:Unknown").State);
    }

    [Fact]
    public async Task EnvironmentRejectsUnknownSelectedKeyAndAmbiguousNormalizedNames()
    {
        var unknown = new ConfigurationEnvironmentSource("MYAPP_", variables: () => [new("MYAPP_EDITOR__UNKNOWN", "secret")]);
        var unknownProfile = Profile(builder => builder.AddSource("unknown", ConfigurationSourcePriority.Environment, unknown));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => unknownProfile.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.SourceId == "unknown");
        var ambiguous = new ConfigurationEnvironmentSource("MYAPP_", variables: () =>
            [new("MYAPP_EDITOR__COUNT", "11"), new("MYAPP_editor__count", "22")]);
        var ambiguousProfile = Profile(builder => builder.AddSource("collision", ConfigurationSourcePriority.Environment, ambiguous));
        var collision = await Assert.ThrowsAsync<ConfigurationException>(() => ambiguousProfile.LoadAsync().AsTask());
        Assert.Contains(collision.Issues, issue => issue.SourceId == "collision");
    }

    [Fact]
    public async Task EnvironmentIsRecapturedOnlyWhenExplicitlyLoaded()
    {
        var variables = new Dictionary<string, string?> { ["MYAPP_EDITOR__COUNT"] = "11" };
        var captures = 0;
        var source = new ConfigurationEnvironmentSource("MYAPP_", variables: () => { captures++; return variables; });
        var profile = Profile(builder => builder.AddSource("env", ConfigurationSourcePriority.Environment, source));
        var first = await profile.LoadAsync();
        variables["MYAPP_EDITOR__COUNT"] = "22";
        Assert.Equal(11, first.Read<int>("Editor:Count").Value);
        Assert.Equal(1, captures);
        var second = await profile.LoadAsync();
        Assert.Equal(22, second.Read<int>("Editor:Count").Value);
        Assert.Equal(2, captures);
        Assert.Equal(11, first.Read<int>("Editor:Count").Value);
    }

    [Fact]
    public void CaptureReadsOnlyRequestedNamesAndRetainsMissingAsNull()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var selected = "TM_CORE71_SELECTED_" + suffix;
        var unrelated = "TM_CORE71_UNRELATED_" + suffix;
        var missing = "TM_CORE71_MISSING_" + suffix;
        Environment.SetEnvironmentVariable(selected, "first");
        Environment.SetEnvironmentVariable(unrelated, "hidden");
        try
        {
            var snapshot = ConfigurationEnvironment.Capture(selected, missing);
            Environment.SetEnvironmentVariable(selected, "second");
            Assert.Equal(2, snapshot.Count);
            Assert.Equal("first", snapshot[selected]);
            Assert.Null(snapshot[missing]);
            Assert.False(snapshot.ContainsKey(unrelated));
        }
        finally
        {
            Environment.SetEnvironmentVariable(selected, null);
            Environment.SetEnvironmentVariable(unrelated, null);
        }
    }

    [Fact]
    public void EnvironmentRequiresPrefixAndValidLogicalAllowlist()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConfigurationEnvironmentSource(""));
        Assert.ThrowsAny<ArgumentException>(() => new ConfigurationEnvironmentSource("MYAPP_", ["Editor::Count"]));
    }

    private static IConfigurationProfile Profile(Action<IConfigurationProfileBuilder> configure)
        => ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(ConfigurationSchemas.Create("Editor", () => new Options(),
                value => value with { Tags = value.Tags.ToArray() }, _ => { }, fields =>
                {
                    fields.Field("Count", value => value.Count, (value, count) => value.Count = count);
                    fields.Field("Name", value => value.Name, (value, name) => value.Name = name);
                    fields.Field("Tags", value => value.Tags, (value, tags) => value.Tags = tags);
                    fields.Field("Project_Path", value => value.ProjectPath, (value, path) => value.ProjectPath = path);
                }));
            configure(builder);
        });

    private sealed record Options
    {
        public int Count { get; set; } = 7;
        public string? Name { get; set; }
        public string[] Tags { get; set; } = [];
        public string ProjectPath { get; set; } = "default";
    }

    private sealed class TestDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tm-core71-" + Guid.NewGuid().ToString("N"));
        internal TestDirectory() => Directory.CreateDirectory(Path);
        internal string Write(string name, string content)
        {
            var file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, content);
            return file;
        }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
