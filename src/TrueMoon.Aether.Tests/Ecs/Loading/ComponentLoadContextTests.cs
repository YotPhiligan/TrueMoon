using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class ComponentLoadContextTests
{
    [Fact]
    public void ScalarHelpers_ReadDefaultsExactKindsAndExplicitNullWithProvenance()
    {
        var schema = Scalars();
        var registry = new ComponentRegistryBuilder().Register(schema, context =>
        {
            Assert.Equal(LoadFixture.SceneId, context.Scene.Id);
            Assert.Equal(LoadFixture.First, context.SourceEntity.Id);
            Assert.Equal(LoadFixture.Component, context.Component.Id);
            Assert.Same(schema, context.Schema);
            Assert.Equal(CancellationToken.None, context.CancellationToken);
            Assert.Equal(42L, context.GetInteger("integer"));
            Assert.Equal(3.5d, context.GetNumber("number"));
            Assert.Equal(PropertyValueKind.Null, context.GetValue("nullable").Kind);
            Assert.True(context.Component.Properties["nullable"].IsSchemaDefault);
            Assert.False(context.Component.Properties["text"].IsSchemaDefault);
            Assert.Null(context.Parent);
            return new ScalarsValue(context.GetBoolean("flag"), context.GetInt32("integer"),
                context.GetSingle("number"), context.GetString("text").Length);
        }).Build();
        using var result = Loader(registry).Load(LoadFixture.Scene(schema, [new("text", PropertyValue.From("override"))]));
        Assert.Equal(new ScalarsValue(true, 42, 3.5f, 8), result.World.GetComponent<ScalarsValue>(result.Entities[LoadFixture.First]));
        Assert.True(result.IsReady);
    }

    [Theory]
    [InlineData("boolean")]
    [InlineData("integer")]
    [InlineData("number")]
    [InlineData("string")]
    [InlineData("null-string")]
    [InlineData("int32")]
    [InlineData("single")]
    [InlineData("unknown")]
    [InlineData("null-key")]
    public void ScalarHelpers_RejectWrongKindsAndMissingValuesWithStableLocation(string kind)
    {
        var schema = Scalars(); var factories = 0;
        var expectedProperty = kind switch
        {
            "boolean" or "number" or "single" => "integer",
            "integer" or "int32" => "number",
            "string" => "flag",
            "null-string" => "nullable",
            "unknown" => "missing",
            _ => null
        };
        var registry = new ComponentRegistryBuilder().Register(schema, _ => new LoadFixture.Value(0), validate: context =>
        {
            switch (kind)
            {
                case "boolean": context.GetBoolean("integer"); break;
                case "integer": context.GetInteger("number"); break;
                case "number": context.GetNumber("integer"); break;
                case "string": context.GetString("flag"); break;
                case "null-string": context.GetString("nullable"); break;
                case "int32": context.GetInt32("number"); break;
                case "single": context.GetSingle("integer"); break;
                case "unknown": context.GetValue("missing"); break;
                case "null-key": context.GetValue(null!); break;
            }
        }).Build();
        var loader = Loader(registry, new Factory<World>(() => { factories++; return new(); }));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(schema)));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal((LoadFixture.SceneId, LoadFixture.First, LoadFixture.Component, expectedProperty),
            (error.SceneId, error.EntityId, error.ComponentId, error.PropertyId));
        Assert.Equal(0, factories);
        Assert.Null(error.CleanupOwner);
    }

    [Theory]
    [InlineData(-2147483648L, int.MinValue, int.MaxValue, true, int.MinValue)]
    [InlineData(2147483647L, int.MinValue, int.MaxValue, true, int.MaxValue)]
    [InlineData(-2147483649L, int.MinValue, int.MaxValue, false, 0)]
    [InlineData(2147483648L, int.MinValue, int.MaxValue, false, 0)]
    [InlineData(2L, 2, 4, true, 2)]
    [InlineData(4L, 2, 4, true, 4)]
    [InlineData(1L, 2, 4, false, 0)]
    [InlineData(5L, 2, 4, false, 0)]
    [InlineData(3L, 4, 2, false, 0)]
    public void GetInt32_EnforcesInclusiveBoundsWithoutTruncation(long value, int minimum, int maximum, bool valid, int expected)
    {
        var schema = LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(value)));
        var registry = new ComponentRegistryBuilder().Register(schema,
            context => new LoadFixture.Value(context.GetInt32("value", minimum, maximum))).Build();
        if (valid)
        {
            using var result = Loader(registry).Load(LoadFixture.Scene(schema));
            Assert.Equal(new LoadFixture.Value(expected), result.World.GetComponent<LoadFixture.Value>(result.Entities[LoadFixture.First]));
        }
        else
        {
            var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry).Load(LoadFixture.Scene(schema)));
            Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
            Assert.Equal("value", error.PropertyId);
            Assert.Equal(LoadFixture.First, error.EntityId);
        }
    }

    [Theory]
    [InlineData(2d, 2f, 4f)]
    [InlineData(4d, 2f, 4f)]
    [InlineData(-3.5d, -4f, 0f)]
    [InlineData((double)float.MaxValue, float.NegativeInfinity, float.PositiveInfinity)]
    [InlineData(-(double)float.MaxValue, float.NegativeInfinity, float.PositiveInfinity)]
    public void GetSingle_AdmitsInclusiveFiniteBounds(double value, float minimum, float maximum)
    {
        var schema = LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(value)));
        var registry = new ComponentRegistryBuilder().Register(schema,
            context => new FloatValue(context.GetSingle("value", minimum, maximum))).Build();
        using var result = Loader(registry).Load(LoadFixture.Scene(schema));
        Assert.Equal(new FloatValue((float)value), result.World.GetComponent<FloatValue>(result.Entities[LoadFixture.First]));
        Assert.True(result.IsReady);
    }

    [Theory]
    [InlineData("below")]
    [InlineData("above")]
    [InlineData("rounds-down")]
    [InlineData("rounds-up")]
    [InlineData("overflow-positive")]
    [InlineData("overflow-negative")]
    [InlineData("above-float-max-rounding")]
    [InlineData("nan-min")]
    [InlineData("nan-max")]
    [InlineData("inverted")]
    public void GetSingle_RejectsOriginalDoubleOutsideRangeAndInvalidBounds(string kind)
    {
        var value = kind switch
        {
            "below" => 1.5d, "above" => 4.5d,
            "rounds-down" => 4d + 1e-10, "rounds-up" => 2d - 1e-10,
            "overflow-positive" => double.MaxValue, "overflow-negative" => -double.MaxValue,
            "above-float-max-rounding" => Math.BitIncrement((double)float.MaxValue), _ => 3d
        };
        var schema = LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(value)));
        var minimum = kind == "nan-min" ? float.NaN : kind == "inverted" ? 4f : 2f;
        var maximum = kind == "nan-max" ? float.NaN : kind == "inverted" ? 2f : 4f;
        if (kind.StartsWith("overflow", StringComparison.Ordinal) || kind == "above-float-max-rounding")
        { minimum = float.NegativeInfinity; maximum = float.PositiveInfinity; }
        var registry = new ComponentRegistryBuilder().Register(schema,
            context => new FloatValue(context.GetSingle("value", minimum, maximum))).Build();
        var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry).Load(LoadFixture.Scene(schema)));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal((LoadFixture.SceneId, LoadFixture.First, LoadFixture.Component, "value"),
            (error.SceneId, error.EntityId, error.ComponentId, error.PropertyId));
    }

    [Fact]
    public void CallbackContexts_RemainInactiveDuringLaterCallbacksAndAfterLoad()
    {
        var schema = LoadFixture.IntegerSchema;
        var validations = new List<ComponentValidationContext>();
        var imports = new List<ComponentLoadContext>();
        var registry = new ComponentRegistryBuilder().Register(schema, context =>
        {
            foreach (var previous in validations) AssertInactive(previous);
            foreach (var previous in imports) AssertInactive(previous);
            imports.Add(context);
            return new LoadFixture.Value(context.GetInt32("value"));
        }, validate: context =>
        {
            foreach (var previous in validations) AssertInactive(previous);
            validations.Add(context);
            Assert.Equal(7L, context.GetInteger("value"));
        }).Build();
        using var result = Loader(registry).Load(LoadFixture.Scene(schema, count: 2));
        Assert.Equal((2, 2), (validations.Count, imports.Count));
        Assert.NotSame(validations[0], validations[1]); Assert.NotSame(imports[0], imports[1]);
        foreach (var previous in validations) AssertInactive(previous);
        foreach (var previous in imports)
        {
            AssertInactive(previous);
            Assert.Throws<InvalidOperationException>(() => previous.Entity);
            Assert.Throws<InvalidOperationException>(() => previous.Parent);
            Assert.Throws<InvalidOperationException>(() => previous.GetEntityReference("value"));
            Assert.Throws<InvalidOperationException>(() => previous.GetAssetReference("value"));
            var acquisitions = 0;
            Assert.Throws<InvalidOperationException>(() => previous.Acquire(_ => { acquisitions++; return new Lease(); }));
            Assert.Equal(0, acquisitions);
        }
        Assert.Equal((2, 1), (result.World.EntityCount, result.World.ComponentTypeCount));
    }

    [Fact]
    public void NullableReferences_ReturnNullWithoutImplicitStringOrAssetResolution()
    {
        var schema = LoadFixture.Schema(LoadFixture.Property("target", PropertyValue.Null, true), LoadFixture.Property("asset", PropertyValue.Null, true));
        var registry = new ComponentRegistryBuilder().Register(schema, context =>
        {
            Assert.Null(context.GetEntityReference("target"));
            Assert.Null(context.GetEntityData("target"));
            Assert.Null(context.GetAssetReference("asset"));
            return new LoadFixture.Value(17);
        }, [new("target", ComponentReferenceKind.Entity), new("asset", ComponentReferenceKind.Asset)]).Build();
        using var result = Loader(registry).Load(LoadFixture.Scene(schema));
        Assert.Equal(new LoadFixture.Value(17), result.World.GetComponent<LoadFixture.Value>(result.Entities[LoadFixture.First]));
    }

    [Theory]
    [InlineData("entity-from-asset", "asset")]
    [InlineData("asset-from-entity", "target")]
    [InlineData("data-from-asset", "asset")]
    [InlineData("undeclared", "text")]
    public void ReferenceHelpers_RejectUndeclaredOrWrongSemantics(string operation, string property)
    {
        var asset = Guid.Parse("a1000000-0000-0000-0000-000000000099");
        var schema = LoadFixture.Schema(LoadFixture.Property("target", PropertyValue.From(LoadFixture.First.Value.ToString("D"))),
            LoadFixture.Property("asset", PropertyValue.From(asset.ToString("D"))), LoadFixture.Property("text", PropertyValue.From(LoadFixture.First.Value.ToString("D"))));
        var assets = 0;
        var registry = new ComponentRegistryBuilder().WithAssetValidator((id, _) => { assets++; Assert.Equal(asset, id); }).Register(schema, context =>
        {
            switch (operation)
            {
                case "entity-from-asset": context.GetEntityReference("asset"); break;
                case "asset-from-entity": context.GetAssetReference("target"); break;
                case "data-from-asset": context.GetEntityData("asset"); break;
                case "undeclared": context.GetEntityReference("text"); break;
            }
            return new LoadFixture.Value(0);
        }, [new("target", ComponentReferenceKind.Entity), new("asset", ComponentReferenceKind.Asset)]).Build();
        var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry).Load(LoadFixture.Scene(schema)));
        Assert.Equal(SceneWorldLoadError.InvalidReference, error.Error);
        Assert.Equal(property, error.PropertyId);
        Assert.Equal(1, assets);
    }

    private static SceneWorldLoader Loader(ComponentRegistry registry, IFactory<World>? worlds = null)
        => new(registry, worlds ?? LoadFixture.Worlds(), LoadFixture.Resolution());
    private static ComponentSchema Scalars() => LoadFixture.Schema(
        LoadFixture.Property("flag", PropertyValue.From(true)), LoadFixture.Property("integer", PropertyValue.From(42L)),
        LoadFixture.Property("number", PropertyValue.From(3.5d)), LoadFixture.Property("text", PropertyValue.From("default")),
        LoadFixture.Property("nullable", PropertyValue.Null, true));
    private static void AssertInactive(ComponentValidationContext context)
    {
        Assert.Throws<InvalidOperationException>(() => context.Scene);
        Assert.Throws<InvalidOperationException>(() => context.SourceEntity);
        Assert.Throws<InvalidOperationException>(() => context.Component);
        Assert.Throws<InvalidOperationException>(() => context.Schema);
        Assert.Throws<InvalidOperationException>(() => context.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => context.GetValue("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetBoolean("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetInteger("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetInt32("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetNumber("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetSingle("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetString("value"));
        Assert.Throws<InvalidOperationException>(() => context.GetEntityData("value"));
    }
    private readonly record struct ScalarsValue(bool Flag, int Integer, float Number, int TextLength);
    private readonly record struct FloatValue(float Number);
    private sealed class Lease : IDisposable { public void Dispose() { } }
}
