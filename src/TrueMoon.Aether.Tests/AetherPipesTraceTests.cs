using System.Buffers;
using System.Diagnostics;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class AetherPipesTraceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppGenericEngineControlFactories_PropagateTraceWithoutActivatingEngineOrRuntime(bool microsoftDI)
    {
        const string hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var endpoint = "engine-trace-" + Guid.NewGuid().ToString("N"); var activations = 0;
        var launch = new SessionLaunch(new(Guid.NewGuid(), 1, hash, BuildIdentity.PreviewId, new('B', 64)), endpoint,
            "unused-aether-trace-report", Role: RuntimeRole.EditPreview);
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            context.UseDiagnostics();
            context.Services(services =>
            {
                services.Singleton<IFactory<AetherHost>>(_ => new Factory<AetherHost>(() => { activations++; throw new IOException("unexpected host"); }));
                services.Singleton<IFactory<AetherRuntimeHost, SessionLaunch>>(_ => new Factory<AetherRuntimeHost, SessionLaunch>(_ => { activations++; throw new IOException("unexpected runtime"); }));
                services.Singleton<IFactory<FrameProducer>>(_ => new Factory<FrameProducer>(() => { activations++; throw new IOException("unexpected frames"); }));
                services.Singleton<IFactory<GpuResourceHost>>(_ => new Factory<GpuResourceHost>(() => { activations++; throw new IOException("unexpected GPU"); }));
            });
            context.UseAether(options =>
            {
                options.HostContractId = options.GameBuildId = hash; options.ReportDirectory = "unused-aether-trace-report";
                options.RuntimeFactory = _ => { activations++; throw new IOException("unexpected child factory"); };
            });
            context.UseAetherPreviewRuntime(launch, int.MaxValue);
        }).Build();
        var telemetry = Assert.IsType<DiagnosticTelemetry>(app.Services.Resolve<IDiagnosticTelemetry>());
        using var observer = new EngineTelemetryObserver(telemetry); var handler = new CapturingHandler();
        try
        {
            var servers = app.Services.Resolve<IFactory<PipesInvocationServer<IEngineControl>, PipesInvocationServerArguments<IEngineControl>>>()!;
            var clients = app.Services.Resolve<IFactory<PipesInvocationClient<IEngineControl>, string>>()!;
            await using (var server = servers.Create(new(endpoint, handler)))
            await using (var client = clients.Create(endpoint))
            {
                using var parent = new Activity("AetherCaller").SetIdFormat(ActivityIdFormat.W3C).Start();
                Assert.Equal(19, await client.InvokeAsync(19, null, bytes => bytes.Span[0]).WaitAsync(TimeSpan.FromSeconds(10)));
                await TestWait.UntilAsync(() => observer.Stopped.Count == 2 && observer.Measurements.Count == 4, TimeSpan.FromSeconds(10));
                var outgoing = Assert.Single(observer.Stopped, activity => activity.Kind == ActivityKind.Client);
                var incoming = Assert.Single(observer.Stopped, activity => activity.Kind == ActivityKind.Server);
                Assert.Equal(parent.TraceId, outgoing.TraceId); Assert.Equal(parent.SpanId, outgoing.ParentSpanId);
                Assert.Equal(outgoing.TraceId, incoming.TraceId); Assert.Equal(outgoing.SpanId, incoming.ParentSpanId); Assert.True(incoming.HasRemoteParent);
                Assert.Same(incoming, handler.Activity); Assert.True(handler.CurrentSurvivedAwait); Assert.Same(parent, Activity.Current);
                Assert.Equal(typeof(IEngineControl).FullName, outgoing.GetTagItem("Service")); Assert.Equal(typeof(IEngineControl).FullName, incoming.GetTagItem("Service"));
                Assert.Equal(outgoing.GetTagItem("RequestId"), incoming.GetTagItem("RequestId")); Assert.Equal(0, activations);
            }
            Assert.False(telemetry.Read().Closed); Assert.Equal(0, telemetry.Read().ActiveOperations); Assert.Equal(0, activations);
        }
        finally { await app.DisposeAsync(); }
        Assert.True(telemetry.Read().Closed); Assert.True(telemetry.Read().Released); Assert.Equal(0, activations);
    }

    private sealed class CapturingHandler : IInvocationServerHandler<IEngineControl>
    {
        internal Activity? Activity; internal bool CurrentSurvivedAwait;
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        {
            Activity = System.Diagnostics.Activity.Current; await Task.Yield(); CurrentSurvivedAwait = ReferenceEquals(Activity, System.Diagnostics.Activity.Current);
            writer.Write(new[] { method }); return (true, null);
        }
    }
}
