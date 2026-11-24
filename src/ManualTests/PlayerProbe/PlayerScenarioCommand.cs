using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Player;
using TrueMoon.Aether.Player.Scenarios;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.ManualTests.PlayerProbe;

internal static class PlayerScenarioCommand
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<GameButtons>(), new JsonStringEnumConverter<GameInputOutcome>(),
            new JsonStringEnumConverter<ObservationOutcome>() },
        WriteIndented = true
    };

    internal static async Task<int> RunAsync(string scenarioPath, string build, string scene, string output, bool legacyScene, bool headless)
    {
        if (Directory.Exists(output))
        {
            Console.Error.WriteLine("Select a fresh scenario output directory.");
            return 5;
        }
        Directory.CreateDirectory(output);
        using var reservation = new FileStream(Path.Combine(output, ".run"), FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 1, FileOptions.DeleteOnClose);

        var observations = new List<Observation>();
        using var interrupted = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource();
        using var run = CancellationTokenSource.CreateLinkedTokenSource(interrupted.Token, timeout.Token);
        Console.CancelKeyPress += OnCancel;
        PlayerStart? start = null;
        Scenario? scenario = null;
        string? scenarioHash = null, error = null, cleanupError = null;
        var result = 0;
        PlayerSession? session = null;
        HostedUiWindow? window = null;
        try
        {
            await using var inputFile = File.OpenRead(scenarioPath);
            if (inputFile.Length is <= 0 or > 1024 * 1024) throw new InvalidDataException("Invalid scenario file size.");
            var bytes = new byte[checked((int)inputFile.Length)];
            await inputFile.ReadExactlyAsync(bytes, run.Token);
            scenarioHash = Convert.ToHexString(SHA256.HashData(bytes));
            var offset = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
            scenario = JsonSerializer.Deserialize<Scenario>(bytes.AsSpan(offset), Json) ?? throw new InvalidDataException("Missing scenario.");
            Validate(scenario);
            timeout.CancelAfter(scenario.TimeoutMs);
            start = await PlayerStart.CaptureAsync(build, BuildIdentity.HostId, scene, legacyScene, run.Token);
            var app = App.Build(context =>
            {
                context.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = Path.Combine(output, "diagnostics") });
                if (headless)
                {
                    context.UseAetherHeadlessPlayer(start);
                }
                else
                {
                    var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
                    {
                        ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
                    });
                    context.UseAetherPlayer(start, fonts, player =>
                    {
                        player.Width = scenario.Width;
                        player.Height = scenario.Height;
                        player.Title = "Aether Player Scenario";
                        player.Validation = scenario.Validation;
                        player.ManualTicks = true;
                    });
                }
            });
            try
            {
                if (!headless)
                {
                    window = app.Services.Resolve<HostedUiWindow>() ?? throw new InvalidOperationException("Hosted window is unavailable.");
                    window.Closing += OnWindowClosing;
                }
                var player = app.Services.Resolve<IPlayerScenarioDriver>() ?? throw new InvalidOperationException("Player scenario is unavailable.");
                session = app.Services.Resolve<PlayerSession>() ?? throw new InvalidOperationException("Player session is unavailable.");
                await app.StartAsync(run.Token);
                var changes = scenario.Input.ToDictionary(change => change.Tick);
                var checkpoints = scenario.Checkpoints.ToDictionary(checkpoint => checkpoint.Tick);
                var input = default(GameInputState);
                for (var tick = 1; tick <= scenario.MaxTicks; tick++)
                {
                    if (changes.TryGetValue(tick, out var change)) input = change.State;
                    checkpoints.TryGetValue(tick, out var checkpoint);
                    var expected = checkpoint?.ExpectedObservations ?? [];
                    var step = await player.AdvanceScenarioAsync(input, checkpoint != null, expected.Length != 0, run.Token);
                    input = input with { Yaw = 0, Pitch = 0, InteractPressed = false };
                    if (step.Status.Tick != (ulong)tick) throw new InvalidDataException($"Requested tick {tick}, observed {step.Status.Tick}.");
                    if (checkpoint == null) continue;
                    var frame = step.Frame;
                    if (player.SupportsFrameCapture && frame == null)
                        throw new InvalidDataException($"No frame for '{checkpoint.Id}'.");
                    string? file = null, hash = null;
                    if (frame != null)
                    {
                        file = $"frame-{checkpoint.Id}.png";
                        await File.WriteAllBytesAsync(Path.Combine(output, file), frame.Png.AsMemory(), run.Token);
                        hash = Convert.ToHexString(SHA256.HashData(frame.Png.AsSpan()));
                    }
                    var hudStatus = session.Hud?.Status;
                    var checks = expected.Select(expectation => Evaluate(expectation, step.Observation)).ToArray();
                    var passed = (checkpoint.ExpectedInput == null || checkpoint.ExpectedInput == step.Input) &&
                        (checkpoint.ExpectedHudStatus == null || checkpoint.ExpectedHudStatus == hudStatus) &&
                        checks.All(check => check.Outcome == ObservationOutcome.Passed);
                    observations.Add(new(checkpoint, step.Input, hudStatus, step.Status.Tick,
                        frame != null ? "Captured" : "UnavailableHeadless", frame?.Tick, frame?.FrameId, frame?.SurfaceGeneration,
                        frame?.Width, frame?.Height, frame?.Scale, file, hash,
                        step.Observation?.Tick, step.Observation?.Version, checks, passed));
                    if (!passed)
                    {
                        var unavailable = checks.Any(check => check.Outcome is ObservationOutcome.Unavailable or ObservationOutcome.Incompatible);
                        result = unavailable ? 7 : 2;
                        error = unavailable ? $"Observation unavailable or incompatible at '{checkpoint.Id}'." : $"Expectation failed at '{checkpoint.Id}'.";
                        break;
                    }
                }
            }
            finally
            {
                // The native close may resume the canceled wait on the UI owner thread.
                try { await Task.Run(async () => await app.DisposeAsync()); }
                catch (Exception cleanup) { cleanupError = cleanup.ToString(); }
                finally { if (window != null) window.Closing -= OnWindowClosing; }
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            result = interrupted.IsCancellationRequested ? 4 : 3;
            error = result == 4 ? "Scenario canceled." : "Scenario timed out.";
        }
        catch (Exception failure) { result = 5; error = failure.ToString(); }
        finally { Console.CancelKeyPress -= OnCancel; }

        var surface = session?.Surface;
        var diagnostics = surface?.Diagnostics;
        if (headless && cleanupError == null && session != null && (surface != null || session.Status.State != PlayerState.Stopped))
            cleanupError = "The headless Player did not stop without a surface.";
        if (cleanupError == null && surface != null && (!surface.IsReleased || !Released(diagnostics!) || window?.IsClosed != true ||
            session?.Status.State != PlayerState.Stopped))
            cleanupError = "The Player window or GPU resources remain live after App disposal.";
        if (result == 0 && diagnostics is { ValidationEnabled: true } && (diagnostics.Errors != 0 || diagnostics.Warnings != 0))
        {
            result = 5;
            error = "Vulkan validation reported messages during the run.";
        }
        if (cleanupError != null) result = 6;
        var report = new
        {
            FormatVersion = 1, ExitCode = result, Error = error, CleanupError = cleanupError, ScenarioHash = scenarioHash,
            Seed = scenario?.Seed, MaxTicks = scenario?.MaxTicks, TimeoutMs = scenario?.TimeoutMs,
            BuildId = start?.Artifact.BuildId, SceneHash = start?.SceneHash,
            Scene = start?.World?.Scene.Id.Value.ToString() ?? start?.Snapshot?.Id,
            SceneRevision = start?.World?.Scene.Revision ?? start?.Snapshot?.Revision,
            HostContractId = start?.Artifact.HostContractId, Profile = headless ? "Headless" : "Windowed",
            Backend = headless ? "None" : "Vulkan / OpenGL",
            Versions = new { Probe = VersionOf(typeof(Program)), Player = VersionOf(typeof(PlayerWindow)), Runtime = VersionOf(typeof(PlayerSession)) },
            Checkpoints = observations, State = session?.Status.State.ToString(), WindowClosed = window?.IsClosed,
            SurfaceReleased = surface?.IsReleased, Diagnostics = diagnostics
        };
        var pending = Path.Combine(output, "report.tmp");
        await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(report, Json));
        File.Move(pending, Path.Combine(output, "report.json"));
        return result;

        void OnCancel(object? sender, ConsoleCancelEventArgs args) { args.Cancel = true; interrupted.Cancel(); }

        async Task<bool> OnWindowClosing(UiCloseReason reason)
        {
            if (reason == UiCloseReason.Native) await interrupted.CancelAsync();
            return true;
        }
    }

    private static bool Released(GpuSurfaceProducerDiagnostics diagnostics) => diagnostics is { LiveSlots: 0, OpenHandles: 0 } &&
        diagnostics.SlotsCreated == diagnostics.SlotsReleased && diagnostics.SurfacesCreated == diagnostics.SurfacesReleased &&
        diagnostics.DevicesCreated == diagnostics.DevicesReleased;

    private static string? VersionOf(Type type) => type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private static ObservationCheck Evaluate(ExpectedNumber expected, GameScenarioSnapshot? snapshot)
    {
        if (snapshot == null || !snapshot.Values.TryGetValue(expected.Key, out var number))
            return new(expected, null, ObservationOutcome.Unavailable);
        if (snapshot.Version != expected.Version || !StringComparer.Ordinal.Equals(number.Unit, expected.Unit))
            return new(expected, number, ObservationOutcome.Incompatible);
        var inRange = number.Value >= expected.Minimum && number.Value <= expected.Maximum;
        return new(expected, number, inRange ? ObservationOutcome.Passed : ObservationOutcome.OutOfRange);
    }

    private static void Validate(Scenario value)
    {
        if (value.FormatVersion != 1 || value.MaxTicks is < 1 or > 100_000 || value.TimeoutMs is < 1 or > 600_000 ||
            value.Width is < 16 or > 1920 || value.Height is < 16 or > 1080 || value.Input == null || value.Checkpoints is not { Length: >0 and <=64 })
            throw new InvalidDataException("Unsupported scenario version or invalid scenario limits.");
        if (value.Input.Any(change => change == null || change.Tick < 1 || change.Tick > value.MaxTicks) ||
            value.Input.Select(change => change.Tick).Distinct().Count() != value.Input.Length)
            throw new InvalidDataException("Input changes require distinct ticks within the scenario.");
        foreach (var change in value.Input) change.State.Validate();
        if (value.Checkpoints.Any(point => point == null || point.Tick < 1 || point.Tick > value.MaxTicks ||
                point.Id is not { Length: >0 and <=48 } || point.Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-') ||
                point.ExpectedInput is {} input && !Enum.IsDefined(input)) ||
            value.Checkpoints.Select(point => point.Tick).Distinct().Count() != value.Checkpoints.Length ||
            value.Checkpoints.Select(point => point.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.Checkpoints.Length)
            throw new InvalidDataException("Checkpoints require distinct ticks and alphanumeric or hyphenated IDs.");
        foreach (var point in value.Checkpoints)
        {
            if (point.ExpectedObservations is not {} numbers) continue;
            if (numbers.Length > GameScenarioSnapshot.MaximumValues || numbers.Any(number => number == null ||
                    string.IsNullOrWhiteSpace(number.Key) || number.Key.Length > 64 ||
                    string.IsNullOrWhiteSpace(number.Unit) || number.Unit.Length > 32 || number.Version <= 0 ||
                    !double.IsFinite(number.Minimum) || !double.IsFinite(number.Maximum) || number.Minimum > number.Maximum) ||
                numbers.Select(number => number.Key).Distinct(StringComparer.Ordinal).Count() != numbers.Length)
                throw new InvalidDataException($"Invalid numeric observations at checkpoint '{point.Id}'.");
        }
    }

    private sealed record Scenario([property: JsonRequired] int FormatVersion, [property: JsonRequired] int MaxTicks,
        [property: JsonRequired] int TimeoutMs, InputChange[] Input, Checkpoint[] Checkpoints, uint? Seed = null,
        int Width = 800, int Height = 540, bool Validation = false);
    private sealed record InputChange(int Tick, GameInputState State);
    private sealed record Checkpoint(string Id, int Tick, GameInputOutcome? ExpectedInput, string? ExpectedHudStatus,
        ExpectedNumber[]? ExpectedObservations);
    private sealed record ExpectedNumber(string Key, [property: JsonRequired] int Version, string Unit,
        [property: JsonRequired] double Minimum, [property: JsonRequired] double Maximum);
    private enum ObservationOutcome { Passed, OutOfRange, Unavailable, Incompatible }
    private sealed record ObservationCheck(ExpectedNumber Expected, GameScenarioNumber? Actual, ObservationOutcome Outcome);
    private sealed record Observation(Checkpoint Requested, GameInputOutcome Input, string? HudStatus, ulong Tick, string FrameAvailability,
        ulong? RenderedTick, ulong? FrameId, ulong? SurfaceGeneration, int? Width, int? Height, float? Scale, string? File, string? Sha256,
        ulong? ObservationTick, int? ObservationVersion, ObservationCheck[] ObservationChecks, bool Passed);
}
