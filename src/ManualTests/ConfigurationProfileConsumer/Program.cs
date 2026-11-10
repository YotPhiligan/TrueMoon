using System.Globalization;
using System.Text.Json;
using TrueMoon.Configuration;

namespace ConfigurationProfilesConsumer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var report = Path.GetFullPath(args.Single());
        Directory.CreateDirectory(report);
        var checks = 0;
        void Check(bool condition) { if (!condition) throw new InvalidOperationException("Configuration consumer check failed."); checks++; }
        var prefix = "CORE71_" + Guid.NewGuid().ToString("N") + "_";
        Environment.SetEnvironmentVariable(prefix + "WORKER__BATCH", "23");
        try
        {
            var file = Path.Combine(report, "settings.json");
            await File.WriteAllTextAsync(file, "{\"Worker\":{\"Batch\":17,\"Tags\":[\"file\"]}}");
            var schema = ConfigurationSchemas.Create("Worker", () => new Options(),
                value => value with { Tags = value.Tags.ToArray() },
                value => { if (value.Batch <= 0) throw new ArgumentOutOfRangeException(nameof(value.Batch)); }, fields =>
                {
                    fields.Field("Batch", value => value.Batch, (value, count) => value.Batch = count, new(Aliases: ["--batch", "-b"]));
                    fields.Field("Tags", value => value.Tags, (value, tags) => value.Tags = tags,
                        new(Aliases: ["--tag"], Argument: ConfigurationArgumentKind.Repeated));
                    fields.Field("Enabled", value => value.Enabled, (value, enabled) => value.Enabled = enabled,
                        new(Aliases: ["--enabled"], Argument: ConfigurationArgumentKind.Flag));
                    fields.Field("Optional", value => value.Optional, (value, number) => value.Optional = number);
                    fields.Field("Port", value => value.Port, (value, port) => value.Port = port, codec: new PortCodec());
                });
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(schema);
                builder.AddArguments("cli", ["--batch=31", "--tag=a,b", "--tag=c", "--enabled"]);
                builder.AddEnvironment("env", prefix);
                builder.AddJsonFile("json", file);
            });
            var snapshot = await profile.LoadAsync();
            Check(snapshot.Generation == 1);
            Check(snapshot.Read<int>("worker:BATCH").Value == 31);
            Check(snapshot.Read<int>("Worker:Batch").Origin?.SourceId == "cli");
            Check(snapshot.Read<string[]>("Worker:Tags").Value!.SequenceEqual(["a,b", "c"]));
            Check(snapshot.Read<bool>("Worker:Enabled").Value);
            Check(snapshot.Read<int?>("Worker:Optional").State == ConfigurationReadState.Null);
            Check(snapshot.Read<int>("Batch").State == ConfigurationReadState.Missing);
            Check(snapshot.Read<Port>("Worker:Port").Value.Value == 80);
            var options = schema.Bind(snapshot);
            options.Tags[0] = "consumer mutation";
            Check(snapshot.Read<string[]>("Worker:Tags").Value![0] == "a,b");
            var overrides = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(schema); builder.AddArguments("cli", ["--batch=19"]);
                builder.Override("Worker:Batch", 47);
            });
            Check((await overrides.LoadAsync()).Read<int>("Worker:Batch").Value == 47);
            await File.WriteAllTextAsync(file, "{broken");
            try { await profile.LoadAsync(); throw new InvalidOperationException("Broken JSON was accepted."); }
            catch (ConfigurationException error) { Check(error.Issues.Any(issue => issue.SourceId == "json")); }
            Check(snapshot.Read<int>("Worker:Batch").Value == 31);
            var parsed = ConfigurationCommandLine.Parse(["-b", "-12", "--", "--unknown"], schema.Keys);
            Check(parsed.Entries.Single().Value.GetString() == "-12" && parsed.Positionals.Single() == "--unknown");
            try { ConfigurationCommandLine.Parse(["--unregistered=hidden-value"], schema.Keys); throw new InvalidOperationException("Unknown option was accepted."); }
            catch (ConfigurationException error) { Check(!error.ToString().Contains("hidden-value") && error.Issues.All(issue => !issue.Message.Contains("hidden-value"))); }
            var legacy = new ConfigurationBuilder().Build();
            legacy.Set("core71-consumer", "legacy-kept");
            Check(legacy.Get<string>("core71-consumer") == "legacy-kept");
            await File.WriteAllTextAsync(Path.Combine(report, "verification.json"), JsonSerializer.Serialize(new
            {
                Completed = true, Checks = checks, GpuRun = false, WindowRun = false,
                ConfigurationAssembly = typeof(ConfigurationProfiles).Assembly.Location,
                ContractsAssembly = typeof(IConfigurationProfile).Assembly.Location
            }));
            Console.WriteLine($"Configuration profile: {checks} checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Environment.SetEnvironmentVariable(prefix + "WORKER__BATCH", null); }
    }

    private sealed record Options
    {
        public int Batch { get; set; } = 7;
        public string[] Tags { get; set; } = [];
        public bool Enabled { get; set; }
        public int? Optional { get; set; }
        public Port Port { get; set; } = new(80);
    }
    private readonly record struct Port(int Value);
    private sealed class PortCodec : IConfigurationValueCodec<Port>
    {
        public Port Read(JsonElement value) => new(int.Parse(value.GetString()![2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        public JsonElement Write(Port value) => JsonSerializer.SerializeToElement("0x" + value.Value.ToString("x", CultureInfo.InvariantCulture));
    }
}
