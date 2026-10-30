using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorRuntimeObservationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TrueMoon-child-report-" + Guid.NewGuid().ToString("N"));
    public EditorRuntimeObservationTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Report_ExportsNumericChildHealthAndDeviceAfterCrashWithoutSinkErrorText(bool includeText)
    {
        var process = Process(7) with { Completed = true, ResourcesReleased = true };
        var snapshot = new EditorDiagnosticSnapshot([], 0, 0, 0, 0, false) { Processes = new(true, false, [process]) };
        var path = Path.Combine(_directory, includeText + ".json");
        await new EditorDiagnosticReportWriter().WriteReportAsync(snapshot, path, new() { IncludeText = includeText });
        var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE_CHILD", text);
        using var json = JsonDocument.Parse(text); var root = json.RootElement; var observation = Assert.Single(root.GetProperty("runtimeObservations").EnumerateArray());
        Assert.True(observation.GetProperty("Completed").GetBoolean()); Assert.True(observation.GetProperty("CollectorAvailable").GetBoolean()); Assert.True(observation.GetProperty("DeviceAvailable").GetBoolean());
        var sink = Assert.Single(observation.GetProperty("Collector").GetProperty("Sinks").EnumerateArray());
        Assert.Equal(7, sink.GetProperty("Failures").GetInt64()); Assert.Equal(4, sink.GetProperty("Dropped").GetInt64()); Assert.True(sink.GetProperty("HasError").GetBoolean());
        Assert.Equal("Vulkan", observation.GetProperty("GraphicsDevice").GetProperty("Backend").GetString());
        Assert.Equal("selected-GPU", observation.GetProperty("GraphicsDevice").GetProperty("Name").GetString());
        Assert.DoesNotContain(root.GetProperty("unavailable").EnumerateArray(), value => value.GetString() is "childSinkHealth" or "runtimeGraphicsDevice");
        Assert.Equal(0, root.GetProperty("runtimeObservationsOmittedByBudget").GetInt32()); Assert.Contains("stale", root.GetProperty("runtimeObservationPolicy").GetString());
        var legacyPath = Path.Combine(_directory, "legacy-" + includeText + ".json"); await new EditorDiagnosticReportWriter().WriteAsync(snapshot, legacyPath);
        Assert.DoesNotContain("PRIVATE_CHILD", await File.ReadAllTextAsync(legacyPath));
    }

    [Fact]
    public async Task SmallBudget_OmitsChildObservationExplicitlyAndKeepsValidPartialJson()
    {
        var process = Process(1); var health = process.Delivery!.RuntimeObservation!.Collector!;
        process = process with { Delivery = process.Delivery with { RuntimeObservation = process.Delivery.RuntimeObservation with
            { Collector = health with { Sinks = Enumerable.Range(0, 16).Select(i => health.Sinks[0] with { Name = "custom-" + i }).ToArray() } } } };
        var path = Path.Combine(_directory, "budget.json"); await new EditorDiagnosticReportWriter().WriteReportAsync(
            new([], 0, 0, 0, 0, false) { Processes = new(true, false, [process]) }, path, new() { MaximumBytes = 4096 });
        Assert.InRange(new FileInfo(path).Length, 1, 4096); using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Empty(json.RootElement.GetProperty("runtimeObservations").EnumerateArray());
        Assert.Equal(1, json.RootElement.GetProperty("runtimeObservationsOmittedByBudget").GetInt32()); Assert.True(json.RootElement.GetProperty("partial").GetBoolean());
    }

    [Fact]
    public void HealthInspection_FreezesObservedChildStateUntilExplicitRefreshAndMarksMissingDataUnavailable()
    {
        using var context = new EditorDiagnosticContextTests.Context(); context.Batch = new(true, false, [Process(7)]);
        using var model = new EditorDiagnosticsViewModel(null, new(), new(), null, context); model.Mode = EditorDiagnosticMode.Health; model.Refresh(); model.Select("process:p1");
        Assert.Contains("Child sink jsonl", model.Details); Assert.Contains("failures 7", model.Details); Assert.Contains("selected-GPU", model.Details);
        Assert.Contains("последнее независимое наблюдение", model.Details);
        context.Batch = new(true, false, [Process(99)]); model.Refresh(); Assert.DoesNotContain("failures 99", model.Details);
        model.RefreshDetails(); Assert.Contains("failures 99", model.Details);
        context.Batch = new(true, false, [Process(0) with { Delivery = null }]); model.Refresh(); model.RefreshDetails();
        Assert.Contains("наблюдение не получено", model.Details); Assert.DoesNotContain("Child sink jsonl", model.Details); Assert.Equal(0, context.Disposals);
    }

    [Fact]
    public void PresentationDevice_IsReadFromCachedOptionalSurfaceCapabilityAndCopiedAsValues()
    {
        using var model = new EditorDiagnosticsViewModel(null, new(), new()) { IsOpen = true, FollowTail = false };
        using var surface = new Surface(); using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        using var session = new UiSession(view, surface, new(1280, 360)); session.Update();
        Assert.Contains("actual-GPU", model.Snapshot().Presentation!.Device); Assert.Equal(1, surface.Reads);
        surface.Info = null; view.AdvanceFrame(TimeSpan.Zero, session); Assert.Null(model.Snapshot().Presentation!.Device);
    }

    private static ProcessDiagnosticSnapshot Process(long failures)
    {
        var process = EditorDiagnosticContextTests.Context.Process("p1", "", "");
        var observation = new RuntimeDiagnosticObservation(DateTimeOffset.Parse("2026-10-31T06:00:00Z"),
            new(Guid.Parse("142aaf3b-89b7-4866-8841-9f0132f11051"), false, 10, 2, 100, 4, 0, 0, [new("jsonl", 10, 2, 4, failures, 1, 30, "PRIVATE_CHILD_ERROR")]),
            new("Vulkan", "selected-GPU", 1, 2, 3, 4));
        return process with { Delivery = process.Delivery! with { RuntimeObservation = observation, ObservationOmittedPackets = 2 } };
    }
    public void Dispose() => Directory.Delete(_directory, true);
    private sealed class Surface : IUiRenderSurface, IUiRenderDiagnostics
    {
        private readonly EditorDiagnosticsViewTests.Surface _inner = new();
        internal UiRenderDeviceInfo? Info = new("OpenGL", "actual-GPU", "vendor", "version"); internal int Reads;
        public UiRenderDeviceInfo? DeviceInfo { get { Reads++; return Info; } }
        public ITextLayoutService TextLayout => _inner.TextLayout;
        public void VerifyAvailable() => _inner.VerifyAvailable(); public void Resize(UiViewport viewport) => _inner.Resize(viewport);
        public void Render(Element root, UiViewport viewport) => _inner.Render(root, viewport); public void Dispose() => _inner.Dispose();
    }
}
