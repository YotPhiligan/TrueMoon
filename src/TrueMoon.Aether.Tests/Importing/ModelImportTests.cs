using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Importing;

public sealed class ModelImportTests
{
    private static CapturedModelSource Source() => new(Guid.Parse("75fc48ba-421c-492e-893a-e3e902d315ef"), "scene.gltf", new[] { new CapturedModelFile("scene.gltf", "{}"u8.ToArray()) });
    private static MeshDefinition Mesh() => new(new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }, new uint[] { 0, 1, 2 },
        new[] { new MeshSlot(RenderContentProfile.DefaultSlotId, 0, 3) }, new(Vector3.Zero, new(1, 1, 0)));
    private static ModelImportCandidate Candidate(CapturedModelSource source) => new(source.SourceId, source.InputRevision, new[] { new ModelImportMesh(Mesh(), 0) },
        new[] { new OpaqueUnlitMaterialDefinition(new(.5f, .25f, 1)) }, new[] { new ModelImportInstance(0, Matrix4x4.CreateTranslation(10, 2, 0)) });
    private static ModelImportWorkerDeployment Deployment() => new(Path.GetTempPath(), Environment.ProcessPath!, new Dictionary<string, string>
    {
        [ModelImportWorkerDeployment.EntryAssembly] = new('A', 64), ["TrueMoon.Aether.Importing.Assimp.deps.json"] = new('B', 64),
        ["TrueMoon.Aether.Importing.Assimp.runtimeconfig.json"] = new('C', 64)
    });

    [Fact]
    public void CandidateRoundTripPreservesGeometryColorTransformAndRevision()
    {
        var source = Source(); var original = Candidate(source); var file = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(file, ModelImportProtocol.Encode(original)); var result = ModelImportProtocol.ReadOutput(file, source);
            Assert.Equal(original.Revision, result.Revision); Assert.Equal(new uint[] { 0, 1, 2 }, result.Meshes[0].Geometry.Indices);
            Assert.Equal(Vector3.UnitY, result.Meshes[0].Geometry.Vertices[2]); Assert.Equal(new Vector3(.5f,.25f,1), result.Materials[0].BaseColorSrgb);
            Assert.Equal(new Vector3(10,2,0), Vector3.Transform(Vector3.Zero, result.Instances[0].World));
            Assert.Equal(0, result.Meshes[0].MaterialIndex);
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData(-1)] [InlineData(1)]
    public void CandidateRejectsInvalidMaterialAndMeshLinks(int index)
    {
        var source = Source(); var material = new[] { new OpaqueUnlitMaterialDefinition(Vector3.One) };
        Assert.Throws<InvalidDataException>(() => new ModelImportCandidate(source.SourceId, source.InputRevision, new[] { new ModelImportMesh(Mesh(), index) }, material, new[] { new ModelImportInstance(0, Matrix4x4.Identity) }));
        Assert.Throws<InvalidDataException>(() => new ModelImportCandidate(source.SourceId, source.InputRevision, new[] { new ModelImportMesh(Mesh(), 0) }, material, new[] { new ModelImportInstance(index, Matrix4x4.Identity) }));
    }

    [Theory]
    [InlineData("identity")] [InlineData("trailing")] [InlineData("length")] [InlineData("version")] [InlineData("truncated")]
    public void CandidatePacketRejectsMalformedOrForeignPayload(string mutation)
    {
        var source = Source(); var bytes = ModelImportProtocol.Encode(Candidate(source)); var file = Path.GetTempFileName();
        try
        {
            if (mutation == "identity") source = new(Guid.NewGuid(), source.RootName, source.Files);
            if (mutation == "trailing") bytes = bytes.Concat(new byte[] { 0 }).ToArray();
            if (mutation == "length") BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 4);
            if (mutation == "version") bytes[0] = 0;
            if (mutation == "truncated") bytes = bytes[..^1];
            File.WriteAllBytes(file, bytes); Assert.Throws<InvalidDataException>(() => ModelImportProtocol.ReadOutput(file, source));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CandidateRejectsSingularTransformAndAggregateQuota()
    {
        var source = Source(); var mesh = new[] { new ModelImportMesh(Mesh(), 0) }; var materials = new[] { new OpaqueUnlitMaterialDefinition(Vector3.One) };
        Assert.Throws<RenderContentException>(() => new ModelImportCandidate(source.SourceId, source.InputRevision, mesh, materials, new[] { new ModelImportInstance(0, default) }));
        var positions = Enumerable.Repeat(Vector3.Zero, 65536).ToArray();
        var large = new MeshDefinition(positions, new uint[] {0,1,2}, new[] {new MeshSlot(RenderContentProfile.DefaultSlotId,0,3)}, new(Vector3.Zero,Vector3.Zero));
        Assert.Throws<InvalidDataException>(() => new ModelImportCandidate(source.SourceId, source.InputRevision, Enumerable.Repeat(new ModelImportMesh(large,0),11), materials, new[] {new ModelImportInstance(0,Matrix4x4.Identity)}));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppFactoryIsLazyCallerOwnedAndClosedAfterStop(bool microsoft)
    {
        await using var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context => context.UseAetherModelImport()).Build();
        await app.StartAsync(); var host = app.Services.Resolve<ModelImportHost>()!;
        var factory = app.Services.Resolve<IFactory<ModelImportOperation,ModelImportArguments>>()!;
        await using var operation = factory.Create(new(Source(), Deployment(), Path.GetTempPath()))!;
        Assert.Equal(0, operation.WorkerProcessId); Assert.Equal(0, host.ActiveOperationCount);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = operation.ImportAsync(cancelled.Token); });
        Assert.Equal(0, operation.WorkerProcessId); Assert.Equal(0, host.ActiveOperationCount);
        await app.StopAsync(); Assert.Throws<ObjectDisposedException>(() => { _ = operation.ImportAsync(); });
    }

    [Fact]
    public async Task AdmissionRejectsSecondOperationUntilFirstReleasesAndIdleDisposalDoesNotReleaseAnother()
    {
        await using var host = new ModelImportHost();
        await using var first = new ModelImportOperation(host, new(Source(), Deployment(), Path.GetTempPath()));
        var idle = new ModelImportOperation(host, new(Source(), Deployment(), Path.GetTempPath()));
        host.Enter(first); Assert.Throws<InvalidOperationException>(() => host.Enter(idle));
        await idle.DisposeAsync(); Assert.Equal(1, host.ActiveOperationCount);
        host.Exit(first); Assert.Equal(0, host.ActiveOperationCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(30001)]
    public void OperationRejectsUnboundedDeadline(int milliseconds) => Assert.Throws<ArgumentOutOfRangeException>(() => new ModelImportOperation(new(), new(Source(), Deployment(), Path.GetTempPath(), TimeSpan.FromMilliseconds(milliseconds))));

    [Fact]
    public void DeploymentCopiesManifestAndRejectsAmbiguousNames()
    {
        var manifest = Deployment().Sha256.ToDictionary(pair => pair.Key, pair => pair.Value);
        var deployment = new ModelImportWorkerDeployment(Path.GetTempPath(), Environment.ProcessPath!, manifest);
        manifest[ModelImportWorkerDeployment.EntryAssembly] = new('D',64);
        Assert.Equal(new string('A',64), deployment.Sha256[ModelImportWorkerDeployment.EntryAssembly]);
        manifest["../escape.dll"] = new('E',64);
        Assert.Throws<NotSupportedException>(() => new ModelImportWorkerDeployment(Path.GetTempPath(), Environment.ProcessPath!, manifest));
    }
}
