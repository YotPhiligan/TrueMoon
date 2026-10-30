using System.Text.Json;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorCompilerDiagnosticsTests
{
    [Fact]
    public void CompilerSelection_UsesFrozenJobLocationAndSurvivesNextSuccessfulBuildAndFilter()
    {
        using var context = Context(); var navigator = new Navigator();
        using var model = Model(context, navigator); model.Refresh(); model.Select("failed:compiler:0");
        Assert.True(model.CanOpenSource); Assert.Contains("CS1002", model.Details); Assert.Contains("job failed", model.Details); Assert.Contains("revision 41", model.Details);
        Assert.DoesNotContain('\r', model.Details);
        var frozen = model.Details;
        context.Batch = new(false, true, [Process("success", 42, []), context.Batch.Processes[0]]); model.Refresh();
        model.Search = "absent"; Assert.Equal("failed:compiler:0", model.SelectedId); Assert.Equal(frozen, model.Details);
        model.OpenSource(); Assert.Single(navigator.Locations); Assert.Equal(new(SourcePath, 17, 9), navigator.Locations[0]);
        Assert.Contains("отправлен", model.NavigationStatus);
        context.Batch = new(false, true, []); model.Refresh(); Assert.Null(model.SelectedId); Assert.False(model.CanOpenSource);
        Assert.Equal(0, context.Disposals);
    }

    [Fact]
    public void CompilerView_RetainsTreeAndNavigatesWithFocusedButton()
    {
        using var context = Context(); var navigator = new Navigator(); using var model = Model(context, navigator);
        model.IsOpen = true; model.FollowTail = false; model.Refresh();
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        using var ui = new UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360)); ui.Update();
        var tree = view.Events; view.Events.Select("failed:compiler:0"); ui.Update(); Assert.True(view.OpenSource.IsEnabled);
        ui.Focus(view.OpenSource); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter)); ui.Update();
        Assert.Single(navigator.Locations); Assert.Equal(17, navigator.Locations[0].Line); Assert.Equal(9, navigator.Locations[0].Column);
        ui.SetTheme(Theme.Light); model.Refresh(); ui.Update(); Assert.Same(tree, view.Events); Assert.Equal("failed:compiler:0", view.Events.SelectedId);
        Assert.Equal(model.Details, view.Details.Text);
    }

    [Fact]
    public void MissingHookOrSourceRegion_DisablesNavigationWithoutHidingDiagnostic()
    {
        using var context = Context(); using var missing = Model(context, null); missing.Refresh(); missing.Select("failed:compiler:0");
        Assert.False(missing.CanOpenSource); Assert.Contains("host", missing.Details);
        using var model = Model(context, new Navigator()); model.Refresh(); model.Select("failed:compiler:1");
        Assert.False(model.CanOpenSource); Assert.Contains("NU0001", model.Details);
        model.Mode = EditorDiagnosticMode.StandardError; model.Select("failed"); Assert.False(model.CanOpenSource);
    }

    [Fact]
    public async Task Navigation_StopCancelsAndDrainsBorrowedHookWithoutDuplicateRequest()
    {
        using var context = Context(); var navigator = new Navigator { Hold = true }; using var model = Model(context, navigator);
        model.Refresh(); model.Select("failed:compiler:0"); model.OpenSource(); model.OpenSource(); Assert.Single(navigator.Locations); Assert.False(model.CanOpenSource);
        await model.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)); Assert.True(navigator.Token.IsCancellationRequested);
        model.Refresh(); Assert.Contains("отменено", model.NavigationStatus); Assert.False(model.CanOpenSource);
    }

    [Fact]
    public void NavigationFailure_IsObservedOnRefreshWithoutEscapingUiCommand()
    {
        using var context = Context(); using var model = Model(context, new Navigator { Failure = new IOException("PRIVATE PATH") });
        model.Refresh(); model.Select("failed:compiler:0"); model.OpenSource(); model.Refresh();
        Assert.Contains("IOException", model.NavigationStatus); Assert.DoesNotContain("PRIVATE PATH", model.NavigationStatus); Assert.True(model.CanOpenSource);
    }

    [Fact]
    public async Task PresentationBudget_BoundsAndFreezesCompilerDataWhileMetadataExportOmitsSourceText()
    {
        using var context = Context(); var entries = Enumerable.Range(0, 200).Select(i => new GameBuildDiagnostic("CS" + i,
            GameBuildDiagnosticSeverity.Error, "PRIVATE_MESSAGE_" + new string('m', 3000), SourcePath, 1, 2)).ToArray();
        context.Batch = new(false, true, [Process("many", 1, entries)]);
        using var model = new EditorDiagnosticsViewModel(null, new() { MaximumRecords = 3, MaximumRetainedBytes = 16384 }, new(), null, context, null) { Mode = EditorDiagnosticMode.Compiler };
        model.Refresh(); var batch = Assert.Single(model.Processes!.Processes).CompilerDiagnostics!;
        Assert.Equal(3, batch.Entries.Count); Assert.Equal(197, batch.Omitted); Assert.True(batch.IsTruncated); Assert.All(batch.Entries, entry => Assert.Equal(2048, entry.Message.Length));
        entries[0] = entries[0] with { Message = "MUTATED" }; Assert.StartsWith("PRIVATE_MESSAGE_", batch.Entries[0].Message);
        var path = Path.Combine(Path.GetTempPath(), "TrueMoon-compiler-report-" + Guid.NewGuid() + ".json");
        try
        {
            await new EditorDiagnosticReportWriter().WriteAsync(model.Snapshot(), path);
            var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE_MESSAGE", text); Assert.DoesNotContain(SourcePath, text);
            using var report = JsonDocument.Parse(text); Assert.True(report.RootElement.GetProperty("partial").GetBoolean());
            var compiler = Assert.Single(report.RootElement.GetProperty("processes").EnumerateArray()).GetProperty("Compiler");
            Assert.Equal(3, compiler.GetProperty("Count").GetInt32()); Assert.Equal(197, compiler.GetProperty("Omitted").GetInt32());
            Assert.True(compiler.GetProperty("IsTruncated").GetBoolean());
            Assert.Contains(report.RootElement.GetProperty("omittedByPolicy").EnumerateArray(), value => value.GetString() == "sourcePaths");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CommandHook_SnapshotsArgvAndSubstitutesSourcePathAsOneLiteralArgument()
    {
        var arguments = new[] { "--goto", "{file}:{line}:{column}" };
        var options = new SourceEditorOptions { ExecutablePath = Path.Combine(Path.GetTempPath(), "editor.exe"), Arguments = arguments };
        var navigator = new CommandSourceFileNavigator(options); arguments[1] = "MUTATED"; options.ExecutablePath = "relative";
        var location = new EditorSourceLocation(Path.Combine(Path.GetTempPath(), "a & {line}.cs"), 17, 9);
        var info = navigator.StartInfo(location); Assert.False(info.UseShellExecute); Assert.True(info.CreateNoWindow);
        Assert.Equal(new[] { "--goto", location.FilePath + ":17:9" }, info.ArgumentList); Assert.EndsWith("editor.exe", info.FileName);
    }

    [Fact]
    public async Task CommandHook_MissingSourceAndCancellationNeverStartEditor()
    {
        var navigator = new CommandSourceFileNavigator(new() { ExecutablePath = Path.Combine(Path.GetTempPath(), "missing-editor.exe"), Arguments = ["{file}"] });
        var location = new EditorSourceLocation(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs"), 1, 1);
        await Assert.ThrowsAsync<FileNotFoundException>(() => navigator.OpenAsync(location));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => navigator.OpenAsync(location, cancellation.Token));
        Assert.Throws<ArgumentException>(() => new CommandSourceFileNavigator(new() { ExecutablePath = "relative.exe", Arguments = ["{file}"] }));
        Assert.Throws<ArgumentException>(() => new CommandSourceFileNavigator(new() { ExecutablePath = SourcePath, Arguments = ["--line", "{line}"] }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_BorrowCustomHookAndRegisterCommandHookWithDuplicateRejection(bool microsoft)
    {
        var navigator = new Navigator(); var context = Context();
        using var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c =>
        {
            c.Services(s => { s.Singleton<ISourceFileNavigator>(_ => navigator); s.Singleton<IProcessDiagnosticsSource>(_ => context); }); c.UseAetherEditorDiagnostics();
        }).Build();
        Assert.Same(navigator, app.Services.Resolve<ISourceFileNavigator>()); Assert.NotNull(app.Services.Resolve<EditorDiagnosticsViewModel>());
        var model = app.Services.Resolve<EditorDiagnosticsViewModel>()!; model.Mode = EditorDiagnosticMode.Compiler;
        model.Refresh(); model.Select("failed:compiler:0"); model.OpenSource(); Assert.Single(navigator.Locations); Assert.Equal(SourcePath, navigator.Locations[0].FilePath);
        app.Dispose(); Assert.Equal(1, context.Disposals);
        using var configured = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c => c.UseAetherEditorSourceNavigation(
            new() { ExecutablePath = Path.Combine(Path.GetTempPath(), "editor.exe"), Arguments = ["{file}"] })).Build();
        Assert.IsType<CommandSourceFileNavigator>(configured.Services.Resolve<ISourceFileNavigator>());
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(c =>
        {
            c.UseAetherEditorSourceNavigation(new() { ExecutablePath = SourcePath, Arguments = ["{file}"] });
            c.UseAetherEditorSourceNavigation(new() { ExecutablePath = SourcePath, Arguments = ["{file}"] });
        }).Build());
    }

    private static readonly string SourcePath = Path.Combine(Path.GetTempPath(), "PRIVATE_SOURCE.cs");
    private static EditorDiagnosticContextTests.Context Context() => new() { Batch = new(false, true, [Process("failed", 41,
        [new("CS1002", GameBuildDiagnosticSeverity.Error, "Expected ;", SourcePath, 17, 9), new("NU0001", GameBuildDiagnosticSeverity.Warning, "No source", null, null, null)])]) };
    private static ProcessDiagnosticSnapshot Process(string id, long revision, IReadOnlyList<GameBuildDiagnostic> entries) =>
        new(id, "Build", DateTimeOffset.UnixEpoch, 1, null, null, null, null, id, revision, true, true, null, null, null)
        { CompilerDiagnostics = new(GameBuildDiagnosticState.Available, entries) };
    private static EditorDiagnosticsViewModel Model(EditorDiagnosticContextTests.Context context, ISourceFileNavigator? navigator) =>
        new(null, new(), new(), null, context, navigator) { Mode = EditorDiagnosticMode.Compiler };
    private sealed class Navigator : ISourceFileNavigator
    {
        internal readonly List<EditorSourceLocation> Locations = [];
        internal bool Hold; internal Exception? Failure; internal CancellationToken Token;
        public Task OpenAsync(EditorSourceLocation location, CancellationToken cancellationToken = default)
        { Locations.Add(location); Token = cancellationToken; return Failure != null ? Task.FromException(Failure) : Hold ? Task.Delay(Timeout.Infinite, cancellationToken) : Task.CompletedTask; }
    }
}
