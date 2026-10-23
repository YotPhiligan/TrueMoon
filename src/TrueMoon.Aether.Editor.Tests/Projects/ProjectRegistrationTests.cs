using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Projects;

public sealed class ProjectRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task App_DefaultRegistrationSharesCoordinatorButFactoryProductsHaveIndependentConsumerOwnership(bool di)
    {
        using var files = new ProjectFixture();
        var app = Builder(di).Setup(context => Assert.Same(context, context.UseAetherEditorProjects())).Build();
        try
        {
            var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>());
            Assert.Same(projects, app.Services.Resolve<EditorProjects>());
            Assert.IsType<JsonProjectStore>(app.Services.Resolve<IProjectStore>());
            Assert.IsType<DotnetProjectToolchain>(app.Services.Resolve<IProjectToolchain>());
            var factory = Assert.IsAssignableFrom<IFactory<ProjectSession, ProjectSessionArguments>>(
                app.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>());
            Assert.Same(factory, app.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>());
            using var first = factory.Create(new(files.Resolved, new("10.0.402")));
            using var second = factory.Create(new(files.Resolved, new("10.0.402")));
            Assert.NotSame(first, second);
            Assert.Null(projects.Active);
            Assert.False(projects.IsClosed);
            Assert.False(File.Exists(files.Manifest));
            var start = app.Services.Resolve<IStartable>();
            var stop = app.Services.Resolve<IStoppable>();
            Assert.Same(start, stop);
            Assert.False(start is IDisposable);
            Assert.False(start is IAsyncDisposable);
            await app.DisposeAsync();
            Assert.True(projects.IsClosed);
            Assert.False(first.IsClosed);
            Assert.False(second.IsClosed);
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task App_PreservesInjectedStoreToolchainAndFactoryAndDisposesEachDependencyOnce(bool di)
    {
        using var files = new ProjectFixture();
        var store = new EditorProjectsTests.Store(files.Resolved);
        var toolchain = new EditorProjectsTests.Toolchain();
        var factory = new EditorProjectsTests.Sessions();
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services
                .Singleton<IProjectStore>(_ => store)
                .Singleton<IProjectToolchain>(_ => toolchain)
                .Singleton<IFactory<ProjectSession, ProjectSessionArguments>>(_ => factory));
            context.UseAetherEditorProjects();
        }).Build();
        try
        {
            Assert.Same(store, app.Services.Resolve<IProjectStore>());
            Assert.Same(toolchain, app.Services.Resolve<IProjectToolchain>());
            Assert.Same(factory, app.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>());
            await app.StartAsync();
            var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>());
            var active = await projects.OpenAsync(files.Manifest);
            Assert.Same(active, Assert.Single(factory.Products));
            await app.StopAsync();
            Assert.True(active.IsClosed);
            Assert.True(projects.IsClosed);
            Assert.Null(projects.Active);
            Assert.Equal((0, 0, 0), (store.Disposals, toolchain.Disposals, factory.Disposals));
            await app.DisposeAsync();
            Assert.Equal((1, 1, 1), (store.Disposals, toolchain.Disposals, factory.Disposals));
            await app.DisposeAsync();
            Assert.Equal((1, 1, 1), (store.Disposals, toolchain.Disposals, factory.Disposals));
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task App_StopClosesAdmissionAndDrainsPendingOpenBeforeDisposingBorrowedDependencies(bool di)
    {
        using var files = new ProjectFixture();
        var store = new EditorProjectsTests.Store(files.Resolved);
        var toolchain = new EditorProjectsTests.Toolchain();
        var factory = new EditorProjectsTests.Sessions();
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => store)
                .Singleton<IProjectToolchain>(_ => toolchain)
                .Singleton<IFactory<ProjectSession, ProjectSessionArguments>>(_ => factory));
            context.UseAetherEditorProjects();
        }).Build();
        await app.StartAsync();
        var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>());
        var first = await projects.OpenAsync(files.Manifest);
        var entered = EditorProjectsTests.Signal(); var finish = EditorProjectsTests.Signal(); var cancelled = EditorProjectsTests.Signal();
        store.Read = async (_, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.Project;
        };
        var opening = projects.OpenAsync(files.Manifest);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = app.StopAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(projects.IsClosed);
        Assert.False(stopping.IsCompleted);
        Assert.False(first.IsClosed);
        Assert.Equal((0, 0, 0), (store.Disposals, toolchain.Disposals, factory.Disposals));
        Assert.Throws<ObjectDisposedException>(() => { _ = projects.OpenAsync(files.Manifest); });
        finish.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await stopping;
        Assert.True(first.IsClosed);
        Assert.Null(projects.Active);
        Assert.Single(factory.Products);
        await app.DisposeAsync();
        Assert.Equal((1, 1, 1), (store.Disposals, toolchain.Disposals, factory.Disposals));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void App_RejectsDuplicateAttachmentOrExistingCoordinatorBeforeDependencyActivation(bool di, bool existing)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => { activations++; throw new IOException("must stay lazy"); }));
            if (existing) context.Services(services => services.Singleton<EditorProjects>(_ => throw new IOException("must stay lazy")));
            context.UseAetherEditorProjects();
            if (!existing) context.UseAetherEditorProjects();
        });
        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("Editor project services are already registered.", error.Message);
        Assert.Equal(0, activations);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
}
