using System.Text.Json;
using TrueMoon;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.ManualTests.EneritDocumentationConsumer;

internal static class Program
{
    private static async Task Main()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var endpoint = "enerit-docs-" + Guid.NewGuid().ToString("N");
        await using var server = App.Build(context =>
        {
            context.UseDiagnostics();
            context.PipesServiceTransport(_ => endpoint);
            context.Services(services => services.Singleton<IBackend>(_ => new BackendService()));
            context.ListenInvocationService<IBackend, BackendService>();
        });
        await server.StartAsync(timeout.Token);
        await using var client = App.Build(context =>
        {
            context.UseDiagnostics();
            context.PipesServiceTransport(_ => endpoint);
            context.UseInvocationService<IBackend>();
        });
        await client.StartAsync(timeout.Token);
        // Composition root captures the resolved services once, outside invocation execution.
        var backend = (IBackend)client.Services.GetService(typeof(IBackend))!;
        var transport = (PipesInvocationClient<IBackend>)client.Services.GetService(typeof(IInvocationClient<IBackend>))!;
        Check(backend.GetType().Name == "BackendGeneratedImplementation", "Generated client registration");
        Check((await backend.HelloAsync(timeout.Token)).Id == 73, "Identity DTO");
        foreach (var name in new[] { "", "Project", "Луна 🌙" })
            Check((await backend.OpenProjectAsync(name, timeout.Token)).Name == "opened:" + name, "Project reply DTO");
        await client.StopAsync(timeout.Token);
        Check(transport.PendingRequestCount == 0, "Pending requests after stop");
        Check(transport.Completion.IsCompletedSuccessfully, "Client workers after stop");
        await server.StopAsync(timeout.Token);
        Console.WriteLine("RESULT " + JsonSerializer.Serialize(new { Completed = true, Calls = 4, Pending = transport.PendingRequestCount,
            WorkersCompleted = transport.Completion.IsCompletedSuccessfully }));
    }

    private static void Check(bool value, string requirement)
    {
        if (!value) throw new InvalidOperationException(requirement);
    }
}

/// <summary>Minimal Studio-shaped backend contract.</summary>
public interface IBackend
{
    /// <summary>Reads the backend identity.</summary>
    Task<BackendIdentity> HelloAsync(CancellationToken cancellationToken = default);
    /// <summary>Opens the named project.</summary>
    Task<BackendProjectReply> OpenProjectAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>Mutable backend identity payload.</summary>
public sealed class BackendIdentity
{
    /// <summary>Backend identity number.</summary>
    public int Id { get; set; }
}

/// <summary>Mutable project result payload.</summary>
public sealed class BackendProjectReply
{
    /// <summary>Opened project name.</summary>
    public string? Name { get; set; }
}

internal sealed class BackendService : IBackend
{
    public Task<BackendIdentity> HelloAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BackendIdentity { Id = 73 });
    public Task<BackendProjectReply> OpenProjectAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(new BackendProjectReply { Name = "opened:" + name });
}
