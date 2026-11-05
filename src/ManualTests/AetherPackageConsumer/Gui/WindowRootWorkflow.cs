using TrueMoon;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AetherGuiConsumer;

internal sealed class WindowRootProbe : IViewStyle<EditorMainWindow>
{
    internal const string BaseTitle = "Window integration · пользовательский title";
    internal static readonly Size Client = new(1360, 840);
    internal int Styles, HeaderCalls;
    internal EditorView? HeaderContent;
    public void Configure(IViewStyleBuilder<EditorMainWindow> styles)
    { Styles++; new EditorMainWindowStyle().Configure(styles); }
}

internal static class WindowRootWorkflow
{
    internal static async Task<object> Run(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        IApp app, WindowRootProbe probe, bool embedded, CancellationToken token)
    {
        var documents = app.Services.Resolve<EditorDocumentViewModel>()!;
        EditorMainWindow? root = null; var owner = 0; var closeRequests = 0; var componentCloses = 0;
        var cleanCaptions = 0; var dirtyCaptions = 0; var pending = false; var checkedFrames = 0;
        var sourceOwners = new HashSet<string>(); var sourceStates = new HashSet<string>();
        await ui.InvokeAsync(() =>
        {
            owner = Environment.CurrentManagedThreadId;
            if (embedded)
            {
                Program.Check(probe.Styles == 0 && probe.HeaderCalls == 0 && view.Parent is Border,
                    "Embedded consumer acquired the standalone Window graph.");
            }
            else
            {
                root = EditorMainWindow.FromRoot(ui.Root);
                Program.Check(root.IsHosted && ReferenceEquals(root.EditorView, view) && ReferenceEquals(root.Child, view) &&
                    ReferenceEquals(root.DataContext, view.DataContext), "Standalone Window/content/model identity changed.");
                Program.Check(root.ClientSize == WindowRootProbe.Client && probe.Styles == 1 && probe.HeaderCalls == 1 &&
                    ReferenceEquals(probe.HeaderContent, view) && view.Header != null,
                    "Configured size/header callback or factory ownership was lost.");
                Program.Check(root.Bounds == view.Bounds, "Neutral Window wrapper changed viewport layout.");
                root.Closing += reason =>
                {
                    Program.Check(Environment.CurrentManagedThreadId == owner && reason == WindowCloseReason.Application,
                        "Window close guard escaped owner or changed reason.");
                    componentCloses++; return Task.FromResult(true);
                };
            }
        }, token);
        if (!embedded)
        {
            var initial = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(native =>
            {
                try
                {
                    var viewport = native.Viewport;
                    Program.Check(Math.Abs(viewport.Width / viewport.Scale - WindowRootProbe.Client.Width) < 2 &&
                        Math.Abs(viewport.Height / viewport.Scale - WindowRootProbe.Client.Height) < 2,
                        "Native client size ignored the copied configure override.");
                    initial.SetResult();
                }
                catch (Exception error) { initial.SetException(error); }
            });
            await initial.Task.WaitAsync(token);
        }
        void Observe(UiSession current)
        {
            if (embedded) return;
            Program.Check(Environment.CurrentManagedThreadId == owner && ReferenceEquals(EditorMainWindow.FromRoot(current.Root), root) &&
                ReferenceEquals(root!.EditorView, view) && root.Bounds == view.Bounds, "Window was rebuilt or moved its content.");
            checkedFrames++;
            var source = view.DataContext!.Source;
            if (source != null) { sourceOwners.Add(source.OwnerKey.ToString()); sourceStates.Add(source.State.ToString()); }
            if (pending) return;
            pending = true;
            window.PostWindow(native =>
            {
                try
                {
                    var caption = documents.GetWindowCaption(WindowRootProbe.BaseTitle);
                    if (native.NativeWindow.Title == caption && root!.Title == caption && view.Header!.Caption.Value == caption)
                    {
                        if (documents.Document?.IsDirty == true) dirtyCaptions++; else cleanCaptions++;
                    }
                }
                finally { pending = false; }
            });
        }
        async Task<bool> RequestClose()
        {
            closeRequests++;
            Task<bool> attempt = null!;
            await ui.InvokeAsync(() => attempt = root!.RequestCloseAsync(token), token);
            return await attempt.WaitAsync(token);
        }
        window.FramePresented += Observe;
        object workflow;
        try { workflow = await Workflow.Run(directory, window, ui, view, app, token, embedded ? null : RequestClose); }
        finally { window.FramePresented -= Observe; }
        if (!embedded)
            Program.Check(root is { IsDisposed: true, IsHosted: false } && view.IsDisposed && closeRequests == 3 && componentCloses == 3 &&
                probe.Styles == 1 && probe.HeaderCalls == 1 && cleanCaptions > 0 && dirtyCaptions > 0 && sourceOwners.Count >= 2,
                "Window caption/close-once/source-owner/lifetime proof is incomplete.");
        else Program.Check(probe.Styles == 0 && probe.HeaderCalls == 0, "Embedded path created standalone Window UI.");
        return new { Profile = "EditorWindowRoot", Integrated = !embedded, CopiedSettings = !embedded,
            HeaderCallbackReceivesEditorView = !embedded, CloseRequests = closeRequests, ComponentCloseCalls = componentCloses,
            StyleCreates = probe.Styles, HeaderCreates = probe.HeaderCalls, CleanCaptions = cleanCaptions, DirtyCaptions = dirtyCaptions,
            CheckedFrames = checkedFrames, SourceOwners = sourceOwners, SourceStates = sourceStates, Owner = owner, UnderlyingWorkflow = workflow };
    }
}
