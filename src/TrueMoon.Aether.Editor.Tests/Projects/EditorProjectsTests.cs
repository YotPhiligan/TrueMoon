using TrueMoon.Aether.Editor.Projects;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Projects;

public sealed class EditorProjectsTests
{
    [Fact]
    public async Task Open_CommitsValidatedFactoryProductThenClosesOnlyPreviousSession()
    {
        using var files = new ProjectFixture();
        var calls = new List<string>();
        var store = new Store(files.Resolved, calls);
        var toolchain = new Toolchain(calls);
        var factory = new Sessions(calls);
        await using var projects = new EditorProjects(store, toolchain, factory);
        var first = await projects.OpenAsync(files.Manifest);
        Assert.Same(first, projects.Active);
        Assert.False(first.IsClosed);
        Assert.Equal(Guid.Parse("82476a9d-dc9a-48cc-a187-0866c31c7021"), first.Project.Description.Id);
        Assert.Equal(new[] { "read", "sdk", "factory" }, calls);
        Assert.Equal(files.Resolved.GameProjectPath, Assert.Single(toolchain.Paths));
        Assert.Equal("10.0.402", first.Toolchain.SdkVersion);
        Assert.Same(store.Project, Assert.Single(factory.Arguments).Project);

        calls.Clear();
        store.Project = files.Resolved with { Description = files.Resolved.Description with { Name = "Second", Id = Guid.NewGuid() } };
        var second = await projects.OpenAsync(files.Manifest);
        Assert.Same(second, projects.Active);
        Assert.NotSame(first, second);
        Assert.True(first.IsClosed);
        Assert.False(second.IsClosed);
        Assert.Equal("Second", second.Project.Description.Name);
        Assert.Equal(new[] { "read", "sdk", "factory" }, calls);
        Assert.Equal(0, store.Disposals);
        Assert.Equal(0, toolchain.Disposals);
        Assert.Equal(0, factory.Disposals);
    }

