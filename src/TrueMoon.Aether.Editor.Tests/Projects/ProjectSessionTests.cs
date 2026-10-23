using TrueMoon.Aether.Editor.Projects;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Projects;

public sealed class ProjectSessionTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    public void BuildRequest_MapsCanonicalProjectAndEntryWithoutOwningBorrowedMetadata(long revision)
    {
        using var fixture = new ProjectFixture();
        var project = fixture.Resolved;
        var sdk = new ProjectToolchainInfo("10.0.402");
        using var session = new ProjectSession(new(project, sdk));
        var request = session.CreateBuildRequest(revision);
        Assert.Equal(Path.Combine(fixture.Root, "Game", "Game.csproj"), request.ProjectPath);
        Assert.Equal("Room.Logic.dll", request.EntryAssembly);
        Assert.Equal("Room.Logic.Game+Nested", request.EntryType);
        Assert.Equal(revision, request.SourceRevision);
        Assert.Null(request.Dependencies);
        Assert.Same(project, session.Project);
        Assert.Same(sdk, session.Toolchain);
        Assert.False(session.IsClosed);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.CreateBuildRequest(-1));
        Assert.False(File.Exists(fixture.Manifest));
    }

    [Fact]
    public void Dispose_ClosesSessionIdempotentlyAndRetainsDiagnosticsWhileRejectingBuild()
    {
        using var fixture = new ProjectFixture();
        var project = fixture.Resolved;
        var sdk = new ProjectToolchainInfo("10.0.402");
        var session = new ProjectSession(new(project, sdk));
        session.Dispose(); session.Dispose();
        Assert.True(session.IsClosed);
        Assert.Throws<ObjectDisposedException>(() => session.CreateBuildRequest(3));
        Assert.Same(project, session.Project);
        Assert.Same(sdk, session.Toolchain);
        Assert.True(File.Exists(project.GameProjectPath));
    }

    [Fact]
    public async Task DefaultToolchain_PreCancelledAndMissingProjectDoNotStartAnSdkProbe()
    {
        using var fixture = new ProjectFixture();
        var toolchain = new DotnetProjectToolchain();
        var missing = Path.Combine(fixture.Root, "Missing.csproj");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toolchain.ValidateAsync(missing, cancellation.Token));
        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => toolchain.ValidateAsync(missing));
        Assert.Equal(missing, error.FileName);
        Assert.False(File.Exists(missing));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.log"));
    }
}
