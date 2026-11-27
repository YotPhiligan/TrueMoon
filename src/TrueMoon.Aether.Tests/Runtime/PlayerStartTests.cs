using System.Text.Json;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Runtime;

public sealed class PlayerStartTests
{
    private const string Hash="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    [Fact]
    public async Task Capture_FreezesExactSceneAndHash_AndWrongHostNeverLoadsTheGame()
    {
        var (build,scene)=Create();
        const string json="{\"formatVersion\":1,\"id\":\"local\",\"revision\":11,\"entities\":[]}";
        await File.WriteAllTextAsync(scene,json);
        var start=await PlayerStart.CaptureAsync(build.DirectoryPath,Hash,scene,true);
        await File.WriteAllTextAsync(scene,"changed");
        Assert.Equal(build.BuildId,start.Artifact.BuildId);
        Assert.Equal("local",start.Snapshot!.Id);Assert.Equal(11,start.Snapshot.Revision);Assert.Empty(start.Snapshot.CopyEntities());Assert.Null(start.World);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),start.SceneHash);
        await Assert.ThrowsAsync<InvalidDataException>(()=>PlayerStart.CaptureAsync(build.DirectoryPath,new('B',64),scene,true));
    }

    [Fact]
    public async Task WithCheckpoint_RequiresExactBuildHostAndSceneWithoutChangingFrozenStart()
    {
        var (build,scene)=Create();
        await File.WriteAllTextAsync(scene,"{\"formatVersion\":1,\"id\":\"local\",\"revision\":11,\"entities\":[]}");
        var start=await PlayerStart.CaptureAsync(build.DirectoryPath,Hash,scene,true);
        var otherHash=new string('B',64);
        var state=new GameCheckpointState(13,default,false);
        Assert.Throws<InvalidDataException>(()=>start.WithCheckpoint(new(Hash,otherHash,"local",11,state)));
        Assert.Throws<InvalidDataException>(()=>start.WithCheckpoint(new(otherHash,build.BuildId,"local",11,state)));
        Assert.Throws<InvalidDataException>(()=>start.WithCheckpoint(new(Hash,build.BuildId,"different",11,state)));
        Assert.Throws<InvalidDataException>(()=>start.WithCheckpoint(new(Hash,build.BuildId,"local",12,state)));
        var checkpoint=new GameCheckpoint(Hash,build.BuildId,"local",11,state);

        var attached=start.WithCheckpoint(checkpoint);

        Assert.Null(start.Checkpoint);
        Assert.Same(checkpoint,attached.Checkpoint);
        Assert.Same(start.Snapshot,attached.Snapshot);
        Assert.Same(start.Artifact,attached.Artifact);
        Assert.Equal(start.SceneHash,attached.SceneHash);
        Assert.Throws<InvalidOperationException>(()=>attached.WithCheckpoint(checkpoint));
    }
    [Theory]
    [InlineData("{\"formatVersion\":2,\"id\":\"x\",\"revision\":0,\"entities\":[]}",false)]
    [InlineData("{\"formatVersion\":1,\"id\":\"x\",\"revision\":0,\"revision\":1,\"entities\":[]}",false)]
    [InlineData("{\"formatVersion\":1,\"id\":\"x\",\"entities\":[]}",true)]
    [InlineData("{\"formatVersion\":1,\"id\":\"x\",\"revision\":0,\"entities\":[],\"unknown\":42}",true)]
    public async Task LegacyScene_RejectsVersionDuplicateMissingAndUnknownFields(string json,bool serializationError)
    {
        var (build,scene)=Create();await File.WriteAllTextAsync(scene,json);
        if(serializationError)await Assert.ThrowsAsync<JsonException>(()=>PlayerStart.CaptureAsync(build.DirectoryPath,Hash,scene,true));
        else await Assert.ThrowsAsync<InvalidDataException>(()=>PlayerStart.CaptureAsync(build.DirectoryPath,Hash,scene,true));
    }
    [Fact]
    public async Task EmptyOversizedAndCancelledCapture_AreRejectedBeforePublication()
    {
        var (build,scene)=Create();await File.WriteAllTextAsync(scene,"");
        await Assert.ThrowsAsync<InvalidDataException>(()=>PlayerStart.CaptureAsync(build.DirectoryPath,Hash,scene,true));
        using(var file=File.OpenWrite(scene))file.SetLength(SceneFileCodec.MaximumBytes+1L);
        await Assert.ThrowsAsync<InvalidDataException>(()=>PlayerStart.CaptureAsync(build.DirectoryPath,Hash,scene,true));
        using var end=new CancellationTokenSource();end.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>PlayerStart.CaptureAsync("does-not-exist",Hash,"does-not-exist",true,end.Token));
    }
    [Fact]
    public void PlayerFingerprint_RequiresDeliveredIdenticalSharedAssemblies()
    {
        var root=Path.Combine(Path.GetTempPath(),"TrueMoon-player-identity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var entry=typeof(ProbeGame).Assembly.Location;
        foreach(var assembly in new[]{typeof(PlayerStart).Assembly,typeof(IFactory<>).Assembly,typeof(ISystem).Assembly,typeof(TrueMoon.Enerit.IO.Pipes.PipesInvocationClient<>).Assembly,typeof(TrueMoon.Mithril.Units.ChildProcessUnitHandle).Assembly,typeof(ProbeGame).Assembly})
            File.Copy(assembly.Location,Path.Combine(root,Path.GetFileName(assembly.Location)));
        var path=Path.Combine(root,Path.GetFileName(entry));var id=BuildIdentity.ForPlayer(path);Assert.Equal(BuildIdentity.ForPlayer(entry),id);
        File.AppendAllText(path,"different entry");Assert.NotEqual(id,BuildIdentity.ForPlayer(path));
        var neutral=Path.Combine(root,Path.GetFileName(typeof(ISystem).Assembly.Location));
        File.AppendAllText(neutral,"different Abstractions");Assert.Throws<InvalidDataException>(()=>BuildIdentity.ForPlayer(path));
        File.Copy(typeof(ISystem).Assembly.Location,neutral,true);
        File.AppendAllText(Path.Combine(root,Path.GetFileName(typeof(PlayerStart).Assembly.Location)),"tamper");
        Assert.Throws<InvalidDataException>(()=>BuildIdentity.ForPlayer(path));
    }
    private static (GameBuildArtifact Build,string Scene) Create()
    {
        var root=Path.Combine(Path.GetTempPath(),"TrueMoon-player-capture-"+Guid.NewGuid().ToString("N"));
        var build=GameArtifactPublisher.Publish(Path.GetDirectoryName(typeof(ProbeGame).Assembly.Location)!,root,Hash,"Game.Logic.dll","Game.Logic.ProbeGame",1, runtimeAccess: true);
        return(build,Path.Combine(root,"scene.json"));
    }
}
