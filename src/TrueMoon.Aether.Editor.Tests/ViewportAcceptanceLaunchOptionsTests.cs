using TrueMoon.ManualTests.AetherEditorHost;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class ViewportAcceptanceLaunchOptionsTests
{
    [Theory] [InlineData("--smoke-viewport")] [InlineData("--smoke-native-input")] [InlineData("--smoke-translation")]
    public void AcceptanceProfiles_RequireExplicitFixtureAndCannotCombineWithOtherProfiles(string profile)
    {
        var options=EditorLaunchOptions.Parse(["--project","fixture.aetherproj","--smoke","fresh",profile,"--validation"]);
        Assert.Equal(profile=="--smoke-viewport",options.AcceptanceSmoke);Assert.Equal(profile=="--smoke-native-input",options.NativeInputSmoke);
        Assert.Equal(profile=="--smoke-translation",options.TranslationSmoke);
        Assert.True(options.Validation);Assert.False(options.NavigationSmoke);
        Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse([profile]));
        Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(["--project","fixture.aetherproj",profile]));
        Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(["--project","Game.csproj","--smoke","fresh",profile]));
        foreach(var other in new[]{"--smoke-camera","--smoke-navigation","--smoke-transform","--smoke-documents","--smoke-hierarchy","--smoke-inspector","--smoke-viewport","--smoke-native-input","--smoke-translation"}.Where(p=>p!=profile))
            Assert.Throws<ArgumentException>(()=>EditorLaunchOptions.Parse(["--project","fixture.aetherproj","--smoke","fresh",profile,other]));
    }
}
