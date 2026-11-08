using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

// Real document, routing and protocol; only the child process and GPU presentation are deterministic doubles.
internal sealed class ViewportInputFixture : IDisposable
{
    internal readonly DocumentUiFixture Owner=new();
    internal readonly EditorWorkspace Workspace=new(new("input",0,[]));
    internal readonly RuntimeFactory Runtime=new();
    internal readonly Transport Viewport=new();
    internal readonly SessionSupervisor Supervisor;
    internal readonly FrameSession Frames;
    internal readonly ScenePreview Preview;
    internal readonly EditorViewportState State;
    internal readonly EditorPreview Coordinator;
    internal readonly EditorInspectorViewModel Inspector;
    internal readonly EditorDocumentViewModel Documents;
    internal readonly EditorCommands Commands=new((_,_)=>Task.CompletedTask,CancellationToken.None);
    internal readonly EditorViewModel Model;
    internal readonly EditorView View;
    internal readonly UiSession Ui;
    internal EditorViewportController Controller=>View.ViewportController!;
    internal SceneAuthoringState Content=>Owner.Document.Read().Content!;
    internal readonly TypographyRecordingContext Drawing=new();
    private ulong _frame;
    internal static readonly TimeSpan Deadline=TimeSpan.FromSeconds(5);
    internal ViewportInputFixture(bool wrap=false)
    {
        var source=DocumentData.Scene();
        EntityInstance Entity(EntityId id,double x)=>new(id,DocumentData.Base,new(propertyOverrides:
            [new(DocumentData.Transform,"x",PropertyValue.From(x)),new(DocumentData.Transform,"y",PropertyValue.From(1d))]));
        Owner.Store.File=new(new(source.Id,source.Revision,[Entity(DocumentData.Parent,-1.2),Entity(DocumentData.Child,1.2)],source.Definitions,source.Schemas),new("input"));
        Owner.Run(DocumentCommand.Open,"input.aetherscene");Workspace.SetProject(Owner.Projects.Active);SyncSource();
        Supervisor=new(Runtime,new('A',64),new('B',64),"input-tests",new(){HeartbeatInterval=TimeSpan.FromMinutes(1)});
        Frames=new(Supervisor,Viewport);Preview=new(Frames);State=new(Workspace,Preview);
        Coordinator=new(State.Capture,(scene,view,token)=>Preview.ShowViewAsync(scene,view,640,360,token),Preview.StopAsync);
        Inspector=new(Owner.Commands,Coordinator,Workspace);Documents=new(Owner.Commands);
        Model=new(Workspace,Commands,Documents,Inspector);View=new(Model,new EditorViewStyle());
        Ui=InspectorFixture.Ui(wrap?new OverlayHost(View):View);Ui.Update();
        View.EnsureViewportController(new Factory<EditorViewportController,EditorViewportArguments>(a=>new(a,State,Workspace,Owner.Commands,Model,Coordinator,Preview)));
        Controller.Observe(Ui);Ui.Update();
    }
    internal void SyncSource()=>Workspace.SetDocument(Content,Owner.Document.Read().OpeningId);
    internal ViewportFrame Issue(PreviewViewState state)=>Frames.Presentation!.Issue(new(0,++_frame,0,0,1){PreviewView=state.Stamp});
    internal void Start()
    {
        Assert.True(Coordinator.Show(Content.Resolved));WaitUntil(()=>Frames.Presentation!=null&&Preview.DesiredView!=null);
        Present();DocumentUiFixture.Wait(Coordinator.Pending);Assert.Null(Coordinator.Read().Error);
    }
    internal void Present(Task? transition=null)
    {
        var view=Preview.DesiredView!;
        var acknowledged=Preview.PresentedView?.State.Camera.Revision??0;
        for(var i=0;i<4;i++)
        {
            PreviewViewState actual=null!;ViewportFrame frame=null!;
            WaitUntil(()=>
            {
                if(Runtime.LastView is not {} candidate||candidate.Camera.Revision<=acknowledged&&candidate.Stamp!=view.Stamp)return false;
                var ticket=Issue(candidate);
                if(Preview.Describe(ticket)==null)return false;actual=candidate;frame=ticket;return true;
            });
            if(!DrawAndAck(frame))continue;acknowledged=actual.Camera.Revision;
            if(actual.Stamp==view.Stamp)break;
        }
        if(Coordinator.Read().Busy)DocumentUiFixture.Wait(Coordinator.Pending);
        if(transition!=null)DocumentUiFixture.Wait(transition);
        DocumentUiFixture.Wait(Preview.CameraUpdates);Assert.True(Preview.IsCurrent(Preview.PresentedView!));
    }
    internal bool DrawAndAck(ViewportFrame frame,bool acknowledge=true)
    {
        Ui.Update();Controller.Observe(Ui);
        var surface=frame.Presentation;
        View.Canvas.Upload(SkiaImageSource.CopyBgraPixels(surface.Surface.Width,surface.Surface.Height,new byte[surface.Surface.Width*surface.Surface.Height*4]),frame,()=>ReferenceEquals(Frames.Presentation,surface));
        View.Canvas.Draw(Drawing);
        var b=View.Canvas.ImageBounds;var c=View.Canvas.DrawnClipBounds;
        // A worker may advance while the UI draws the previous ticket. Production also rejects that stale ack and waits for the next frame.
        return acknowledge&&Preview.Presented(frame,new(b.X,b.Y,b.Width,b.Height),new(c.X,c.Y,c.Width,c.Height),View.Canvas.LayoutRevision);
    }
    internal (float X,float Y) Point(EntityId entity)
    {
        var receipt=Preview.PresentedView!;var primitive=receipt.Picks.Primitives.Single(p=>p.Entity==entity);
        Assert.True(receipt.Projection.TryProject(primitive.World.Translation,out var p));var b=receipt.ImageBounds;
        return ((float)(b.Left+(p.X+1)*b.Width/2),(float)(b.Top+(p.Y+1)*b.Height/2));
    }
    internal (float X,float Y) Center {get {var b=View.Canvas.ImageBounds;return(b.X+b.Width/2,b.Y+b.Height/2);}}
    internal UiInputResult Input(InputKind kind,float x,float y,int button=0,float wheel=0)=>Ui.HandleInput(new(kind,x,y,Button:button,WheelDelta:wheel));
    internal void Click(float x,float y){Input(InputKind.PointerDown,x,y);Input(InputKind.PointerUp,x,y);}
    internal void Drag(int button,float dx=24,float dy=10)
    {var p=Center;Assert.True(Input(InputKind.PointerDown,p.X,p.Y,button).Handled);Assert.True(Controller.IsGesturing);Input(InputKind.PointerMove,p.X+dx,p.Y+dy,button);}
    internal static void WaitUntil(Func<bool> ready)=>Assert.True(SpinWait.SpinUntil(ready,Deadline),"Tracked preview transition did not complete.");
    public void Dispose()
    {
        Ui.Dispose();View.Dispose();DocumentUiFixture.Wait(Coordinator.StopAsync());Model.Dispose();Inspector.Dispose();Documents.Dispose();Coordinator.Dispose();State.Dispose();
        DocumentUiFixture.Wait(Preview.DisposeAsync().AsTask());DocumentUiFixture.Wait(Frames.DisposeAsync().AsTask());DocumentUiFixture.Wait(Supervisor.DisposeAsync().AsTask());Owner.Dispose();
    }
    internal sealed class RuntimeFactory : IRuntimeFactory
    {
        internal readonly List<Lease> Leases=[];
        internal volatile PreviewViewState? LastView;
        public IRuntimeLease Create(SessionLaunch launch){var lease=new Lease(this,launch);Leases.Add(lease);return lease;}
    }
    internal sealed class Lease(RuntimeFactory owner,SessionLaunch launch):IRuntimeLease
    {
        internal readonly PreviewGameSession Game=new();
        internal RuntimeProtocol Protocol=null!;
        private readonly TaskCompletionSource _exit=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId=>900;
        public Task Exited=>_exit.Task;
        public bool ResourcesReleased{get;private set;}
        public Task LaunchAsync(CancellationToken token){token.ThrowIfCancellationRequested();Protocol=new(launch.Identity,Game,900,role:launch.Role);return Task.CompletedTask;}
        public async Task<ControlReply> SendAsync(ControlRequest request,CancellationToken token)
        {var reply=await Protocol.SendAsync(request,token);if(request.PreviewView!=null)owner.LastView=PreviewViewCodec.Decode(request.PreviewView);return reply;}
        public Task StopAsync(CancellationToken gracefulDeadline=default){_exit.TrySetResult();return Task.CompletedTask;}
        public ValueTask DisposeAsync(){Game.Dispose();ResourcesReleased=true;return ValueTask.CompletedTask;}
    }
    internal sealed class Transport : IGameViewportTransport
    {
        public ViewportTransportKind Kind=>ViewportTransportKind.Gpu;
        public Task<IGameViewportConnection> ConnectAsync(IRuntimeLease runtime,CancellationToken token)=>Task.FromResult<IGameViewportConnection>(new Connection());
        private sealed class Connection:IGameViewportConnection
        {
            private TaskCompletionSource _end=new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task Completion=>_end.Task;
            public Task ConfigureAsync(ViewportPresentation presentation,CancellationToken token){_end.TrySetCanceled();_end=new(TaskCreationOptions.RunContinuationsAsynchronously);return Task.CompletedTask;}
            public ValueTask DisposeAsync(){_end.TrySetCanceled();return ValueTask.CompletedTask;}
        }
    }
}
