using System.Security.Cryptography;
using System.Text.Json;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Player.Scenarios;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Checkpoints;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;
using Xunit;

namespace TrueMoon.Aether.Player.Tests;

public sealed class PlayerScenarioCheckpointTests
{
    [Fact]
    public async Task Resume_SkipsCompletedCheckpointsAndRetainsHeldInputWithoutRepeatingPulses()
    {
        await WithScenarioAsync("""
            {"formatVersion":1,"maxTicks":4,"timeoutMs":15000,
             "input":[{"tick":2,"state":{"captured":true,"buttons":"Forward","yaw":0.3,"pitch":0.2,"interactPressed":true}},
                      {"tick":1,"state":{"captured":true,"buttons":"Left"}}],
             "checkpoints":[{"id":"before-saved-tick","tick":1},
                            {"id":"saved-tick","tick":2,"expectedInput":"Accepted"},
                            {"id":"final-tick","tick":4,"expectedInput":"Accepted"}]}
            """, async (root, artifact, scene, scenario) =>
        {
            var scenarioHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(scenario)));
            var sceneHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(scene)));
            byte[]? savedBytes = null;
            using var initial = await RunAsync(root, artifact, scene, scenario, "initial", 0,
                new(SaveAtTick: 2, Save: (_, _, checkpoint) => savedBytes = GameCheckpointCodec.Encode(checkpoint)));
            Assert.Equal(2, initial.RootElement.GetProperty("savedAtTick").GetInt32());
            Assert.Equal(JsonValueKind.Null, initial.RootElement.GetProperty("resumedFromTick").ValueKind);
            var saved = GameCheckpointCodec.Decode(Assert.IsType<byte[]>(savedBytes));
            Assert.True(saved.State.Interaction);

            GameCheckpoint? uninterrupted = null;
            using var control = await RunAsync(root, artifact, scene, scenario, "control", 0,
                new(SaveAtTick: 3, Save: (_, _, checkpoint) => uninterrupted = checkpoint));
            var expected = Assert.IsType<GameCheckpoint>(uninterrupted);
            Assert.NotEqual(saved.State.Camera.Z, expected.State.Camera.Z);
            Assert.Equal(saved.State.Camera.Yaw, expected.State.Camera.Yaw);
            Assert.Equal(saved.State.Camera.Pitch, expected.State.Camera.Pitch);
            Assert.True(expected.State.Interaction);

            GameCheckpoint? continued = null;
            var loads = 0;
            using var resumed = await RunAsync(root, artifact, scene, scenario, "resumed", 0,
                new(Load: (start, hash) =>
                {
                    loads++;
                    Assert.Equal(scenarioHash, hash);
                    Assert.Equal(sceneHash, start.SceneHash);
                    Assert.Equal(artifact.BuildId, start.Artifact.BuildId);
                    Assert.Null(start.Checkpoint);
                    return GameCheckpointCodec.Decode(savedBytes!);
                }, SaveAtTick: 3, Save: (_, _, checkpoint) => continued = checkpoint));

            Assert.Equal(1, loads);
            var report = resumed.RootElement;
            Assert.Equal(1, report.GetProperty("formatVersion").GetInt32());
            Assert.Equal(2, report.GetProperty("resumedFromTick").GetInt32());
            Assert.Equal(3, report.GetProperty("savedAtTick").GetInt32());
            var observed = Assert.Single(report.GetProperty("checkpoints").EnumerateArray());
            Assert.Equal(4, observed.GetProperty("tick").GetInt32());
            Assert.Equal("final-tick", observed.GetProperty("requested").GetProperty("id").GetString());
            Assert.Equal(expected.State, Assert.IsType<GameCheckpoint>(continued).State);
        });
    }

    [Theory]
    [InlineData(false, 2, 0)]
    [InlineData(true, 5, 1)]
    public async Task Save_IsNotReportedAfterFailedExpectationsOrPersistence(bool expectationsPass, int exitCode, int saveCalls)
    {
        var expectedHud = expectationsPass ? "null" : "\"unreachable\"";
        await WithScenarioAsync($$"""
            {"formatVersion":1,"maxTicks":2,"timeoutMs":15000,"input":[],
             "checkpoints":[{"id":"save","tick":1,"expectedHudStatus":{{expectedHud}}}]}
            """, async (root, artifact, scene, scenario) =>
        {
            var calls = 0;
            using var result = await RunAsync(root, artifact, scene, scenario, "failed", exitCode,
                new(SaveAtTick: 1, Save: (_, _, _) =>
                {
                    calls++;
                    throw new IOException("persistence marker");
                }));
            Assert.Equal(saveCalls, calls);
            Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("savedAtTick").ValueKind);
            Assert.Contains(expectationsPass ? "persistence marker" : "Expectation failed", result.RootElement.GetProperty("error").GetString()!);
        });
    }

    [Theory]
    [InlineData(0, null, "Resume tick must be positive")]
    [InlineData(4, null, "precede the final")]
    [InlineData(2, 2, "Save tick must follow")]
    public async Task InvalidResumeOrSaveBoundary_IsRejectedBeforeStartingThePlayer(int savedTick, int? saveAtTick, string error)
    {
        await WithScenarioAsync("""
            {"formatVersion":1,"maxTicks":4,"timeoutMs":15000,"input":[],"checkpoints":[{"id":"final","tick":4}]}
            """, async (root, artifact, scene, scenario) =>
        {
            var loads = 0;
            using var result = await RunAsync(root, artifact, scene, scenario, "invalid", 5,
                new(Load: (start, _) =>
                {
                    loads++;
                    return new(artifact.HostContractId, artifact.BuildId, start.Snapshot!.Id, start.Snapshot.Revision,
                        new((ulong)savedTick, default, false));
                }, SaveAtTick: saveAtTick, Save: saveAtTick.HasValue ? (_, _, _) => throw new Exception("Unexpected save") : null));
            var report = result.RootElement;
            Assert.Equal(1, loads);
            Assert.Contains(error, report.GetProperty("error").GetString()!);
            Assert.Equal(JsonValueKind.Null, report.GetProperty("state").ValueKind);
            Assert.Equal(JsonValueKind.Null, report.GetProperty("resumedFromTick").ValueKind);
            Assert.Equal(JsonValueKind.Null, report.GetProperty("savedAtTick").ValueKind);
        });
    }

    private static async Task<JsonDocument> RunAsync(string root, GameBuildArtifact artifact, string scene, string scenario,
        string output, int expectedCode, PlayerScenarioCheckpointOptions options)
    {
        var directory = Path.Combine(root, output);
        var exitCode = await PlayerScenarioRunner.RunAsync(scenario, artifact.DirectoryPath, scene, directory,
            legacyScene: true, createWindowFonts: null, configureDiagnostics: null, cancellationToken: default, checkpointOptions: options);
        var report = await File.ReadAllTextAsync(Path.Combine(directory, "report.json"));
        Assert.True(exitCode == expectedCode, report);
        return JsonDocument.Parse(report);
    }

    private static async Task WithScenarioAsync(string json, Func<string, GameBuildArtifact, string, string, Task> run)
    {
        var root = Directory.CreateTempSubdirectory("TrueMoon-scenario-resume-").FullName;
        try
        {
            var artifact = GameArtifactPublisher.Publish(Path.GetDirectoryName(typeof(CameraGame).Assembly.Location)!,
                Path.Combine(root, "deployment"), BuildIdentity.HostId, "Game.Logic.dll", typeof(CameraGame).FullName!, 1, runtimeAccess: true);
            var scene = Path.Combine(root, "scene.json");
            await File.WriteAllTextAsync(scene, "{\"formatVersion\":1,\"id\":\"legacy\",\"revision\":7,\"entities\":[{\"id\":\"switch\",\"interactive\":true}]}");
            var scenario = Path.Combine(root, "scenario.json");
            await File.WriteAllTextAsync(scenario, json);
            await run(root, artifact, scene, scenario);
        }
        finally
        {
            // Collectible game assemblies may keep their mapped DLLs until collection on Windows.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Directory.Delete(root, recursive: true);
        }
    }
}
