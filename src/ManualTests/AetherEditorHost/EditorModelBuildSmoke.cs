using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class EditorModelBuildSmoke
{
    private static async Task<object> VerifyModelImportsAsync(string report, GameBuildArtifact oldBuild, GameBuildArtifact newBuild,
        SceneDocumentSession document, Guid openingId, IFactory<ModelImportLedgerSession, ModelImportLedgerArguments> ledgers,
        UiSession ui, EditorView view, AetherHost engine, EditorScenePlayback playback, CancellationToken token)
    {
        var redId = Guid.Parse("ce662048-9dcb-4c60-aaf0-213342054fa6");
        var greenId = Guid.Parse("65710c6d-6781-4476-9bb8-735fc518f008");
        var redBefore = await ReadImport(redId, "red-baseline.tmledger");
        var redAfter = await ReadImport(redId, "scene.tmledger");
        var green = await ReadImport(greenId, "green.tmledger");
        var buffer = Path.Combine(report, "source", "scene.bin");
        var edited = await File.ReadAllBytesAsync(buffer, token);
        var original = edited.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(original.AsSpan(16), 0f);
        await File.WriteAllBytesAsync(buffer, original, token);

        var oldContent = Freeze(oldBuild);
        var newContent = Freeze(newBuild);
        const string current = "Red · Актуальна\nGreen · Актуальна";
        const string stale = "Red · Импорт устарел\nGreen · Актуальна";
        const string unavailable = "Red · Исходники недоступны\nGreen · Актуальна";
        await Select(redBefore, oldContent);
        await Status(current);
        await Build();
        var previous = engine.Editor.Status;
        Check(playback.Error == null && previous.Context?.World != null && previous.Candidate != null &&
            Matches(previous.Candidate.Build.Artifact, oldContent), $"Initial Editor Build failed: {playback.Error}");

        await File.WriteAllBytesAsync(buffer, edited, token);
        await Build();
        var blocked = playback.Error?.Message;
        var afterBlock = engine.Editor.Status;
        Check(blocked?.Contains("«Red» устарела", StringComparison.Ordinal) == true &&
            previous.ContextVersion == afterBlock.ContextVersion && ReferenceEquals(previous.Candidate, afterBlock.Candidate),
            $"Editor Build did not reject changed sources before creating a new context: {blocked}");
        await Status(stale);
        await EditorHeaderSmoke.UntilAsync(ui, () => view.StatusBar!.DataContext!.ErrorDetails.Contains(blocked!, StringComparison.Ordinal), token);

        File.Move(buffer, buffer + ".missing");
        try
        {
            await Build();
            Check(playback.Error?.Message.Contains("«Red» недоступны", StringComparison.Ordinal) == true &&
                ReferenceEquals(previous.Candidate, engine.Editor.Status.Candidate), "Missing BIN did not block Build.");
            await Status(unavailable);
        }
        finally { File.Move(buffer + ".missing", buffer); }
        await Status(stale);

        try
        {
            await Select(redAfter, oldContent);
            throw new InvalidOperationException("Editor accepted old render content for the new import.");
        }
        catch (InvalidDataException) { }
        await Status(stale);
        await Select(redAfter, newContent);
        await Status(current);
        await Build();
        var updated = engine.Editor.Status.Candidate?.Build.Artifact;
        Check(playback.Error == null && updated != null && updated.BuildId != previous.Candidate!.Build.BuildId &&
            Matches(updated, newContent), $"Editor Build with the reimported model failed: {playback.Error}");

        return new
        {
            Completed = true, BeforeEdit = current, AfterEdit = stale, WhileMissing = unavailable, AfterReimport = current, Blocked = blocked,
            MatchingContent = true, OldBuild = previous.Candidate!.Build.BuildId, NewBuild = updated!.BuildId
        };

        async Task<PublishedModelImport> ReadImport(Guid id, string path)
        {
            await using var ledger = ledgers.Create(new(id, Path.Combine(report, "ledger", path)))!;
            return await ledger.OpenAsync(token) ?? throw new InvalidOperationException($"Missing ledger {path}.");
        }

        Task Select(PublishedModelImport red, GameBuildRenderContent frozen) => document.SelectModelImportsAsync(openingId,
            [new("Red", Watch(red, "source")), new("Green", Watch(green, "source-green"))], frozen, token);

        ModelSourceWatchArguments Watch(PublishedModelImport import, string path) =>
            new(new(import.Candidate.SourceId, Path.Combine(report, path), "scene.gltf"), import)
            { UseFileSystemNotifications = false, PollInterval = TimeSpan.FromSeconds(5) };

        async Task Status(string expected)
        {
            var button = view.StatusBar!.SummaryButton;
            await ui.InvokeAsync(() => view.Header!.Menu.CloseMenu()).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.StatusBar.DataContext!.ModelImportDetails == expected &&
                button.Bounds.Width > 0 && !ui.NeedsUpdate, token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                popup.ModelImportDetails.IsVisible && popup.ModelImportDetails.Value == expected, token, () =>
                {
                    if (view.Overlays.Popup is EditorStatusPopupView) return;
                    EditorHeaderSmoke.Move(ui, button);
                    EditorHeaderSmoke.Click(ui, button);
                });
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape))).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup == null, token);
        }

        async Task Build()
        {
            await EditorHeaderSmoke.UntilAsync(ui, () => view.DataContext!.CanBuild, token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "runtime.Build", token);
            await playback.Pending.WaitAsync(token);
        }

        static GameBuildRenderContent Freeze(GameBuildArtifact artifact) =>
            new(artifact.RenderContent!.ProjectId, artifact.RenderContent.Entries);

        static bool Matches(GameBuildArtifact artifact, GameBuildRenderContent content) =>
            artifact.RenderContent?.Entries.Select(entry => entry.Key).SequenceEqual(content.AssetKeys) == true;
    }

    internal static async Task RunAsync(string[] args)
    {
        var probeImports = args.Length == 7 && args[6] == "--imports";
        if (args.Length != 6 && !probeImports || args[0] is not ("cobalt" or "microsoft")) throw new ArgumentException("Invalid model delivery probe arguments.");
        var report = args[1];
        var project = args[2];
        var scenePath = args[3];
        var hostId = BuildIdentity.HostId;
        var oldBuild = GameBuildArtifact.Open(args[4], hostId);
        var newBuild = GameBuildArtifact.Open(args[5], hostId);
        var corruptBuild = CorruptCopy(oldBuild, report, hostId);
        var sceneHash = Hash(File.ReadAllBytes(scenePath));
        Check(oldBuild.BuildId != newBuild.BuildId, "Scene probe requires two distinct build IDs.");
        var compiler = Path.Combine(report, "editor-compiler");
        CopyArtifact(oldBuild, compiler);
        Action? cancelReplacement = null;
        SessionIdentity? cancelledCandidate = null;
        var cancelOnLoad = 0;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        {
            ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
        });
        var configuration = ConfigurationProfiles.Create(profile =>
        {
            profile.AddSchema(AetherConfigurationSchemas.Engine);
            profile.AddSchema(AetherConfigurationSchemas.Authoring);
            profile.AddSchema(EditorConfigurationSchemas.Options);
            profile.AddSchema(EditorConfigurationSchemas.Diagnostics);
            profile.AddSchema(EditorConfigurationSchemas.Window);
            profile.Override("Aether:ReportDirectory", report);
            profile.Override("Aether:BuildDirectory", compiler);
            profile.Override("Aether:GpuValidation", true);
            profile.Override("Editor:ProjectPath", project);
        });
        await using var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(configuration);
            if (probeImports)
            {
                context.UseAetherModelSourceCapture();
                context.UseAetherModelImport();
            }
            context.UseConfiguredAetherEditor(new AetherComposition
            {
                HostContractId = hostId, GameBuildId = new string('0', 64),
                RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime"),
                StatusChanged = status =>
                {
                    if (status is { State: SessionState.Starting, SceneLoaded: true, Identity: { } identity } &&
                        identity.GameBuildId == oldBuild.BuildId && Interlocked.Exchange(ref cancelOnLoad, 0) == 1)
                    {
                        cancelledCandidate = identity;
                        cancelReplacement!();
                    }
                }
            });
            context.UseConfiguredAetherEditorWindow(fonts, options => { options.Width = 1100; options.Height = 720; });
        }).Build();
        var engine = app.Services.Resolve<AetherHost>()!;
        var native = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        cancelReplacement = () => playback.Execute(EditorCommand.Cancel);
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var captures = new List<FrameProof>();
        var first = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.FramePresented += OnFirstFrame;
        void OnFirstFrame(UiSession ui) { window.FramePresented -= OnFirstFrame; first.TrySetResult(ui); }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        try
        {
            await app.StartAsync(token);
            await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(token);
            var ui = await first.Task.WaitAsync(token);
            EditorView view = null!;
            await ui.InvokeAsync(() => view = EditorMainWindow.FromRoot(ui.Root).EditorView).WaitAsync(token);
            Check(documents.Execute(DocumentCommand.Open, scenePath), "Editor declined opening the placed scene.");
            await documents.Pending.WaitAsync(token);
            var before = documents.Read().Document ?? throw new InvalidOperationException("No active document.");
            var content = before.Content ?? throw new InvalidOperationException("Editor did not load the placed scene.");
            if (probeImports)
            {
                Check(content.Document.Entities.Length == 5 && !before.IsDirty, "Unexpected two-model scene.");
                var document = documents.Read().Project?.Documents ?? throw new InvalidOperationException("No project document session.");
                var ledgerFactory = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
                var proof = await VerifyModelImportsAsync(report, oldBuild, newBuild, document, before.OpeningId,
                    ledgerFactory, ui, view, engine, playback, token);
                Check(ReferenceEquals(content, document.Read().Content) && !document.Read().IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Import selection or Build modified the authoring scene.");
                Check(documents.Execute(DocumentCommand.Close), "Editor declined closing the document.");
                await documents.Pending.WaitAsync(token);
                await EditorHeaderSmoke.UntilAsync(ui, () => view.StatusBar!.DataContext!.ModelImportDetails.Length == 0, token);
                Check(document.ReadModelImports(before.OpeningId).Count == 0, "Closing the scene retained selected imports.");
                window.Close();
                await window.Completion.WaitAsync(token);
                await File.WriteAllTextAsync(Path.Combine(report, "editor-imports.json"), JsonSerializer.Serialize(proof), token);
                return;
            }
            Check(content.Document.Entities.Length == 3 && !before.IsDirty, "Unexpected captured scene.");

            await ChooseBuild(oldBuild.BuildId.ToLowerInvariant(), "Запустить опубликованную сборку");
            await Complete();
            while (engine.Supervisor.Status.Tick < 5)
            {
                await engine.Supervisor.GetStatusAsync(token);
                await Task.Delay(16, token);
            }
            var original = await CapturePlaying("old", oldBuild, 5);
            Check(!playback.RunSelectedBuild(newBuild), "Editor admitted a second independent Start.");
            await ChooseBuild(newBuild.DirectoryPath, "Заменить текущую сборку");
            await Complete();
            var updated = await CapturePlaying("new", newBuild, original.Tick);
            Check(native.Leases.Count == 2 && updated.Generation > original.Generation,
                "Successful Replace did not activate exactly one new child.");
            await CheckReleased(original.Generation);

            var beforeCorruption = engine.Supervisor.Status.Identity;
            var previousSurface = engine.Frames.Presentation;
            await ChooseBuild(corruptBuild.DirectoryPath, "Заменить текущую сборку");
            await playback.Pending.WaitAsync(token);
            Check(playback.Error is InvalidDataException corruption &&
                corruption.Message.Contains("render payload hash mismatch", StringComparison.Ordinal) &&
                engine.Supervisor.Status.Identity == beforeCorruption &&
                ReferenceEquals(previousSurface, engine.Frames.Presentation) && native.Leases.Count == 2,
                "Corrupt candidate changed the running child or viewport.");
            var afterCorruption = await CapturePlaying("after-corruption", newBuild, updated.Tick);

            Volatile.Write(ref cancelOnLoad, 1);
            await ChooseBuild(oldBuild.BuildId, "Заменить текущую сборку");
            await playback.Pending.WaitAsync(token);
            Check(cancelledCandidate is not null && playback.Error is GameReplacementException
                { Phase: "Candidate", RollbackAttempted: true, RollbackSucceeded: true, RecoveryError: null, InnerException: OperationCanceledException } &&
                native.Leases.Count == 4, "Cancellation after scene load did not recover the previous build exactly once.");
            var recovered = await CapturePlaying("after-cancel", newBuild, afterCorruption.Tick);
            await EditorHeaderSmoke.UntilAsync(ui, () => ((EditorDocumentViewModel)view.Documents!.DataContext!).ErrorText ==
                "Замена отменена. Предыдущая сборка восстановлена.", token);
            Check(recovered.Generation > cancelledCandidate!.Generation && cancelledCandidate.Generation > updated.Generation,
                "Cancellation did not advance the runtime generation on recovery.");
            await CheckReleased(updated.Generation);
            // The cancelled candidate had completed scene activation. Restore and surface configuration were still pending.
            Check(native.Leases[2].Identity == cancelledCandidate && native.Leases[2].Reaped && native.Leases[2].ResourcesReleased,
                "Cancelled candidate survived rollback.");

            await ChooseBuild(oldBuild.BuildId, "Заменить текущую сборку");
            await Complete();
            var reopened = await CapturePlaying("old-reopened", oldBuild, recovered.Tick);
            Check(native.Leases.Count == 5 && reopened.Generation > recovered.Generation, "Unexpected runtime generations.");
            await CheckReleased(recovered.Generation);

            Check(playback.Execute(EditorCommand.Stop), "Editor did not admit final Stop.");
            await Complete();
            Check(engine.Supervisor.Status.State == SessionState.Stopped && engine.Frames.Presentation == null &&
                engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                "Final Stop retained viewport resources.");
            await CheckReleased(reopened.Generation);

            var old = original.Mask;
            var next = updated.Mask;
            Check(old == reopened.Mask && next == afterCorruption.Mask && next == recovered.Mask &&
                old.Width == next.Width && old.Height == next.Height && Math.Abs(old.Left - next.Left) <= 1 &&
                next.Right > old.Right + 10 && next.RedPixels > old.RedPixels * 1.6,
                "GPU scene does not show the expected replacement or recovered geometry.");
            Check(!Directory.Exists(Path.Combine(compiler, "jobs")) &&
                AppDomain.CurrentDomain.GetAssemblies().All(assembly => assembly.GetName().Name != "DeliveryGame"),
                "Selecting a published artifact compiled or loaded game code in the parent.");
            window.Close();
            await window.Completion.WaitAsync(token);

            async Task ChooseBuild(string selection, string title)
            {
                var tools = view.Documents!;
                var model = (EditorDocumentViewModel)tools.DataContext!;
                await EditorHeaderSmoke.UntilAsync(ui, () => model.CanOpenBuild, token);
                await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "document.OpenBuild", token, confirmPath: false);
                await EditorHeaderSmoke.UntilAsync(ui, () => tools.IsVisible && tools.Actions.IsVisible && !ui.NeedsUpdate, token);
                await ui.InvokeAsync(() =>
                {
                    Check(model.PathRequest == DocumentCommand.OpenBuild && model.PathPromptText == title, "Wrong build selection prompt.");
                    ui.Focus(tools.PathInput);
                    ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                    ui.HandleInput(new(InputKind.Text, Text: selection));
                    Check(model.RequestedPath == selection, "Artifact path never reached its bound field.");
                }).WaitAsync(token);
                await EditorHeaderSmoke.ConfirmDocumentPathAsync(ui, view, token);
                await EditorHeaderSmoke.UntilAsync(ui, () => !model.IsPathPromptOpen, token);
            }

            async Task Complete()
            {
                await playback.Pending.WaitAsync(token);
                if (playback.Error is { } error) throw new InvalidOperationException("Editor scene command failed.", error);
            }

            async Task<FrameProof> CapturePlaying(string name, GameBuildArtifact artifact, ulong minimumTick)
            {
                await engine.Supervisor.GetStatusAsync(token);
                var status = engine.Supervisor.Status;
                var identity = status.Identity ?? throw new InvalidOperationException("No selected runtime identity.");
                Check(status.State == SessionState.Playing && identity.GameBuildId == artifact.BuildId &&
                    status.Tick >= minimumTick && engine.Supervisor.RequiredRenderViewVersion == 1,
                    $"Expected {artifact.BuildId} Playing at tick >= {minimumTick}, observed {identity.GameBuildId} {status.State} at tick {status.Tick}.");
                var manifest = artifact.RenderContent!;
                await EditorHeaderSmoke.UntilAsync(ui, () =>
                    view.Header!.Caption.AccessibleName.Contains(artifact.BuildId, StringComparison.Ordinal) &&
                    view.Header.Caption.Value.Contains(artifact.BuildId[..12], StringComparison.Ordinal), token);
                var frame = await Capture(name, identity, content.Document.Id.Value, content.Document.Revision, manifest.Digest);
                Check(frame.Tick >= minimumTick, "Presented frame predates the checkpoint.");
                var after = documents.Read().Document!;
                Check(ReferenceEquals(content, after.Content) && before.IsDirty == after.IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Replace changed the open authoring document.");
                var proof = new FrameProof(name, artifact.BuildId, identity.Generation, status.ProcessId!.Value,
                    manifest.Digest.ToString(), frame.Tick, frame.Mask);
                captures.Add(proof);
                return proof;
            }

            async Task CheckReleased(ulong generation)
            {
                var lease = native.Leases.Single(lease => lease.Identity.Generation == generation);
                Check(lease.Reaped && lease.ResourcesReleased, "Retired child or handles survived replacement.");
                using var child = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(report, $"runtime-{generation}.json"), token));
                var stats = child.RootElement;
                var validation = stats.GetProperty("Validation");
                Check(stats.GetProperty("Pid").GetInt32() == lease.ProcessId && stats.GetProperty("Resources").GetInt32() == 0 &&
                    stats.GetProperty("Handles").GetInt32() == 0 && validation.GetProperty("ValidationEnabled").GetBoolean() &&
                    validation.GetProperty("Errors").GetInt32() == 0 && validation.GetProperty("Warnings").GetInt32() == 0,
                    "Incorrect child validation or resource cleanup.");
            }
        }
        finally
        {
            window.FramePresented -= OnFirstFrame;
            try { await documents.StopAsync(); }
            finally { await app.StopAsync(); }
        }

        await File.WriteAllTextAsync(Path.Combine(report, "editor-model.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Provider = args[0], SceneSha256 = sceneHash, Captures = captures,
            CancelledGeneration = cancelledCandidate!.Generation, CorruptRejectedBeforeHandoff = true, RollbackSucceeded = true
        }, new JsonSerializerOptions { WriteIndented = true }), token);

        async Task<(PixelMask Mask, ulong Tick)> Capture(string name, SessionIdentity identity, Guid sceneId, long revision, RenderContentHash digest)
        {
            var result = new TaskCompletionSource<(PixelMask Mask, ulong Tick)>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession ui)
            {
                if (result.Task.IsCompleted) return;
                try
                {
                    var canvas = EditorMainWindow.FromRoot(ui.Root).EditorView.Canvas;
                    if (!canvas.CanAcknowledgeDraw || canvas.DrawnFrame is not { } frame ||
                        frame.Presentation.Surface.Identity != identity ||
                        !ReferenceEquals(frame.Presentation, engine.Frames.Presentation)) return;
                    var stamp = frame.Metadata.RenderView ?? throw new InvalidDataException("Missing mesh viewport stamp.");
                    Check(frame.Presentation.Surface.Transport == ViewportTransportKind.Gpu && stamp.SceneId == sceneId &&
                        stamp.SourceRevision == revision && RenderContentHash.Parse(stamp.ManifestHash) == digest,
                        "GPU draw was not from the captured scene and selected build.");
                    using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using (var file = File.Create(Path.Combine(report, name + ".png"))) png.SaveTo(file);
                    using var pixels = SKBitmap.FromImage(image);
                    var bounds = canvas.ImageBounds;
                    var scale = ui.Viewport.Scale;
                    var clip = new SKRectI((int)(bounds.X * scale), (int)(bounds.Y * scale),
                        (int)((bounds.X + bounds.Width) * scale), (int)((bounds.Y + bounds.Height) * scale));
                    result.TrySetResult((Measure(pixels, clip), stamp.Tick));
                }
                catch (Exception error) { result.TrySetException(error); }
            }
            window.FramePresented += Presented;
            try
            {
                window.RequestFrame();
                return await result.Task.WaitAsync(token);
            }
            finally { window.FramePresented -= Presented; }
        }
    }

    private static GameBuildArtifact CorruptCopy(GameBuildArtifact artifact, string report, string hostId)
    {
        var directory = CopyArtifact(artifact, Path.Combine(report, "corrupt"));
        var copy = GameBuildArtifact.Open(directory, hostId, artifact.BuildId);
        var payload = Path.Combine(directory, GameBuildArtifact.RenderContentFile);
        var bytes = File.ReadAllBytes(payload);
        bytes[^1] ^= 1;
        File.WriteAllBytes(payload, bytes);
        return copy;
    }

    private static string CopyArtifact(GameBuildArtifact artifact, string root)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, artifact.BuildId)).FullName;
        foreach (var path in Directory.EnumerateFiles(artifact.DirectoryPath))
            File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
        return directory;
    }

    private static PixelMask Measure(SKBitmap pixels, SKRectI clip)
    {
        Check(clip.Left >= 0 && clip.Top >= 0 && clip.Right <= pixels.Width && clip.Bottom <= pixels.Height &&
            clip.Width > 0 && clip.Height > 0, "Invalid GPU image bounds.");
        var mask = new byte[clip.Width * clip.Height];
        var upper = 0;
        var lower = 0;
        var left = clip.Width;
        var right = -1;
        for (var y = 0; y < clip.Height; y++)
        for (var x = 0; x < clip.Width; x++)
        {
            var color = pixels.GetPixel(clip.Left + x, clip.Top + y);
            if (color.Red <= 200 || color.Green >= 25 || color.Blue >= 25) continue;
            mask[y * clip.Width + x] = 1;
            if (y < clip.Height / 2) upper++;
            else lower++;
            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }
        Check(upper > 100 && lower > 100, "Expected both occurrences in the GPU viewport.");
        return new(clip.Width, clip.Height, left, right, upper + lower, Hash(mask));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record PixelMask(int Width, int Height, int Left, int Right, int RedPixels, string Sha256);
    private sealed record FrameProof(string Name, string BuildId, ulong Generation, int Pid, string ManifestHash, ulong Tick, PixelMask Mask);
}
