using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed native/GPU lifecycle acceptance. Windows input is observed separately by EditorNativeInputSmoke.
internal static class EditorViewportAcceptanceSmoke
{
    internal static async Task RunAsync(string directory,HostedUiWindow window,UiSession ui,EditorView view,EditorWorkspace workspace,
        EditorDocumentCommands commands,AetherHost engine,NativeRuntimeFactory runtime,EditorSession editor,EditorPreview preview,EditorViewportState state,EditorScenePlayback playback)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(6));var token=deadline.Token;
        var inspector=view.Inspector?.DataContext??throw new InvalidOperationException("Inspector missing.");
        var model=view.DataContext??throw new InvalidOperationException("Model missing.");
        var cases=new List<string>();var captures=new List<object>();var root=commands.Read().Project!.Project.RootDirectory;
        var manifest=commands.Read().Project!.Project.ManifestPath;
        await Doc(DocumentCommand.Open,"Transform.aetherscene");await Show();await Capture("acceptance-default.png");
        var original=commands.Read().Document!.Content!;var ids=original.Document.Entities.Select(e=>e.Id).ToArray();Require(ids.Length==2,"Two cube fixture required.");
        var pid=engine.Supervisor.Status.ProcessId;var generation=engine.Supervisor.Status.Identity!.Generation;var surface=engine.Frames.Presentation!.Surface.Generation;
        var camera=state.Camera;var selected=workspace.SelectedId;
        await ui.InvokeAsync(()=>
        {
            inspector.Fields.Single(f=>f.Key=="x").Value="invalid";var p=Center();
            Require(ui.HandleInput(new(InputKind.PointerDown,p.X,p.Y,Button:1)).PointerCaptured,"Orbit capture rejected.");
            for(var i=1;i<=50;i++)ui.HandleInput(new(InputKind.PointerMove,p.X+i/2f,p.Y+i/5f));
            Require(editor.ReadPresentedView(view)==null,"Pending camera admitted old image.");
            ui.HandleInput(new(InputKind.PointerUp,p.X+25,p.Y+10,Button:1));
        });
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-burst.png");
        Require(engine.Supervisor.Status.ProcessId==pid&&engine.Supervisor.Status.Identity!.Generation==generation&&engine.Frames.Presentation!.Surface.Generation==surface&&
            runtime.Leases.Count==1&&engine.Supervisor.Status.Tick==0&&ReferenceEquals(original.Document,commands.Read().Document!.Content!.Document),"Camera burst replaced authoring/runtime/surface.");
        await ui.InvokeAsync(()=>Require(inspector.Fields.Single(f=>f.Key=="x").Value=="invalid","Camera burst lost invalid draft."));cases.Add("50cameraSamples/pending/draft/document/tick/sameChildSurface");
        await ui.InvokeAsync(()=>inspector.Cancel());
        await ui.InvokeAsync(()=>{var p=Center();Require(ui.HandleInput(new(InputKind.PointerDown,p.X,p.Y,Button:2)).PointerCaptured,"Pan capture rejected.");
            ui.HandleInput(new(InputKind.PointerMove,p.X+12,p.Y+6));ui.HandleInput(new(InputKind.PointerUp,p.X+12,p.Y+6,Button:2));});
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-pan.png");
        await ui.InvokeAsync(()=>{var p=Center();ui.HandleInput(new(InputKind.Wheel,p.X,p.Y,WheelDelta:1));});
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-dolly.png");
        await ui.InvokeAsync(()=>{ui.Focus(view.Canvas);Require(ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.F)).Handled,"Frame selected rejected.");});
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-frame.png");
        await ui.InvokeAsync(()=>{var r=engine.Preview.PresentedView!;var d=PreviewCamera.Default;Require(state.Queue(view.ViewportController!.ViewId,r.State.Context,
            state.Camera.WithView(d.World,d.Projection,d.Pivot,d.Distance)),"Restore navigation pose rejected.");});
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-restored.png");
        foreach(var cancellation in new[]{InputKind.KeyDown,InputKind.FocusLost})
        {
            await ui.InvokeAsync(()=>{var p=Center();var pose=state.Camera.World;ui.HandleInput(new(InputKind.PointerDown,p.X,p.Y,Button:1));
                ui.HandleInput(new(InputKind.PointerMove,p.X+15,p.Y+8));var revision=state.Camera.Revision;
                ui.HandleInput(new(cancellation,Key:cancellation==InputKind.KeyDown?UiKey.Escape:UiKey.None));
                Require(!view.ViewportController!.IsGesturing&&state.Camera.World==pose&&state.Camera.Revision>revision,"Gesture cancellation retained capture or reused revision.");});
            await engine.Preview.CameraUpdates.WaitAsync(token);
        }
        await Capture("acceptance-cancel.png");
        Require(engine.Supervisor.Status.ProcessId==pid&&engine.Frames.Presentation!.Surface.Generation==surface&&
            ReferenceEquals(original.Document,commands.Read().Document!.Content!.Document),"Pan/dolly/F/cancel changed document or runtime/surface.");cases.Add("pan/dolly/F/Esc/focusLoss/cameraOnly");
        var receipt=engine.Preview.PresentedView!;var layout=receipt.LayoutRevision;
        window.Resize(1100,740);await Until(async()=>{var ready=false;await ui.InvokeAsync(()=>ready=editor.ReadPresentedView(view) is {} r&&r.LayoutRevision>layout);return ready;});
        await Capture("acceptance-ui-resize.png");Require(ReferenceEquals(receipt.Frame.Presentation,engine.Frames.Presentation),"UI resize reallocated render surface.");
        await engine.Preview.ResizeAsync(360,360,token);await Capture("acceptance-square.png");
        await ui.InvokeAsync(()=>{var b=view.Canvas.Bounds;var s=workspace.SelectedId;var c=state.Camera;Click(b.X+4,b.Y+b.Height/2);ui.HandleInput(new(InputKind.Wheel,b.X+4,b.Y+b.Height/2,WheelDelta:1));Require(workspace.SelectedId==s&&state.Camera==c,"Letterbox changed selection/camera.");});
        await engine.Preview.ResizeAsync(640,360,token);await Capture("acceptance-render-resize.png");cases.Add("UIresize/renderAspect/letterbox/actualLayout");
        window.Minimize();await Until(async()=>{var hidden=false;await ui.InvokeAsync(()=>hidden=ui.Viewport.IsEmpty&&!state.IsVisible&&editor.ReadPresentedView(view)==null);return hidden;});
        var hiddenSurface=engine.Frames.Presentation;var hiddenPid=engine.Supervisor.Status.ProcessId;
        var before=state.Camera;Require(EditorCameraNavigation.TryDolly(before,1,out var latest),"Hidden camera math failed.");
        await ui.InvokeAsync(()=>Require(state.Queue(view.ViewportController!.ViewId,engine.Preview.DesiredView!.Context,latest),"Suspended latest candidate rejected."));
        Require(!engine.Preview.CameraUpdates.IsCompleted,"Suspended camera completed without presentation.");
        window.Restore();await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-ui-resumed.png");
        Require(engine.Supervisor.Status.ProcessId==hiddenPid&&ReferenceEquals(hiddenSurface,engine.Frames.Presentation),"UI suspension replaced child/surface.");cases.Add("nativeMinimize/latestCamera/resumeSameChildSurface");
        await engine.Preview.ResizeAsync(0,0,token);Require(engine.Preview.PresentedView==null,"Suspended surface retained receipt.");
        before=state.Camera;Require(EditorCameraNavigation.TryDolly(before,-1,out latest),"Dolly math failed.");
        await ui.InvokeAsync(()=>Require(state.Queue(view.ViewportController!.ViewId,engine.Preview.DesiredView!.Context,latest),"Physical suspend latest rejected."));
        await engine.Preview.ResizeAsync(640,360,token);await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("acceptance-surface-resumed.png");cases.Add("renderSuspend/latestCamera/resume");
        await ui.InvokeAsync(()=>
        {
            var p=Point(ids[1]);ui.HandleInput(new(InputKind.PointerDown,p.X,p.Y));model.Select(ids[0].Value.ToString("D"));
            var intent=workspace.SelectionRevision;ui.HandleInput(new(InputKind.PointerUp,p.X,p.Y));
            Require(workspace.SelectedId==ids[0].Value.ToString("D")&&workspace.SelectionRevision==intent,"Older click beat newer tree intent.");
        });cases.Add("newerSelectionIntent");
        camera=state.Camera;
        await ui.InvokeAsync(()=>
        {
            var p=Point(ids[1]);ui.HandleInput(new(InputKind.PointerDown,p.X,p.Y));var doc=commands.Read().Project!.Documents!;
            Require(doc.EditEntities(new DeleteEntitiesBatch(new(Guid.NewGuid(),doc.Read().Content!.Document.Revision,"acceptance-delete"),[ids[1]],EntityDeletionPolicy.Subtree)).Succeeded,"Delete failed.");
            editor.Present(ui,view);ui.HandleInput(new(InputKind.PointerUp,p.X,p.Y));Require(workspace.SelectedId!=ids[1].Value.ToString("D"),"Deleted pick was published.");
        });await preview.Pending.WaitAsync(token);await Capture("acceptance-deleted.png");
        await ui.InvokeAsync(()=>{var doc=commands.Read().Project!.Documents!;Require(doc.Undo(new(Guid.NewGuid(),doc.Read().Content!.Document.Revision,"acceptance-undo")).Succeeded,"Undo failed.");editor.Present(ui,view);});
        await preview.Pending.WaitAsync(token);await Capture("acceptance-undo.png");Require(state.Camera==camera,"Commit/Undo reset camera.");cases.Add("deletedEntity/oldClick/Undo/cameraPreserved");
        var old=engine.Preview.PresentedView!;var epoch=workspace.DocumentEpoch;
        await Doc(DocumentCommand.Open,"Transform.aetherscene");Require(workspace.DocumentEpoch!=epoch&&state.Camera==PreviewCamera.Default,"Same-ID reopen did not renew epoch/default camera.");
        Require(!engine.Preview.IsCurrent(old),"Old document receipt survived reopen.");await Show();await Capture("acceptance-reopened.png");cases.Add("sameIdReopen/defaultCamera/oldReceiptDenied");
        var owner=commands.Read().Project!;old=engine.Preview.PresentedView!;
        await Doc(DocumentCommand.OpenProject,Path.Combine(root,"Other","fixture.aetherproj"));
        Require(owner.IsClosed&&workspace.SelectedId==null&&commands.Read().Document?.Content==null&&!engine.Preview.IsCurrent(old),"Project switch retained old document/selection/receipt.");
        await Doc(DocumentCommand.Open,"Transform.aetherscene");await Show();await Capture("acceptance-project.png");cases.Add("projectSwitch/reap/identityReset");
        await ui.InvokeAsync(()=>{editor.Present(ui,view);Require(playback.RunSimulation(),"Play transition rejected.");});await playback.Pending.WaitAsync(token);
        if(playback.Error is {} playError)throw playError;Require(engine.Supervisor.Status.State==SessionState.Playing&&engine.Preview.PresentedView==null,"Play retained Edit receipt.");
        await ui.InvokeAsync(()=>{var p=Center();var s=workspace.SelectedId;var c=state.Camera;Click(p.X,p.Y);ui.HandleInput(new(InputKind.Wheel,p.X,p.Y,WheelDelta:1));Require(workspace.SelectedId==s&&state.Camera==c,"Playing accepted edit picking/camera.");});
        await ui.InvokeAsync(()=>Require(playback.Execute(EditorCommand.Stop),"Game Stop rejected."));await playback.Pending.WaitAsync(token);
        Require(engine.Supervisor.Status.State==SessionState.Stopped,"Game was not stopped.");cases.Add("Play/build/oldEditReceipt/inputDenied/Stop");
        await Doc(DocumentCommand.OpenProject,Path.Combine(root,"Bad","fixture.aetherproj"));await Doc(DocumentCommand.Open,"Transform.aetherscene");
        await ui.InvokeAsync(()=>{editor.Present(ui,view);Require(playback.RunSimulation(),"Bad entry startup not admitted for testing.");});await playback.Pending.WaitAsync(token);
        Require(playback.Error!=null&&engine.Supervisor.Status.State==SessionState.Stopped&&runtime.Leases.All(l=>l.ResourcesReleased),"Failed game activation leaked runtime or hid failure.");cases.Add("failedGameStartup/reaped");
        await Doc(DocumentCommand.OpenProject,manifest);await Doc(DocumentCommand.Open,"Transform.aetherscene");
        PreviewViewState captured=null!;await ui.InvokeAsync(()=>captured=state.Capture(commands.Read().Document!.Content!.Resolved));
        engine.Preview.SetPresentationSuspended(true);using(var cancel=new CancellationTokenSource())
        {
            var count=runtime.Leases.Count;var showing=engine.Preview.ShowViewAsync(commands.Read().Document!.Content!.Resolved,captured,640,360,cancel.Token);
            await Until(()=>Task.FromResult(runtime.Leases.Count>count&&engine.Frames.Presentation!=null));cancel.Cancel();
            try{await showing.WaitAsync(token);throw new InvalidOperationException("Cancelled first presentation completed.");}catch(OperationCanceledException)when(cancel.IsCancellationRequested) { }
        }
        engine.Preview.SetPresentationSuspended(false);Require(runtime.Leases.All(l=>l.ResourcesReleased),"Cancelled first presentation retained child.");cases.Add("cancelFirstPresentation/reaped");
        await Show();await Capture("acceptance-recovered.png");
        await preview.StopAsync().WaitAsync(token);Require(engine.Preview.PresentedView==null&&runtime.Leases.All(l=>l.ResourcesReleased),"Final Stop retained receipt/resources.");cases.Add("recovery/finalStop");
        File.WriteAllText(Path.Combine(directory,"viewport-acceptance.json"),JsonSerializer.Serialize(new{Passed=true,DirectedNativeUi=true,WindowsInput=false,
            Cases=cases,Captures=captures,Children=runtime.Leases.Count,Reaped=runtime.Leases.Count(l=>l.ResourcesReleased),Scale=ui.Viewport.Scale},new JsonSerializerOptions{WriteIndented=true}));
        await EditorHeaderSmoke.ClickActionAsync(ui,view,"file","file.exit",token);await window.Completion.WaitAsync(token);

        async Task Doc(DocumentCommand command,string path)
        {await ui.InvokeAsync(()=>Require(commands.Execute(command,path),"Document action rejected: "+command));await commands.Pending.WaitAsync(token);if(commands.Read().Error is {} e)throw e;await ui.InvokeAsync(()=>editor.Present(ui,view));}
        async Task Show()
        {await ui.InvokeAsync(()=>Require(preview.Show(commands.Read().Document!.Content!.Resolved),"Preview rejected."));await preview.Pending.WaitAsync(token);if(preview.Read().Error is {} e)throw e;}
        (float X,float Y) Center(){var b=view.Canvas.ImageBounds;return(b.X+b.Width/2,b.Y+b.Height/2);}
        (float X,float Y) Point(EntityId id)
        {var r=editor.ReadPresentedView(view)??throw new InvalidOperationException("No current receipt.");var cube=r.Picks.Primitives.Single(p=>p.Entity==id);Require(r.Projection.TryProject(cube.World.Translation,out var p),"Projection failed.");return((float)(r.ImageBounds.Left+(p.X+1)*r.ImageBounds.Width/2),(float)(r.ImageBounds.Top+(p.Y+1)*r.ImageBounds.Height/2));}
        void Click(float x,float y){ui.HandleInput(new(InputKind.PointerDown,x,y));ui.HandleInput(new(InputKind.PointerUp,x,y));}
        async Task Until(Func<Task<bool>> ready){while(!await ready()){token.ThrowIfCancellationRequested();await Task.Delay(10,token);}}
        async Task Capture(string name)
        {
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Frame(UiSession current)
            {
                var r=editor.ReadPresentedView(view);if(r==null||current.NeedsRender)return;
                window.FramePresented-=Frame;
                try
                {
                    using var image=current.ReadbackOpenGLImage();using var bitmap=SKBitmap.FromImage(image);
                    var cube=r.Picks.Primitives.FirstOrDefault(p=>p.Entity.Value.ToString("D")==workspace.SelectedId);
                    if(cube.Entity.Value==Guid.Empty)cube=r.Picks.Primitives[0];
                    Require(r.Projection.TryProject(cube.World.Translation,out var point),"Captured cube projection failed.");
                    var x=(int)((r.ImageBounds.Left+(point.X+1)*r.ImageBounds.Width/2)*current.Viewport.Scale);
                    var y=(int)((r.ImageBounds.Top+(point.Y+1)*r.ImageBounds.Height/2)*current.Viewport.Scale);
                    var color=bitmap.GetPixel(x,y);var expected=PrimitiveSceneAdapter.Project(commands.Read().Document!.Content!.Resolved).CopyEntities()
                        .Single(e=>e.Id==cube.Entity.Value.ToString("D")).Primitive!.ToPrimitive().Color;
                    Require(Math.Abs(color.Red-expected.X*255)<=2&&Math.Abs(color.Green-expected.Y*255)<=2&&Math.Abs(color.Blue-expected.Z*255)<=2,"Actual cube pixels differ from the captured view: "+color);
                    using var data=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(directory,name));data.SaveTo(file);
                    captures.Add(new{Name=name,Camera=r.State.Camera.Revision,r.State.Context.DocumentEpoch,r.State.Context.ContentId,Frame=r.Frame.Metadata.FrameId,r.LayoutRevision,
                        ProcessId=engine.Supervisor.Status.ProcessId,Surface=r.Frame.Presentation.Surface.Generation,Scale=current.Viewport.Scale,Width=r.Projection.Width,Height=r.Projection.Height,Pixel=color.ToString(),X=x,Y=y});
                    File.WriteAllText(Path.Combine(directory,"viewport-stage.json"),JsonSerializer.Serialize(new{Stage=name,Cases=cases,Children=runtime.Leases.Count}));done.TrySetResult();
                }
                catch(Exception e){done.TrySetException(e);}
            }
            window.FramePresented+=Frame;window.RequestFrame();try{await done.Task.WaitAsync(token);}finally{window.FramePresented-=Frame;}
        }
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
