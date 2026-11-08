using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed UiSession input on a real native/GPU window. Physical host input is a separate ED5a.4 gate.
internal static class EditorViewportInputSmoke
{
    internal static async Task RunAsync(string directory,HostedUiWindow window,UiSession ui,EditorView view,EditorWorkspace workspace,
        EditorDocumentCommands commands,SceneDocumentSession document,AetherHost engine,NativeRuntimeFactory runtime,EditorSession editor,
        EditorPreview coordinator,EditorViewportState state)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));var token=deadline.Token;
        await ui.InvokeAsync(()=>Require(commands.Execute(DocumentCommand.Open,"Transform.aetherscene"),"Open rejected."));
        await commands.Pending.WaitAsync(token);if(commands.Read().Error is {} error)throw error;
        await ui.InvokeAsync(()=>{editor.Present(ui,view);Require(coordinator.Show(document.Read().Content!.Resolved),"Show rejected.");});
        await coordinator.Pending.WaitAsync(token);if(coordinator.Read().Error is {} showError)throw showError;
        var original=document.Read().Content!;var epoch=document.Read().OpeningId;
        var pid=engine.Supervisor.Status.ProcessId;var generation=engine.Supervisor.Status.Identity!.Generation;
        var surface=engine.Frames.Presentation!.Surface.Generation;var ids=original.Document.Entities.Select(e=>e.Id).ToArray();
        Require(ids.Length==2,"Navigation fixture needs two independent cubes.");var captures=new List<object>();
        var colorExpected=TrueMoon.Aether.Scenes.PrimitiveSceneAdapter.Project(original.Resolved).CopyEntities()
            .Single(e=>e.Id==ids[1].Value.ToString("D")).Primitive!.ToPrimitive().Color;
        await Capture("navigation-default.png");
        await ui.InvokeAsync(()=>
        {
            var p=Point(ids[1]);Click(p.X,p.Y);Require(workspace.SelectedId==ids[1].Value.ToString("D"),"Second cube click did not select.");
            Require(view.Hierarchy!.DataContext.SelectedId==workspace.SelectedId&&view.Inspector!.DataContext.HasSelection,"Tree/inspector selection differs.");
            var field=view.Inspector.DataContext.Fields.Single(f=>f.Key=="x");field.Value="invalid";
            p=Point(ids[1]);Click(p.X,p.Y);Require(field.Value=="invalid"&&field.HasError,"Same-ID click lost the draft.");
            p=Point(ids[0]);Click(p.X,p.Y);Require(workspace.SelectedId==ids[0].Value.ToString("D")&&!view.Inspector.DataContext.HasDraft,"Different selection retained the draft.");
            var b=view.Canvas.ImageBounds;Click(b.X+b.Width/2,b.Y+12);Require(workspace.SelectedId==null&&!view.Inspector.DataContext.HasSelection,"Background did not clear.");
            p=Point(ids[1]);Click(p.X,p.Y);view.Inspector.DataContext.Fields.Single(f=>f.Key=="x").Value="invalid";
        });
        await Gesture(1,24,10);await Capture("navigation-orbit.png");
        await Gesture(2,12,6);await Capture("navigation-pan.png");
        await ui.InvokeAsync(()=>{var b=view.Canvas.ImageBounds;ui.HandleInput(new(InputKind.Wheel,b.X+b.Width/2,b.Y+b.Height/2,WheelDelta:1));});
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("navigation-dolly.png");
        await ui.InvokeAsync(()=>{Require(view.Inspector!.DataContext.Fields.Single(f=>f.Key=="x").Value=="invalid","Navigation changed the draft.");ui.Focus(view.Canvas);Require(ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.F)).Handled,"F was not handled.");});
        await engine.Preview.CameraUpdates.WaitAsync(token);await Capture("navigation-frame.png");
        var before=state.Camera;
        await ui.InvokeAsync(()=>{var b=view.Canvas.ImageBounds;var x=b.X+b.Width/2;var y=b.Y+b.Height/2;ui.HandleInput(new(InputKind.PointerDown,x,y,Button:1));ui.HandleInput(new(InputKind.PointerMove,x+20,y+8));ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.Escape));Require(!view.ViewportController!.IsGesturing,"Esc retained capture.");});
        await engine.Preview.CameraUpdates.WaitAsync(token);Require(state.Camera.World==before.World&&state.Camera.Revision>before.Revision,"Esc did not restore with a new revision.");
        await Capture("navigation-cancel.png");
        Require(ReferenceEquals(original.Document,document.Read().Content!.Document)&&!document.Read().IsDirty&&document.Read().Content!.UndoCount==original.UndoCount,"Navigation/selection changed document/history.");
        Require(engine.Supervisor.Status.ProcessId==pid&&engine.Supervisor.Status.Identity!.Generation==generation&&engine.Frames.Presentation!.Surface.Generation==surface&&engine.Supervisor.Status.Tick==0&&runtime.Leases.Count==1,"Camera-only input replaced runtime/surface or advanced tick.");
        await coordinator.StopAsync().WaitAsync(token);
        Require(runtime.Leases.All(l=>l.ResourcesReleased),"Stop retained a child.");
        File.WriteAllText(Path.Combine(directory,"navigation.json"),JsonSerializer.Serialize(new{Passed=true,NativeWindow=true,DirectedUiInput=true,PhysicalInput=false,
            ProcessId=pid,RuntimeGeneration=generation,SurfaceGeneration=surface,OpeningId=epoch,Children=runtime.Leases.Count,Reaped=runtime.Leases.Count(l=>l.ResourcesReleased),
            CameraRevision=state.Camera.Revision,DocumentUnchanged=true,HistoryUnchanged=true,DraftPreserved=true,HitSameDifferentAndMiss=true,Captures=captures},new JsonSerializerOptions{WriteIndented=true}));
        await EditorHeaderSmoke.ClickActionAsync(ui,view,"file","file.exit",token);await window.Completion.WaitAsync(token);

        (float X,float Y) Point(TrueMoon.Aether.Scenes.EntityId id)
        {
            var receipt=editor.ReadPresentedView(view)??throw new InvalidOperationException("No current presented view.");
            var cube=receipt.Picks.Primitives.Single(p=>p.Entity==id);Require(receipt.Projection.TryProject(cube.World.Translation,out var p),"Projection failed.");
            return((float)(receipt.ImageBounds.Left+(p.X+1)*receipt.ImageBounds.Width/2),(float)(receipt.ImageBounds.Top+(p.Y+1)*receipt.ImageBounds.Height/2));
        }
        void Click(float x,float y){ui.HandleInput(new(InputKind.PointerDown,x,y));ui.HandleInput(new(InputKind.PointerUp,x,y));}
        async Task Gesture(int button,float dx,float dy)
        {
            await ui.InvokeAsync(()=>{var b=view.Canvas.ImageBounds;var x=b.X+b.Width/2;var y=b.Y+b.Height/2;
                Require(ui.HandleInput(new(InputKind.PointerDown,x,y,Button:button)).PointerCaptured,"Navigation capture rejected.");
                ui.HandleInput(new(InputKind.PointerMove,x+dx,y+dy));ui.HandleInput(new(InputKind.PointerUp,x+dx,y+dy,Button:button));});
            await engine.Preview.CameraUpdates.WaitAsync(token);
        }
        async Task Capture(string name)
        {
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                var receipt=editor.ReadPresentedView(view);if(current.NeedsUpdate||receipt?.State.Camera!=state.Camera||!engine.Preview.IsCurrent(receipt))return;
                window.FramePresented-=Presented;
                try
                {
                    using var image=current.ReadbackOpenGLImage();using var bitmap=SKBitmap.FromImage(image);
                    var cube=receipt.Picks.Primitives.Single(p=>p.Entity==ids[1]);var p=Point(ids[1]);
                    var color=bitmap.GetPixel((int)(p.X*current.Viewport.Scale),(int)(p.Y*current.Viewport.Scale));
                    Require(Math.Abs(color.Red-colorExpected.X*255)<=2&&Math.Abs(color.Green-colorExpected.Y*255)<=2&&Math.Abs(color.Blue-colorExpected.Z*255)<=2,"Actual navigation pixels differ: "+color);
                    using var data=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(directory,name));data.SaveTo(file);
                    captures.Add(new{Name=name,Pixel=color.ToString(),receipt.State.Camera.Revision,Frame=receipt.Frame.Metadata.FrameId,receipt.LayoutRevision});done.TrySetResult();
                }
                catch(Exception e){done.TrySetException(e);}
            }
            window.FramePresented+=Presented;window.RequestFrame();try{await done.Task.WaitAsync(token);}finally{window.FramePresented-=Presented;}
        }
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
