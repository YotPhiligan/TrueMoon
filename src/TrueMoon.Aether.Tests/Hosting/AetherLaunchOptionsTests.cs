using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Aether.Tests.Hosting;

public sealed class AetherLaunchOptionsTests
{
    [Fact]
    public async Task CombinedDefaultsToEditor_PlayerOnlyRequiresExplicitCapturedInputs_WithoutOpeningPaths()
    {
        Assert.Equal(AetherLaunchRole.Editor,(await AetherLaunchOptions.ParseAsync([])).Role);
        await Assert.ThrowsAsync<ArgumentException>(()=>AetherLaunchOptions.ParseAsync([],AetherLaunchRole.Player));
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var player=await AetherLaunchOptions.ParseAsync(["--player","--artifact",path,"--scene",path+".aetherscene","--width=320","--height","180"]);
        Assert.Equal(AetherLaunchRole.Player,player.Role);Assert.Equal(path,player.ArtifactPath);Assert.Equal(320,player.Width);Assert.Equal(180,player.Height);
        Assert.False(Directory.Exists(path));Assert.False(File.Exists(player.ScenePath));
        Assert.Equal(AetherLaunchRole.Player,(await AetherLaunchOptions.ParseAsync(["--help"],AetherLaunchRole.Player)).Role);
    }
    [Theory]
    [InlineData("--editor --player")][InlineData("--player --runtime")][InlineData("--editor --runtime")]
    [InlineData("--editor --editor")][InlineData("--editor=false")][InlineData("--editor --width=100")]
    [InlineData("--player --artifact=a --scene=b --project=game.aetherproj")][InlineData("--player --artifact=a --scene=b --runtime-host=runtime.exe")]
    [InlineData("--unknown")][InlineData("--editor stray")][InlineData("--player --artifact --scene=scene")]
    [InlineData("--player --artifact=a --scene=b --width=15")][InlineData("--player --artifact=a --scene=b --height=1081")]
    [InlineData("--player --artifact=a --scene=b --scene-format=other")]
    public async Task AmbiguousDuplicateUnknownAndRoleSpecificArguments_AreRejected(string command)
    {
        var error=await Record.ExceptionAsync(()=>AetherLaunchOptions.ParseAsync(command.Split(' ')));
        Assert.True(error is ArgumentException or ConfigurationException, $"Expected a launch validation error, got {error}");
    }
    [Fact]
    public async Task ProtectedChildIdentity_CannotBecomePlayerOrEditor_AndChildNeedsBootstrap()
    {
        await Assert.ThrowsAsync<InvalidDataException>(()=>AetherLaunchOptions.ParseAsync(["--runtime"]));
        await Assert.ThrowsAsync<InvalidDataException>(()=>AetherLaunchOptions.ParseAsync(["--editor"],hasRuntimeBootstrap:true));
        await Assert.ThrowsAsync<InvalidDataException>(()=>AetherLaunchOptions.ParseAsync(["--player","--artifact=a","--scene=b"],hasRuntimeBootstrap:true));
        var child=await AetherLaunchOptions.ParseAsync(["--runtime","--console-diagnostics"],hasRuntimeBootstrap:true);
        Assert.Equal(AetherLaunchRole.RuntimeChild,child.Role);Assert.True(child.ConsoleDiagnostics);Assert.Null(child.ArtifactPath);
        await Assert.ThrowsAsync<ArgumentException>(()=>AetherLaunchOptions.ParseAsync(["--runtime","--scene=x"],hasRuntimeBootstrap:true));
        await Assert.ThrowsAsync<InvalidDataException>(()=>Task.FromResult(RuntimeBootstrap.ParseEnvironment(new Dictionary<string,string?>())));
    }
    [Fact]
    public async Task CancellationAndWindowBounds_AreCheckedBeforePublication()
    {
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>AetherLaunchOptions.ParseAsync([],token:cancelled.Token));
        var valid=await AetherLaunchOptions.ParseAsync(["--player","--artifact=a","--scene=b","--width=1920","--height=16","--validation","--scene-format=snapshot"]);
        Assert.Equal(1920,valid.Width);Assert.Equal(16,valid.Height);Assert.True(valid.Validation);Assert.True(valid.LegacyScene);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>AetherLaunchOptions.ParseAsync([],AetherLaunchRole.RuntimeChild));
    }
}
