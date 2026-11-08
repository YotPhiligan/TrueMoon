using System.Numerics;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class PreviewViewProtocolTests
{
    private static SessionIdentity Identity => new(Guid.NewGuid(), 1, new('A',64),new('B',64),new('C',64));
    private static SceneSnapshot Scene(PreviewViewState view) => new(view.Context.SceneId.ToString("D"), view.Context.SceneRevision,
        [new() { Id = Guid.NewGuid().ToString("D"), Primitive = ScenePrimitive.FromPrimitive(new(Matrix4x4.Identity, Vector3.One)) }]);

    [Fact]
    public async Task Command_InstallsInitialViewAndCapturesImmutableSceneStampBeforeCompletion()
    {
        var id = Identity; var view = PreviewViewCodecTests.View(); var scene = Scene(view);
        using var game = new PreviewGameSession(); using var protocol = new RuntimeProtocol(id, game, 100, role: RuntimeRole.EditPreview);
        var hello = await protocol.SendAsync(ControlRequest.Create(id,1,CommandKind.Hello)); Assert.Equal(1, hello.PreviewViewVersion); Assert.Null(hello.PreviewViewStamp);
        var load = ControlRequest.Create(id,2,CommandKind.LoadScene); load.SceneId = scene.Id; load.Revision = scene.Revision; load.Entities = scene.CopyEntities(); load.PreviewView = PreviewViewCodec.Encode(view);
        var ready = await protocol.SendAsync(load); Assert.Equal(view.Stamp, PreviewViewCodec.ReadStamp(ready.PreviewViewStamp!));
        var before = protocol.CaptureRenderFrame(); var gpuStamp = new GpuFrameStamp(id, 1, 1, 0, 2);
        var produced = GpuFrameData.FromScene(gpuStamp, before.Tick, before.Scene); produced.Validate(gpuStamp, 32,16);
        var camera = view.Camera.WithView(Matrix4x4.CreateTranslation(0, 2, 6), view.Camera.Projection, view.Camera.Pivot, 6);
        var next = new PreviewViewState(view.Context, camera); var update = ControlRequest.Create(id,3,CommandKind.SetPreviewView); update.PreviewView = PreviewViewCodec.Encode(next);
        var ack = await protocol.SendAsync(update);
        Assert.Equal(0, ack.Tick); Assert.Equal(7, ack.Revision); Assert.Equal("Paused", ack.Phase); Assert.Equal(next.Stamp, PreviewViewCodec.ReadStamp(ack.PreviewViewStamp!));
        var after = protocol.CaptureRenderFrame(); Assert.Equal(next, after.Scene.PreviewView); Assert.Equal(view, before.Scene.PreviewView);
        Assert.Equal(view.Stamp, produced.Metadata.PreviewView); Assert.NotEqual(next.Stamp, produced.Metadata.PreviewView);
        Assert.Equal(before.Scene.Primitives, after.Scene.Primitives); Assert.Equal(0ul, game.TickNumber);
        Assert.Equal(next.Camera.CreateProjection(32,16).ViewProjection, after.Scene.CreateViewProjection(32,16));
        Assert.Throws<InvalidDataException>(() => (produced with { Metadata = produced.Metadata with { PreviewView = next.Stamp } }).Validate(gpuStamp,32,16));
        Assert.Null(GpuFrameData.GamePattern(gpuStamp,32,16,0,1).Metadata.PreviewView);
    }

    [Theory]
    [InlineData("old-revision")] [InlineData("reused-revision")] [InlineData("document")] [InlineData("view")]
    [InlineData("content")] [InlineData("scene")] [InlineData("scene-revision")] [InlineData("malformed")]
    public async Task Command_InvalidUpdatePreservesWholePreviousCapture(string kind)
    {
        var id = Identity; var view = PreviewViewCodecTests.View(3); var scene = Scene(view);
        using var game = new PreviewGameSession(); using var protocol = new RuntimeProtocol(id,game,100,role:RuntimeRole.EditPreview);
        await protocol.SendAsync(ControlRequest.Create(id,1,CommandKind.Hello));
        var load = ControlRequest.Create(id,2,CommandKind.LoadScene); load.SceneId=scene.Id; load.Revision=7; load.Entities=scene.CopyEntities(); load.PreviewView=PreviewViewCodec.Encode(view); await protocol.SendAsync(load);
        var context = kind switch {
            "document" => view.Context with { DocumentEpoch=Guid.NewGuid() }, "view" => view.Context with { ViewId=Guid.NewGuid() },
            "content" => view.Context with { ContentId=Guid.NewGuid() }, "scene" => view.Context with { SceneId=Guid.NewGuid() },
            "scene-revision" => view.Context with { SceneRevision=8 }, _ => view.Context };
        var camera = new PreviewCamera(Matrix4x4.Identity,view.Camera.Projection,view.Camera.Pivot,6,kind == "old-revision" ? 2ul : kind == "reused-revision" ? 3ul : 4ul);
        var update = ControlRequest.Create(id,3,CommandKind.SetPreviewView); update.PreviewView = kind == "malformed" ? [1] : PreviewViewCodec.Encode(new(context,camera));
        await Assert.ThrowsAsync<InvalidDataException>(() => protocol.SendAsync(update));
        Assert.Equal(view, protocol.CaptureRenderFrame().Scene.PreviewView); Assert.Equal(0ul,game.TickNumber); Assert.Equal(7,game.Revision);
    }

    [Fact]
    public async Task Command_RejectsGameAndLegacyViewUpdatesAndForeignLoadContext()
    {
        foreach (var role in new[] { RuntimeRole.Game,RuntimeRole.EditPreview })
        {
            var id=Identity; var view=PreviewViewCodecTests.View(); var scene=Scene(view);
            using var game=new PreviewGameSession(); using var protocol=new RuntimeProtocol(id,game,100,role:role);
            var hello=await protocol.SendAsync(ControlRequest.Create(id,1,CommandKind.Hello)); Assert.Equal(role == RuntimeRole.EditPreview ? 1 : 0, hello.PreviewViewVersion);
            var load=ControlRequest.Create(id,2,CommandKind.LoadScene); load.SceneId=scene.Id; load.Revision=7; load.Entities=scene.CopyEntities(); await protocol.SendAsync(load);
            var update=ControlRequest.Create(id,3,CommandKind.SetPreviewView); update.PreviewView=PreviewViewCodec.Encode(view);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>protocol.SendAsync(update)); Assert.Null(game.View);
        }
        var state=PreviewViewCodecTests.View(); using var standalone=new PreviewGameSession();
        Assert.Throws<InvalidDataException>(()=>standalone.Load(new(Guid.NewGuid().ToString("D"),7,[]),state));
        Assert.Throws<InvalidOperationException>(()=>standalone.CaptureRenderScene());
    }
}
