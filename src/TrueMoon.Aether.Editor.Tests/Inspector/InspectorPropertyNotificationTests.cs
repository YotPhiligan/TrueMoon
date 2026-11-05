using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorPropertyNotificationTests
{
    [Fact]
    public void Load_UnchangedPresentationIsSilentAndRealChangesNotifyOnlyChangedComputedParts()
    {
        var field = new InspectorProperty(DocumentData.Custom, new("value", PropertyValueKind.String, PropertyValue.Null, true), "Value", () => { });
        field.Enable(true); field.Load(PropertyValue.From("seed"), "local", true);
        var changes = new List<string?>(); field.PropertyChanged += (_, change) => changes.Add(change.PropertyName);
        field.Load(PropertyValue.From("seed"), "local", true);
        Assert.Empty(changes); Assert.True(field.CanWrite); Assert.True(field.CanReset); Assert.False(field.HasDraft);

        field.Value = "draft"; field.Reset(); field.SetError("old diagnostic"); changes.Clear();
        field.Load(PropertyValue.Null, "schema", false);
        Assert.Equal(new[] { "CanChangeNull", "CanReset", "Error", "HasDraft", "HasError", "IsNull", "IsReset", "SourceText", "Value" }, changes.Order(StringComparer.Ordinal));
        Assert.Equal("", field.Value); Assert.Equal("schema", field.SourceText); Assert.True(field.IsNull);
        Assert.False(field.CanWrite); Assert.True(field.CanChangeNull); Assert.False(field.CanReset);
        Assert.False(field.IsReset); Assert.False(field.HasDraft); Assert.False(field.HasError);

        changes.Clear(); field.Load(PropertyValue.From("true"), "schema", false);
        Assert.Equal(new[] { "BooleanValue", "CanWrite", "IsNull", "Value" }, changes.Order(StringComparer.Ordinal));
        Assert.Equal("true", field.Value); Assert.True(field.BooleanValue); Assert.True(field.CanWrite); Assert.False(field.IsNull);
        changes.Clear(); field.Load(PropertyValue.From("true"), "schema", false); Assert.Empty(changes);
    }
}
