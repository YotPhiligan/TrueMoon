using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Player.Scenarios;

/// <summary>Runs a version 1 file scenario against a published Aether Player build.</summary>
public static class PlayerScenarioRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<GameButtons>(), new JsonStringEnumConverter<GameInputOutcome>(),
            new JsonStringEnumConverter<ObservationOutcome>() },
        WriteIndented = true
    };

    /// <summary>Runs fixed ticks and publishes the scenario report after Player shutdown.</summary>
    /// <param name="scenarioPath">Path to the scenario JSON.</param>
    /// <param name="buildDirectory">Path to a published Build for the managed entry assembly.</param>
    /// <param name="scenePath">Path to the selected scene.</param>
    /// <param name="outputDirectory">Fresh output directory for reports, diagnostics, and frames.</param>
    /// <param name="legacyScene">Whether to load a legacy scene snapshot instead of a World scene.</param>
    /// <param name="createWindowFonts">Window font factory; null selects the headless profile.</param>
    /// <param name="configureDiagnostics">Optional customization of the scenario diagnostics.</param>
    /// <param name="cancellationToken">Caller cancellation, reported as exit code 4.</param>
    /// <returns>The scenario v1 exit code (0, 2, 3, 4, 5, 6, or 7).</returns>
    public static async Task<int> RunAsync(string scenarioPath, string buildDirectory, string scenePath, string outputDirectory,
        bool legacyScene, Func<SkiaFontCatalog>? createWindowFonts, Action<DiagnosticPipelineOptions>? configureDiagnostics = null,
        CancellationToken cancellationToken = default)
    {
        var headless = createWindowFonts == null;
        try { outputDirectory = ReserveOutputDirectory(outputDirectory); }
        catch (Exception failure)
        {
            Console.Error.WriteLine($"Could not reserve scenario output: {failure.Message}");
            return 5;
        }

        var observations = new List<Observation>();
        using var interrupted = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource();
        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, interrupted.Token, timeout.Token);
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
            start = await PlayerStart.CaptureAsync(buildDirectory, BuildIdentity.HostId, scenePath, legacyScene, run.Token);
            var app = App.Build(context =>
            {
                var diagnosticsOptions = new DiagnosticPipelineOptions { JsonDirectory = Path.Combine(outputDirectory, "diagnostics") };
                configureDiagnostics?.Invoke(diagnosticsOptions);
                context.UseDiagnostics(diagnosticsOptions);
                if (createWindowFonts == null)
                {
                    context.UseAetherHeadlessPlayer(start);
                }
                else
                {
                    context.UseAetherPlayer(start, createWindowFonts(), player =>
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
                    var step = await player.AdvanceScenarioAsync(input, checkpoint != null && player.SupportsFrameCapture, expected.Length != 0, run.Token);
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
                        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, file), frame.Png.AsMemory(), run.Token);
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
            result = cancellationToken.IsCancellationRequested || interrupted.IsCancellationRequested ? 4 : 3;
            error = result == 4 ? "Scenario canceled." : "Scenario timed out.";
        }
        catch (Exception failure) { result = 5; error = failure.ToString(); }

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
            Versions = new { Probe = VersionOf(Assembly.GetEntryAssembly()), Player = VersionOf(typeof(PlayerWindow).Assembly), Runtime = VersionOf(typeof(PlayerSession).Assembly) },
            Checkpoints = observations, State = session?.Status.State.ToString(), WindowClosed = window?.IsClosed,
            SurfaceReleased = surface?.IsReleased, Diagnostics = diagnostics
        };
        try
        {
            var pending = Path.Combine(outputDirectory, "report.tmp");
            await using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                await JsonSerializer.SerializeAsync(file, report, Json);
            File.Move(pending, Path.Combine(outputDirectory, "report.json"));
        }
        catch (Exception failure)
        {
            Console.Error.WriteLine($"Could not publish scenario report: {failure.Message}");
            if (cleanupError != null) Console.Error.WriteLine(cleanupError);
            return cleanupError == null ? 5 : 6;
        }
        return result;

        async Task<bool> OnWindowClosing(UiCloseReason reason)
        {
            if (reason == UiCloseReason.Native) await interrupted.CancelAsync();
            return true;
        }
    }

    private static string ReserveOutputDirectory(string path)
    {
        var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Directory.Exists(output)) throw new IOException("Select a fresh scenario output directory.");

        // Move a sibling into place rather than writing a reservation into a directory we may not own.
        var pending = Path.Combine(Path.GetDirectoryName(output)!, $".aether-scenario-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pending);
        try { Directory.Move(pending, output); }
        catch
        {
            Directory.Delete(pending);
            throw;
        }
        return output;
    }

    private static bool Released(GpuSurfaceProducerDiagnostics diagnostics) => diagnostics is { LiveSlots: 0, OpenHandles: 0 } &&
        diagnostics.SlotsCreated == diagnostics.SlotsReleased && diagnostics.SurfacesCreated == diagnostics.SurfacesReleased &&
        diagnostics.DevicesCreated == diagnostics.DevicesReleased;

    private static string? VersionOf(Assembly? assembly) => assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

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