    [Fact]
    public async Task Create_PublishesManifestBeforeChangingActiveAndOwnsUnpublishedCandidate()
    {
        using var files = new ProjectFixture();
        var calls = new List<string>();
        var store = new Store(files.Resolved, calls);
        var factory = new Sessions(calls);
        await using var projects = new EditorProjects(store, new Toolchain(calls), factory);
        var first = await projects.OpenAsync(files.Manifest);
        calls.Clear();
        var writeEntered = Signal(); var finishWrite = Signal();
        store.Write = async (_, _) => { writeEntered.SetResult(); await finishWrite.Task.WaitAsync(TimeSpan.FromSeconds(10)); };
        var creating = projects.CreateAsync(files.Manifest, files.Description);
        await writeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var candidate = factory.Products[1];
        Assert.Equal(new[] { "resolve", "sdk", "factory", "write" }, calls);
        Assert.Same(first, projects.Active);
        Assert.False(first.IsClosed);
        Assert.False(candidate.IsClosed);
        Assert.False(creating.IsCompleted);
        finishWrite.SetResult();
        Assert.Same(candidate, await creating);
        Assert.Same(candidate, projects.Active);
        Assert.True(first.IsClosed);
        Assert.False(candidate.IsClosed);
        Assert.Null(projects.Error);
        Assert.Equal(files.Description, store.LastDescription);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("resolve")]
    [InlineData("sdk")]
    [InlineData("factory")]
    [InlineData("write")]
    public async Task FailedCandidate_PreservesActiveAndOriginalErrorThenAllowsRetry(string phase)
    {
        using var files = new ProjectFixture();
        var store = new Store(files.Resolved);
        var toolchain = new Toolchain();
        var factory = new Sessions();
        await using var projects = new EditorProjects(store, toolchain, factory);
        var first = await projects.OpenAsync(files.Manifest);
        var failure = new IOException("candidate " + phase);
        switch (phase)
        {
            case "read": store.Read = (_, _) => Task.FromException<ResolvedProject>(failure); break;
            case "resolve": store.Prepare = (_, _) => throw failure; break;
            case "sdk": toolchain.Probe = (_, _) => Task.FromException<ProjectToolchainInfo>(failure); break;
            case "factory": factory.Activate = _ => throw failure; break;
            case "write": store.Write = (_, _) => Task.FromException(failure); break;
        }
        Task<ProjectSession> Operation() => phase is "resolve" or "write"
            ? projects.CreateAsync(files.Manifest, files.Description) : projects.OpenAsync(files.Manifest);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(Operation));
        Assert.Same(failure, projects.Error);
        Assert.Same(first, projects.Active);
        Assert.False(first.IsClosed);
        if (phase == "write") Assert.True(factory.Products[1].IsClosed);
        else Assert.Single(factory.Products);
        store.Prepare = null; store.Write = null; toolchain.Probe = null; factory.Activate = null;
        var retryEntered = Signal(); var finishRetry = Signal();
        store.Read = async (_, _) => { retryEntered.SetResult(); await finishRetry.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.Project; };
        var retrying = projects.OpenAsync(files.Manifest);
        await retryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(projects.Error);
        Assert.Same(first, projects.Active);
        finishRetry.SetResult();
        var retry = await retrying;
        Assert.Same(retry, projects.Active);
        Assert.True(first.IsClosed);
        Assert.False(retry.IsClosed);
        Assert.Null(projects.Error);
    }

    [Fact]
    public async Task Open_NullFactoryResultPreservesActiveAndNeverPublishesNullSession()
    {
        using var files = new ProjectFixture();
        var factory = new Sessions();
        await using var projects = new EditorProjects(new Store(files.Resolved), new Toolchain(), factory);
        var first = await projects.OpenAsync(files.Manifest);
        factory.Activate = _ => null!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => projects.OpenAsync(files.Manifest));
        Assert.Contains("factory returned null", error.Message);
        Assert.Same(error, projects.Error);
        Assert.Same(first, projects.Active);
        Assert.False(first.IsClosed);
        Assert.Single(factory.Products);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("sdk")]
    [InlineData("write")]
    public async Task Cancellation_IgnoredByDependencyStillPreventsLateCandidatePublication(string phase)
    {
        using var files = new ProjectFixture();
        var store = new Store(files.Resolved);
        var toolchain = new Toolchain();
        var factory = new Sessions();
        await using var projects = new EditorProjects(store, toolchain, factory);
        var first = await projects.OpenAsync(files.Manifest);
        var entered = Signal(); var finish = Signal();
        async Task Gate() { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        switch (phase)
        {
            case "read": store.Read = async (_, _) => { await Gate(); return store.Project; }; break;
            case "sdk": toolchain.Probe = async (_, _) => { await Gate(); return new("10.0.402"); }; break;
            case "write": store.Write = async (_, _) => await Gate(); break;
        }
        using var cancellation = new CancellationTokenSource();
        var opening = phase == "write" ? projects.CreateAsync(files.Manifest, files.Description, cancellation.Token)
            : projects.OpenAsync(files.Manifest, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Assert.False(opening.IsCompleted);
        Assert.Same(first, projects.Active);
        finish.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        Assert.IsAssignableFrom<OperationCanceledException>(projects.Error);
        Assert.Same(first, projects.Active);
        Assert.False(first.IsClosed);
        if (phase == "write") Assert.True(factory.Products[1].IsClosed);
        else Assert.Single(factory.Products);
    }

    [Fact]
    public async Task Close_FencesAndDrainsIgnoredCancellationBeforeClosingActiveAndCanReopen()
    {
        using var files = new ProjectFixture();
        var store = new Store(files.Resolved);
        var toolchain = new Toolchain();
        var factory = new Sessions();
        await using var projects = new EditorProjects(store, toolchain, factory);
        var first = await projects.OpenAsync(files.Manifest);
        var entered = Signal(); var finish = Signal();
        CancellationToken pending = default;
        toolchain.Probe = async (_, token) => { pending = token; entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("10.0.402"); };
        var opening = projects.OpenAsync(files.Manifest);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = projects.CloseAsync();
        Assert.Same(closing, projects.CloseAsync());
        Assert.True(pending.IsCancellationRequested);
        Assert.False(closing.IsCompleted);
        Assert.False(first.IsClosed);
        Assert.Same(first, projects.Active);
        Assert.Throws<InvalidOperationException>(() => { _ = projects.OpenAsync(files.Manifest); });
        finish.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await closing;
        Assert.True(first.IsClosed);
        Assert.Null(projects.Active);
        Assert.Null(projects.Error);
        Assert.False(projects.IsClosed);
        Assert.Single(factory.Products);
        toolchain.Probe = null;
        var reopened = await projects.OpenAsync(files.Manifest);
        Assert.False(reopened.IsClosed);
        Assert.Same(reopened, projects.Active);
    }

    [Fact]
    public async Task Dispose_ClosesAdmissionImmediatelyAndDrainsUnpublishedFactoryProduct()
    {
        using var files = new ProjectFixture();
        var store = new Store(files.Resolved);
        var toolchain = new Toolchain();
        var factory = new Sessions();
        var projects = new EditorProjects(store, toolchain, factory);
        var entered = Signal(); var finish = Signal();
        store.Write = async (_, _) => { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); };
        var creating = projects.CreateAsync(files.Manifest, files.Description);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var candidate = Assert.Single(factory.Products);
        var disposing = projects.DisposeAsync().AsTask();
        Assert.True(projects.IsClosed);
        Assert.Null(projects.Active);
        Assert.False(disposing.IsCompleted);
        Assert.Same(disposing, projects.DisposeAsync().AsTask());
        Assert.Throws<ObjectDisposedException>(() => { _ = projects.OpenAsync(files.Manifest); });
        Assert.Throws<ObjectDisposedException>(() => { _ = projects.CreateAsync(files.Manifest, files.Description); });
        Assert.False(candidate.IsClosed);
        finish.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => creating);
        await disposing;
        Assert.True(candidate.IsClosed);
        Assert.Null(projects.Active);
        Assert.Equal((0, 0, 0), (store.Disposals, toolchain.Disposals, factory.Disposals));
        await projects.DisposeAsync(); projects.Dispose();
        Assert.Equal((0, 0, 0), (store.Disposals, toolchain.Disposals, factory.Disposals));
    }

    [Fact]
    public async Task ConcurrentOpenAndCreate_FailFastWithoutStartingOrReplacingWork()
    {
        using var files = new ProjectFixture();
        var store = new Store(files.Resolved);
        var factory = new Sessions();
        await using var projects = new EditorProjects(store, new Toolchain(), factory);
        var entered = Signal(); var finish = Signal();
        store.Read = async (_, _) => { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.Project; };
        var opening = projects.OpenAsync(files.Manifest);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => { _ = projects.OpenAsync(files.Manifest); });
        Assert.Throws<InvalidOperationException>(() => { _ = projects.CreateAsync(files.Manifest, files.Description); });
        Assert.Equal(1, store.ReadCalls);
        Assert.Equal(0, store.ResolveCalls);
        Assert.Empty(factory.Products);
        finish.SetResult();
        Assert.Same(await opening, projects.Active);
        Assert.Single(factory.Products);
    }

    [Fact]
    public async Task Close_ObservesCancellationCallbackFailureAndStillDrainsOwnedWork()
    {
        using var files = new ProjectFixture();
        var store = new Store(files.Resolved);
        await using var projects = new EditorProjects(store, new Toolchain(), new Sessions());
        var first = await projects.OpenAsync(files.Manifest);
        var entered = Signal(); var finish = Signal();
        var expected = new IOException("cancellation callback failed");
        store.Read = async (_, token) =>
        {
            using var registration = token.Register(() => throw expected);
            entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.Project;
        };
        var opening = projects.OpenAsync(files.Manifest);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = projects.CloseAsync();
        Assert.False(closing.IsCompleted);
        Assert.False(first.IsClosed);
        finish.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        var error = await Assert.ThrowsAsync<AggregateException>(() => closing);
        Assert.Same(expected, Assert.Single(error.Flatten().InnerExceptions));
        Assert.Same(error, projects.Error);
        Assert.True(first.IsClosed);
        Assert.Null(projects.Active);
        await projects.CloseAsync();
        Assert.Null(projects.Error);
    }

    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed class Store(ResolvedProject project, List<string>? calls = null) : IProjectStore, IDisposable
    {
        public ResolvedProject Project = project;
        public Func<string, CancellationToken, Task<ResolvedProject>>? Read;
        public Func<string, ProjectDescription, ResolvedProject>? Prepare;
        public Func<ResolvedProject, CancellationToken, Task>? Write;
        public ProjectDescription? LastDescription;
        public int ReadCalls, ResolveCalls, Disposals;
        public Task<ResolvedProject> ReadAsync(string path, CancellationToken token = default)
        { ReadCalls++; calls?.Add("read"); return Read?.Invoke(path, token) ?? Task.FromResult(Project); }
        public ResolvedProject Resolve(string path, ProjectDescription description)
        { ResolveCalls++; calls?.Add("resolve"); LastDescription = description; return Prepare?.Invoke(path, description) ?? Project; }
        public Task WriteNewAsync(ResolvedProject value, CancellationToken token = default)
        { calls?.Add("write"); return Write?.Invoke(value, token) ?? Task.CompletedTask; }
        public void Dispose() => Disposals++;
    }

    internal sealed class Toolchain(List<string>? calls = null) : IProjectToolchain, IDisposable
    {
        public readonly List<string> Paths = [];
        public Func<string, CancellationToken, Task<ProjectToolchainInfo>>? Probe;
        public int Disposals;
        public Task<ProjectToolchainInfo> ValidateAsync(string path, CancellationToken token = default)
        { Paths.Add(path); calls?.Add("sdk"); return Probe?.Invoke(path, token) ?? Task.FromResult(new ProjectToolchainInfo("10.0.402")); }
        public void Dispose() => Disposals++;
    }

    internal sealed class Sessions(List<string>? calls = null) : IFactory<ProjectSession, ProjectSessionArguments>, IDisposable
    {
        public readonly List<ProjectSessionArguments> Arguments = [];
        public readonly List<ProjectSession> Products = [];
        public Func<ProjectSessionArguments, ProjectSession>? Activate;
        public int Disposals;
        public ProjectSession Create(ProjectSessionArguments arguments)
        {
            calls?.Add("factory"); Arguments.Add(arguments);
            var product = Activate == null ? new ProjectSession(arguments) : Activate(arguments);
            if (product != null) Products.Add(product);
            return product!;
        }
        public void Dispose() => Disposals++;
    }
}
