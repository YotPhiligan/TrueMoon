using TrueMoon.ManualTests.AetherEditorHost;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class CameraLaunchOptionsTests
{
    [Fact]
    public void NavigationSmoke_RequiresDisposableProjectAndExclusiveProfile()
    {
        var options=EditorLaunchOptions.Parse(["--project","room.aetherproj","--smoke","out","--smoke-navigation","--validation"]);
        Assert.True(options.NavigationSmoke);Assert.False(options.CameraSmoke);Assert.True(options.Validation);
        Assert.False(EditorLaunchOptions.Parse([]).NavigationSmoke);
        Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(["--smoke-navigation"]));
        Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(["--project","game.csproj","--smoke","out","--smoke-navigation"]));
        foreach(var other in new[]{"--smoke-camera","--smoke-documents","--smoke-transform","--smoke-hierarchy","--smoke-inspector"})
            Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(["--project","room.aetherproj","--smoke","out","--smoke-navigation",other]));
    }
    [Fact]
    public void CameraSmoke_RequiresExplicitProjectAndOutputWithoutOtherProfiles()
    {
        var options=EditorLaunchOptions.Parse(["--project","room.aetherproj","--smoke","camera-output","--smoke-camera","--validation"]);
        Assert.True(options.CameraSmoke); Assert.True(options.Validation); Assert.False(options.TransformSmoke); Assert.False(options.DocumentSmoke);
        Assert.Equal(Path.GetFullPath("room.aetherproj"),options.ProjectPath); Assert.Equal(Path.GetFullPath("camera-output"),options.SmokeDirectory);
        Assert.False(EditorLaunchOptions.Parse([]).CameraSmoke);
    }
    [Theory]
    [InlineData("--smoke-camera")]
    [InlineData("--project","room.aetherproj","--smoke-camera")]
    [InlineData("--project","game.csproj","--smoke","out","--smoke-camera")]
    [InlineData("--project","room.aetherproj","--smoke","out","--smoke-camera","--smoke-transform")]
    [InlineData("--project","room.aetherproj","--smoke","out","--smoke-camera","--smoke-documents")]
    [InlineData("--project","room.aetherproj","--smoke","out","--smoke-camera","--smoke-hierarchy")]
    [InlineData("--project","room.aetherproj","--smoke","out","--smoke-camera","--smoke-inspector")]
    public void CameraSmoke_RejectsIncompleteOrMixedInputs(params string[] arguments)
        => Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(arguments));
}
