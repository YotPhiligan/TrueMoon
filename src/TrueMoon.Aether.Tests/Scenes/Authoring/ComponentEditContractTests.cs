using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class ComponentEditContractTests
{
    [Fact]
    public void Batch_FreezesBothCallerSequencesIncludingImmutableAdditionData()
    {
        var operation = Operation(7);
        var values = new Dictionary<string, PropertyValue> { ["note"] = PropertyValue.From("original") };
        var addition = new ComponentData(new(Id(104)), CustomType, 1, values);
        var properties = new List<PropertyEdit> { new(First, Transform, "x", PropertyValue.From(9d)) };
        var components = new List<ComponentEdit> { new(First, addition.Id, ComponentChangeKind.Add, addition) };
        var batch = new ComponentEditBatch(operation, properties, components);
        properties.Clear(); components.Clear(); values["note"] = PropertyValue.From("changed");

        Assert.Same(operation, batch.Operation);
        Assert.Equal(new[] { new PropertyEdit(First, Transform, "x", PropertyValue.From(9d)) }, batch.Properties);
        var edit = Assert.Single(batch.Components);
        Assert.Equal(ComponentChangeKind.Add, edit.Kind);
        Assert.Equal(First, edit.EntityId);
        Assert.Equal(addition.Id, edit.ComponentId);
        Assert.Same(addition, edit.Addition);
        Assert.Equal("original", edit.Addition!.Properties["note"].GetString());
    }

    [Fact]
    public void Batch_RejectsNullOperationNullPropertiesAndEmptyCombinedRequest()
    {
        Assert.Throws<ArgumentNullException>(() => new ComponentEditBatch(null!, [new(First, Transform, "x")]));
        Assert.Throws<ArgumentNullException>(() => new ComponentEditBatch(Operation(7), null!));
        Assert.Throws<ArgumentException>(() => new ComponentEditBatch(Operation(7), []));
        Assert.Throws<ArgumentException>(() => new ComponentEditBatch(Operation(7), [], []));
        var structural = new ComponentEditBatch(Operation(7), [], [new(First, Custom, ComponentChangeKind.Remove)]);
        Assert.Empty(structural.Properties);
        Assert.Equal(ComponentChangeKind.Remove, Assert.Single(structural.Components).Kind);
    }

    [Theory]
    [InlineData(4096, 0)]
    [InlineData(4095, 1)]
    [InlineData(0, 4096)]
    public void Batch_AcceptsExact4096CombinedLimit(int properties, int components)
    {
        var batch = new ComponentEditBatch(Operation(7),
            Enumerable.Range(0, properties).Select(index => new PropertyEdit(First, Transform, index.ToString())),
            Enumerable.Range(0, components).Select(index => new ComponentEdit(First, new(Id(1000 + index)), ComponentChangeKind.Remove)));

        Assert.Equal(properties, batch.Properties.Length);
        Assert.Equal(components, batch.Components.Length);
        if (properties != 0) Assert.Equal((properties - 1).ToString(), batch.Properties[^1].PropertyId);
        if (components != 0) Assert.Equal(new ComponentId(Id(999 + components)), batch.Components[^1].ComponentId);
    }

    [Theory]
    [InlineData(4097, 0)]
    [InlineData(4096, 1)]
    [InlineData(0, 4097)]
    public void Batch_RejectsImmediatelyBeyondCombinedLimit(int properties, int components)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComponentEditBatch(Operation(7),
            Enumerable.Range(0, properties).Select(index => new PropertyEdit(First, Transform, index.ToString())),
            Enumerable.Range(0, components).Select(index => new ComponentEdit(First, new(Id(1000 + index)), ComponentChangeKind.Remove))));
    }

    [Fact]
    public void EditComponents_ConfiguredCombinedLimitRejectsBeforeResolutionAndAcceptsExactLimit()
    {
        var resolutions = 0;
        using var session = Session(options: new() { MaxBatchEdits = 2 }, beforeCreate: _ => resolutions++);
        var before = session.Read();
        var tooLarge = new ComponentEditBatch(Operation(7),
            [new(First, Transform, "x", PropertyValue.From(9d)), new(First, Transform, "y", PropertyValue.From(8d))],
            [new(First, Custom, ComponentChangeKind.Remove)]);

        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, session.EditComponents(tooLarge).Error);

        Assert.Same(before, session.Read());
        Assert.Equal(1, resolutions);
        Assert.True(session.EditComponents(new(Operation(7),
            [new(First, Transform, "x", PropertyValue.From(9d))],
            [new(First, Custom, ComponentChangeKind.Remove)])).Succeeded);
        Assert.Equal(2, resolutions);
        Assert.Equal(9, Number(session.Read(), First, "x"));
        Assert.False(session.Read().Resolved.Entities[First].Components.ContainsKey(Custom));
        Assert.Equal(1, session.Read().UndoCount);
    }

    [Fact]
    public void EditComponents_NullBatchDoesNotConsumeReceiptOrChangeSnapshot()
    {
        using var session = Session(options: new() { MaxOperations = 1 });
        var before = session.Read();

        Assert.Throws<ArgumentNullException>(() => session.EditComponents(null!));

        Assert.Same(before, session.Read());
        Assert.True(session.EditComponents(new(Operation(7), [new(First, Transform, "x", PropertyValue.From(9d))])).Succeeded);
        Assert.Equal(9, Number(session.Read(), First, "x"));
    }
}
