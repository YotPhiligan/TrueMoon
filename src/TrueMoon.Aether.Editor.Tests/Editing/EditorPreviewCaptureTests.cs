using System.Threading.Channels;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorPreviewCaptureTests
{
    [Fact]
    public async Task ExplicitCapture_RunsOnRequestOwnerAndCoalescesFrozenContextOnlyChanges()
    {
        using var authoring=DocumentData.Factory().Create(DocumentData.Empty());var scene=authoring.Read().Resolved;
        var owner=Environment.CurrentManagedThreadId;var capturedThreads=new List<int>();
        var context=new PreviewViewContext(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),scene.Id.Value,scene.Revision);
        var camera=PreviewCamera.Default;var calls=Channel.CreateUnbounded<(PreviewViewState View,TaskCompletionSource<ScenePreviewDocument> Complete)>();
        await using var preview=new EditorPreview(s=>{Assert.Same(scene,s);capturedThreads.Add(Environment.CurrentManagedThreadId);return new(context,camera);},
            (s,v,_)=>{var complete=new TaskCompletionSource<ScenePreviewDocument>(TaskCreationOptions.RunContinuationsAsynchronously);calls.Writer.TryWrite((v,complete));return complete.Task;},()=>Task.CompletedTask);
        preview.Show(scene);Assert.Equal(new[]{owner},capturedThreads);var first=await calls.Reader.ReadAsync();var initial=context;
        // After an await the test has no UI affinity; each capture must still run synchronously on its requesting thread.
        capturedThreads.Clear();var requestThread=Environment.CurrentManagedThreadId;
        for(var i=0;i<50;i++){context=context with {ContentId=Guid.NewGuid()};preview.Synchronize(scene);}
        Assert.All(capturedThreads,id=>Assert.Equal(requestThread,id));Assert.Equal(50,capturedThreads.Count);Assert.False(calls.Reader.TryRead(out _));
        Assert.Equal(initial,first.View.Context);first.Complete.SetResult(EditingFixture.Receipt(scene));
        var last=await calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(context,last.View.Context);
        last.Complete.SetResult(EditingFixture.Receipt(scene));await preview.Pending.WaitAsync(TimeSpan.FromSeconds(5));
        EditorCameraNavigation.TryDolly(camera,1,out camera);preview.Synchronize(scene);
        Assert.False(calls.Reader.TryRead(out _));Assert.Null(preview.Read().Error);
    }
}
