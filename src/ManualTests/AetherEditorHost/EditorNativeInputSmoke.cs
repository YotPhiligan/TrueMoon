using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// This observer never injects input. SilkWindowHost.Input is raised only by the native window input path.
internal static class EditorNativeInputSmoke
{
    internal static async Task RunAsync(string directory,HostedUiWindow window,UiSession ui,EditorView view,EditorWorkspace workspace,
        EditorDocumentCommands commands,AetherHost engine,NativeRuntimeFactory runtime,EditorSession editor,EditorPreview preview,EditorViewportState camera)
    {
        var inspector=view.Inspector?.DataContext??throw new InvalidOperationException("Inspector missing.");
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(15));var token=deadline.Token;
        await ui.InvokeAsync(()=>Require(commands.Execute(DocumentCommand.Open,"Transform.aetherscene"),"Open rejected."));
        await commands.Pending.WaitAsync(token);if(commands.Read().Error is {} open)throw open;
        await ui.InvokeAsync(()=>{editor.Present(ui,view);Require(preview.Show(commands.Read().Document!.Content!.Resolved),"Show rejected.");});
        await preview.Pending.WaitAsync(token);if(preview.Read().Error is {} show)throw show;
        var original=commands.Read().Document!.Content!;var pid=engine.Supervisor.Status.ProcessId;var surface=engine.Frames.Presentation!.Surface.Generation;
        var counts=new Dictionary<string,int>();var samples=new List<object>();var captures=new List<string>();var touched=new HashSet<string>();
        var events=0;var recorded=-1;var captureEver=false;var invalid=false;var invalidCamera=false;var fFrame=false;var unchanged=true;var sameRuntime=true;var clear=false;
        var revision=camera.Camera.Revision;string? selected=workspace.SelectedId;var snapshotSignature="";
        TrueMoon.Alloy.Platform.Silk.SilkWindowHost native=null!;
        void Input(UiInput input)
        {
            var key=input.Kind+(input.Kind is InputKind.KeyDown or InputKind.KeyUp?":"+input.Key:
                input.Kind is InputKind.PointerDown or InputKind.PointerUp?":"+input.Button:"");
            counts[key]=counts.GetValueOrDefault(key)+1;events++;captureEver|=view.ViewportController!.IsGesturing;
        }
        void Frame(UiSession current)
        {
            var content=commands.Read().Document?.Content;
            if(content!=null)unchanged&=ReferenceEquals(content.Document,original.Document)&&content.UndoCount==original.UndoCount&&!commands.Read().Document!.IsDirty;
            var field=inspector.Fields.FirstOrDefault(f=>f.Key=="x");invalid|=field?.HasError==true;
            if(field?.HasError==true&&camera.Camera.Revision!=revision)invalidCamera=true;
            var receipt=editor.ReadPresentedView(view);var ready=receipt!=null;
            if(ready){sameRuntime&=engine.Supervisor.Status.ProcessId==pid&&engine.Frames.Presentation?.Surface.Generation==surface&&engine.Supervisor.Status.Tick==0;clear|=events>0&&workspace.SelectedId==null;}
            var signature=$"{camera.Camera.Revision}:{workspace.SelectedId}:{field?.Value}:{ready}:{current.Viewport}:{view.ViewportController!.IsGesturing}";
            if(recorded==events&&snapshotSignature==signature)return;
            if(ready&&(camera.Camera.Revision!=revision||workspace.SelectedId!=selected||captures.Count==0)&&captures.Count<32)
            {
                using var image=current.ReadbackOpenGLImage();using var encoded=image.Encode(SKEncodedImageFormat.Png,100);
                var name=$"native-input-{captures.Count:D2}.png";using var file=File.Create(Path.Combine(directory,name));encoded.SaveTo(file);captures.Add(name);
                if(workspace.SelectedId!=null)touched.Add(workspace.SelectedId);
                fFrame|=counts.GetValueOrDefault("KeyDown:F")>0&&receipt!.Picks.Primitives.Any(p=>p.Entity.Value.ToString("D")==workspace.SelectedId&&
                    System.Numerics.Vector3.Distance(p.World.Translation,camera.Camera.Pivot)<.0001f);
                samples.Add(new{CameraRevision=camera.Camera.Revision,Selected=workspace.SelectedId,InputEvents=events,InvalidDraft=field?.HasError==true,
                    Frame=receipt!.Frame.Metadata.FrameId,receipt.LayoutRevision,Scale=current.Viewport.Scale,ProcessId=engine.Supervisor.Status.ProcessId,Surface=engine.Frames.Presentation!.Surface.Generation});
                revision=camera.Camera.Revision;selected=workspace.SelectedId;
            }
            var b=view.Canvas.ImageBounds;
            var report=new{Ready=ready,InputSource="SilkWindowHost.Input / Windows",InputEvents=events,Counts=counts,
                CameraRevision=camera.Camera.Revision,Distance=camera.Camera.Distance,Selected=workspace.SelectedId,
                InvalidDraft=field?.HasError==true,Draft=field?.Value,NativeCaptureEver=captureEver,Gesturing=view.ViewportController.IsGesturing,
                InvalidDraftDuringCamera=invalidCamera,FramedByNativeF=fFrame,DocumentUnchanged=unchanged,
                Pid=engine.Supervisor.Status.ProcessId,Surface=engine.Frames.Presentation?.Surface.Generation,Scale=current.Viewport.Scale,
                Image=new{b.X,b.Y,b.Width,b.Height},Captures=captures,Samples=samples};
            var path=Path.Combine(directory,"native-input-live.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);
            recorded=events;snapshotSignature=signature;
        }
        var listening=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PostWindow(host=>{native=host;native.Input+=Input;window.FramePresented+=Frame;Frame(ui);listening.TrySetResult();});
        await listening.Task.WaitAsync(token);
        try{await window.Completion.WaitAsync(token);}
        finally{native.Input-=Input;window.FramePresented-=Frame;}
        var passed=counts.GetValueOrDefault("PointerDown:0")>=3&&counts.GetValueOrDefault("PointerUp:0")>=3&&counts.GetValueOrDefault("Wheel")>0&&
            counts.GetValueOrDefault("KeyDown:F")>0&&counts.GetValueOrDefault("PointerUp:1")>0&&counts.GetValueOrDefault("PointerUp:2")>0&&
            captureEver&&invalid&&invalidCamera&&fFrame&&unchanged&&captures.Count>=3&&touched.Count>=2&&
            sameRuntime&&clear&&runtime.Leases.Count==1&&runtime.Leases.All(l=>l.ResourcesReleased);
        File.WriteAllText(Path.Combine(directory,"native-input.json"),JsonSerializer.Serialize(new{Passed=passed,InputSource="Windows SendInput / SilkWindowHost.Input",
            Counts=counts,NativeCaptureEver=captureEver,InvalidDraftObserved=invalid,InvalidDraftDuringCamera=invalidCamera,FramedByNativeF=fFrame,
            DocumentUnchanged=unchanged,SameRuntimeAndSurface=sameRuntime,BackgroundClear=clear,ProcessId=pid,SurfaceGeneration=surface,Children=runtime.Leases.Count,Reaped=runtime.Leases.Count(l=>l.ResourcesReleased),
            Captures=captures,Samples=samples,RightMiddleDrag="Not supplied by this observer; use a supported input driver."},new JsonSerializerOptions{WriteIndented=true}));
        Require(passed,"Native input profile incomplete. Inspect native-input-live.json and native-input.json.");
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
