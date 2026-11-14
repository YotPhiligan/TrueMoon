using System.Numerics;
using System.Text.Json;
using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Abstractions.Preview;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using A = TrueMoon.Aether.Abstractions;
using Xunit;

namespace TrueMoon.Aether.Abstractions.Tests;

public sealed class NeutralValuesTests
{
    [Fact]
    public void SceneAndComponentInputs_AreCopied_WithPersistentIdsAndUnknownValuesPreserved()
    {
        var entity = new A.Scenes.SceneEntity { Id = "one", X = 2, Interactive = true };
        var snapshot = new A.Scenes.SceneSnapshot("scene", 3, [entity]); entity.X = 9;
        var copy = snapshot.CopyEntities(); Assert.Equal(2, copy[0].X); copy[0].X = 19; Assert.Equal(2, snapshot.CopyEntities()[0].X);
        Assert.Equal("[{\"Id\":\"one\",\"X\":2,\"Y\":0,\"Z\":0,\"Interactive\":true,\"Primitive\":null}]", JsonSerializer.Serialize(snapshot.CopyEntities()));
        Assert.Throws<ArgumentException>(() => new A.Scenes.SceneSnapshot("scene", 0, [new() { Id = "a" }, new() { Id = "a" }]));
        Assert.Throws<ArgumentException>(() => new A.Scenes.SceneSnapshot("scene", 0, [new() { Id = "a", X = float.NaN }]));
        Assert.Equal(64, new A.Scenes.SceneSnapshot("scene", 0, Enumerable.Range(0, 64).Select(i => new A.Scenes.SceneEntity { Id = i.ToString() })).CopyEntities().Count);
        Assert.Throws<ArgumentException>(() => new A.Scenes.SceneSnapshot("scene", 0, Enumerable.Range(0, 65).Select(i => new A.Scenes.SceneEntity { Id = i.ToString() })));
        var values = new Dictionary<string, A.Scenes.PropertyValue> { { "future.property", A.Scenes.PropertyValue.From("opaque") } };
        var type = new A.Scenes.ComponentTypeId(Guid.NewGuid()); var id = new A.Scenes.ComponentId(Guid.NewGuid());
        var component = new A.Scenes.ComponentData(id, type, 7, values); values.Clear();
        Assert.Equal(id, component.Id); Assert.Equal(type, component.TypeId); Assert.Equal(7, component.SchemaVersion);
        Assert.Equal(A.Scenes.PropertyValue.From("opaque"), component.Properties["future.property"]);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1001"), A.Scenes.BuiltInComponents.TransformId.Value);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1002"), A.Scenes.BuiltInComponents.AppearanceId.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new A.Scenes.ComponentData(id, type, 0));
    }
    [Fact]
    public void CanonicalMaterialAndMeshBytes_MatchFixedV1Fixtures_WithoutRuntimeCodecDependency()
    {
        var material = new A.Rendering.OpaqueUnlitMaterialDefinition(new(.5f, .25f, 1));
        Assert.Equal(Convert.FromHexString("544D524101000200010000006FC9FCE0954742F5B4DFF05F306F1301010000000000003F0000803E0000803F"), A.Rendering.RenderContentCodec.Encode(material));
        var vertices = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }; var indices = new uint[] { 0, 1, 2 };
        var slot = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"); var mesh = new A.Rendering.MeshDefinition(vertices, indices, [new(slot, 0, 3)], new(Vector3.Zero, Vector3.One));
        var bytes = A.Rendering.RenderContentCodec.Encode(mesh);
        Assert.Equal(132, bytes.Length);
        Assert.Equal("544D52410100010001000000", Convert.ToHexString(bytes.AsSpan(0, 12)));
        Assert.Equal(Convert.FromHexString("00112233445566778899AABBCCDDEEFF0000000003000000"), bytes[108..]);
        vertices[0] = new(99); indices[0] = 2; Assert.Equal(Vector3.Zero, mesh.Vertices[0]); Assert.Equal(0U, mesh.Indices[0]);
        var decoded = Assert.IsType<A.Rendering.MeshDefinition>(A.Rendering.RenderContentCodec.Decode(bytes));
        Assert.Equal(mesh.Revision, decoded.Revision); Assert.Equal(mesh.Vertices.ToArray(), decoded.Vertices.ToArray()); Assert.Equal(mesh.Indices.ToArray(), decoded.Indices.ToArray());
        Assert.Throws<A.Rendering.RenderContentException>(() => new A.Rendering.OpaqueUnlitMaterialDefinition(new(2, 0, 0)));
        Assert.Throws<A.Rendering.RenderContentException>(() => new A.Rendering.OpaqueUnlitMaterialDefinition(Vector3.One, A.Rendering.RenderContentProfile.UnlitFamilyId, schemaVersion: 2));
        Assert.Throws<A.Rendering.RenderContentException>(() => A.Rendering.RenderContentCodec.Decode(bytes.AsSpan(0, bytes.Length - 1)));
        var key = material.CreateKey(Guid.NewGuid(), Guid.NewGuid()); key.Validate(); Assert.Equal(material.Revision, key.Revision);
        Assert.Throws<A.Rendering.RenderContentException>(() => default(A.Rendering.RenderAssetKey).Validate());
    }
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(16, 16, true)]
    [InlineData(1920, 1080, true)]
    [InlineData(0, 16, false)]
    [InlineData(15, 16, false)]
    [InlineData(1921, 1080, false)]
    [InlineData(1920, 1081, false)]
    [InlineData(-1, 0, false)]
    public void NeutralViewportExtent_HasTheExistingExactAdmission(int width, int height, bool valid)
    {
        if (valid) A.Rendering.RenderViewport.ValidateExtent(width, height);
        else Assert.Throws<ArgumentOutOfRangeException>(() => A.Rendering.RenderViewport.ValidateExtent(width, height));
        Assert.Equal(1920, A.Rendering.RenderViewport.MaximumWidth); Assert.Equal(1080, A.Rendering.RenderViewport.MaximumHeight);
    }
    [Fact]
    public void TransformInputAndCheckpointValues_PreserveValidationAndPortableIdentity()
    {
        var local = A.Ecs.Transforms.LocalTransform.Identity; Assert.Equal(Matrix4x4.Identity, local.CreateMatrix());
        Assert.Throws<InvalidDataException>(() => default(A.Ecs.Transforms.LocalTransform).Validate());
        Assert.Throws<InvalidDataException>(() => new A.Ecs.Motion.LinearVelocity(new(float.PositiveInfinity, 0, 0)).Validate());
        var state = new A.Input.GameInputState(true, A.Input.GameButtons.Forward, Yaw: .1f, Pitch: -.1f, InteractPressed: true); state.Validate();
        Assert.Equal("{\"Captured\":true,\"Buttons\":1,\"Yaw\":0.1,\"Pitch\":-0.1,\"InteractPressed\":true}", JsonSerializer.Serialize(state));
        Assert.Throws<InvalidDataException>(() => new A.Input.GameInputState(true, Yaw: float.NaN).Validate());
        var objects = new A.Checkpoints.GameObjectStates([new("z", true), new("a", false)]);
        Assert.Equal(new[] { "a", "z" }, objects.Entries.Select(value => value.EntityId));
        Assert.Throws<InvalidDataException>(() => new A.Checkpoints.GameObjectStates([new("a", true), new("a", false)]));
        var hash = new string('A', 64); var checkpoint = new A.Checkpoints.GameCheckpoint(hash, hash, "scene", 3, new(17, default, true) { Objects = objects });
        checkpoint.ValidateFor(hash, new("scene", 3, [])); Assert.Equal(17UL, checkpoint.State.Tick);
        Assert.Throws<InvalidDataException>(() => checkpoint.ValidateFor(hash, new("scene", 4, [])));
        Assert.Throws<InvalidDataException>(() => new A.Checkpoints.CameraTransform(float.NaN, 0, 0, 0, 0, 0).Validate());
    }

    [Fact]
    public void PreviewValues_RoundtripFixedV1Envelope_WithoutEditorOrNativeServices()
    {
        var epoch = Guid.NewGuid(); var view = Guid.NewGuid(); var content = Guid.NewGuid(); var scene = Guid.NewGuid();
        var state = new A.Preview.PreviewViewState(new(epoch, view, content, scene, 3), A.Preview.PreviewCamera.Default);
        var bytes = A.Preview.PreviewViewCodec.Encode(state);
        Assert.Equal(Convert.FromHexString("544D5056494557310100000000010000"), bytes[..16]); Assert.Equal(256, bytes.Length);
        Assert.Equal(epoch.ToString("N"), System.Text.Encoding.ASCII.GetString(bytes, 20, 32));
        Assert.Equal(3, System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(148)));
        var decoded = A.Preview.PreviewViewCodec.Decode(bytes); Assert.Equal(state.Stamp, decoded.Stamp); Assert.Equal(state.Camera, decoded.Camera);
        Assert.Throws<InvalidDataException>(() => A.Preview.PreviewViewCodec.Decode(bytes.AsSpan(1)));
        Assert.Throws<InvalidDataException>(() => (state.Stamp with { Version = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (state.Stamp with { CameraRevision = 0 }).Validate());
        Assert.Equal("TrueMoon.Aether.Abstractions", typeof(A.Sessions.IGameSession).Assembly.GetName().Name);
    }
}
