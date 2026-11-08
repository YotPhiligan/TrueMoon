using System.Numerics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Exercises explicit engine/API updates. Directed orbit/pan/select input remains ED5a.3.
internal static class EditorPreviewCameraSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, EditorDocumentCommands commands, SceneDocumentSession document, AetherHost engine, NativeRuntimeFactory runtime, EditorSession editor)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token=deadline.Token;
        var opened=false;
        await ui.InvokeAsync(()=>opened=commands.Execute(DocumentCommand.Open,"Transform.aetherscene")).WaitAsync(token);
        Require(opened,"Camera fixture open was not admitted."); await commands.Pending.WaitAsync(token);
        if (commands.Read().Error is {} openError) throw openError;
        var original=document.Read().Content ?? throw new InvalidOperationException("Missing camera scene.");
        var source=PrimitiveSceneAdapter.Project(original.Resolved); var cube=source.CopyEntities().First(e=>e.Primitive!=null).Primitive!.ToPrimitive();
        await ui.InvokeAsync(()=>editor.Present(ui,view)).WaitAsync(token);
        var state=new PreviewViewState(new(document.Read().OpeningId,view.ViewportController!.ViewId,workspace.ContentToken,original.Document.Id.Value,original.Document.Revision),PreviewCamera.Default);
        await engine.Preview.ShowViewAsync(original.Resolved,state,640,360,token);
        var baseline=engine.Preview.PresentedView ?? throw new InvalidOperationException("No actual baseline receipt.");
        var pid=engine.Supervisor.Status.ProcessId; var generation=engine.Supervisor.Status.Identity!.Generation;
        var surface=baseline.Frame.Presentation.Surface.Generation; var selected=workspace.SelectedId;
        var captures=new List<string>(); var pixels=new List<object>();
        await Capture("camera-default.png",state,null);
        var camera=state.Camera;
        for (var i=1;i<=50;i++)
        {
            camera=camera.WithView(Matrix4x4.CreateTranslation(-2f*i/50,1,6),camera.Projection,camera.Pivot,camera.Distance);
            Require(engine.Preview.QueueCamera(state.Context,camera),"Camera queue rejected current context.");
        }
        await engine.Preview.CameraUpdates.WaitAsync(token); state=new(state.Context,camera);
        Require(engine.Supervisor.Status.ProcessId==pid && engine.Supervisor.Status.Identity!.Generation==generation &&
            engine.Frames.Presentation!.Surface.Generation==surface && runtime.Leases.Count==1,"Camera-only update replaced child/surface.");
        Require(!engine.Preview.IsCurrent(baseline),"Old camera remained current after navigation request.");
        await Capture("camera-moved.png",state,baseline);
        await engine.Preview.ResizeAsync(800,450,token); await Capture("camera-resized.png",state,null);
        await engine.Preview.ResizeAsync(0,0,token); Require(engine.Preview.PresentedView==null,"Suspended surface retained a presented receipt.");
        camera=camera.WithView(Matrix4x4.CreateTranslation(-3,1,6),camera.Projection,camera.Pivot,camera.Distance);
        Require(engine.Preview.QueueCamera(state.Context,camera),"Suspended latest camera was rejected.");
        await engine.Preview.ResizeAsync(640,360,token); await engine.Preview.CameraUpdates.WaitAsync(token); state=new(state.Context,camera);
        await Capture("camera-resumed.png",state,null);
        Require(ReferenceEquals(original,document.Read().Content) && !document.Read().IsDirty && workspace.SelectedId==selected,
            "Camera changed authoring/history/selection.");
        Require(engine.Supervisor.Status.Tick==0 && engine.Supervisor.Status.ProcessId==pid && engine.Supervisor.Status.Identity!.Generation==generation,
            "Camera/resize advanced simulation or replaced runtime.");
        await engine.Preview.StopAsync().WaitAsync(token);
        Require(engine.Preview.PresentedView==null && runtime.Leases.All(l=>l.ResourcesReleased),"Camera stop retained receipt/child.");
        File.WriteAllText(Path.Combine(directory,"camera.json"),JsonSerializer.Serialize(new {Passed=true,NativeWindow=true,
            ProductionCameraApi=true,NavigationInput=false,CameraSamples=50,LastCameraRevision=camera.Revision,ProcessId=pid,RuntimeGeneration=generation,
            InitialSurfaceGeneration=surface,Children=runtime.Leases.Count,Reaped=runtime.Leases.Count(l=>l.ResourcesReleased),
            DocumentUnchanged=ReferenceEquals(original,document.Read().Content),SelectionUnchanged=workspace.SelectedId==selected,Captures=captures,Pixels=pixels},new JsonSerializerOptions{WriteIndented=true}));
        await EditorHeaderSmoke.ClickActionAsync(ui,view,"file","file.exit",token); await window.Completion.WaitAsync(token);

        async Task Capture(string name, PreviewViewState expected, PreviewPresentedView? old)
        {
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                var receipt=editor.ReadPresentedView(view);
                if (current.NeedsUpdate || receipt?.State!=expected || !engine.Preview.IsCurrent(receipt) || !ReferenceEquals(receipt.Frame,view.Canvas.DrawnFrame)) return;
                window.FramePresented-=Presented;
                try
                {
                    using var image=current.ReadbackOpenGLImage(); using var bitmap=SKBitmap.FromImage(image);
                    Require(view.Canvas.MatchesPresentedLayout(receipt.ImageBounds,receipt.ClipBounds,receipt.LayoutRevision),"Presented layout became stale.");
                    Require(receipt.Projection.TryProject(cube.World.Translation,out var point),"Cube center failed projection.");
                    var bounds=receipt.ImageBounds; var x=(int)((bounds.Left+bounds.Width*(point.X+1)/2)*current.Viewport.Scale);
                    var y=(int)((bounds.Top+bounds.Height*(point.Y+1)/2)*current.Viewport.Scale); var pixel=bitmap.GetPixel(x,y);
                    using var data=image.Encode(SKEncodedImageFormat.Png,100); using var file=File.Create(Path.Combine(directory,name)); data.SaveTo(file);
                    Require(Math.Abs(pixel.Red-cube.Color.X*255)<=2 && Math.Abs(pixel.Green-cube.Color.Y*255)<=2 && Math.Abs(pixel.Blue-cube.Color.Z*255)<=2,
                        "Camera frame pixel disagrees with its actual captured view: "+pixel);
                    string? oldPixel=null;
                    if (old!=null)
                    {
                        Require(old.Projection.TryProject(cube.World.Translation,out var previous),"Old center failed projection.");
                        var previousPixel=bitmap.GetPixel((int)((bounds.Left+bounds.Width*(previous.X+1)/2)*current.Viewport.Scale),
                            (int)((bounds.Top+bounds.Height*(previous.Y+1)/2)*current.Viewport.Scale));
                        Require(previousPixel!=pixel,"New stamp did not move the actual cube pixels."); oldPixel=previousPixel.ToString();
                    }
                    captures.Add(name); pixels.Add(new {Name=name,X=x,Y=y,Pixel=pixel.ToString(),OldLocationPixel=oldPixel,
                        CameraRevision=receipt.State.Camera.Revision,Frame=receipt.Frame.Metadata.FrameId,Surface=receipt.Frame.Presentation.Surface.Generation,
                        receipt.LayoutRevision,Width=receipt.Projection.Width,Height=receipt.Projection.Height}); completion.TrySetResult();
                }
                catch(Exception error) { completion.TrySetException(error); }
            }
            window.FramePresented+=Presented; window.RequestFrame();
            try { await completion.Task.WaitAsync(token); } finally { window.FramePresented-=Presented; }
        }
    }
    private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
