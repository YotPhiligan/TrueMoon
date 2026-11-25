using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class SceneMaterialBindingReferenceTests
{
    private static readonly Guid Mesh = new("fb4693d1-7951-4a12-bb60-245c93b387ae");
    private static readonly Guid ChangedMesh = new("fb4693d1-7951-4a12-bb60-245c93b387af");
    private static readonly Guid Left = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Right = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid White = new("1340e422-4a23-421f-a816-b250d209d72c");
    private static readonly Guid Red = new("e8db1916-ca88-493f-bf7b-81409b638731");

    [Fact]
    public void CombinedReference_PreservesBothPairsAndIsIndependentOfEditOrder()
    {
        var value = SceneMaterialBindingReference.Format([(Left, White), (Right, Red)]);
        Assert.Equal(value, SceneMaterialBindingReference.Format([(Right, Red), (Left, White)]));
        Assert.True(SceneMaterialBindingReference.TryParse(Mesh, value, out var binding, out var slots));
        Assert.Equal(new[] { (Left, White), (Right, Red) }, slots);
        Assert.True(SceneMaterialBindingReference.TryParse(ChangedMesh, value, out var changedMeshBinding, out _));
        Assert.NotEqual(binding, changedMeshBinding);
        var exchanged = SceneMaterialBindingReference.Format([(Left, Red), (Right, White)]);
        Assert.True(SceneMaterialBindingReference.TryParse(Mesh, exchanged, out var exchangedBinding, out _));
        Assert.NotEqual(binding, exchangedBinding);
    }

    [Fact]
    public void LegacyGuid_RemainsTheExactBindingId()
    {
        Assert.True(SceneMaterialBindingReference.TryParse(Mesh, Red.ToString("D"), out var binding, out var slots));
        Assert.Equal(Red, binding);
        Assert.Empty(slots);
    }

    [Fact]
    public void CombinedReference_RejectsDuplicateEmptyAndOverBudgetSlots()
    {
        var duplicate = SceneMaterialBindingReference.Format([(Left, White), (Left, Red)]);
        var empty = SceneMaterialBindingReference.Format([(Left, White), (Right, Guid.Empty)]);
        var limit = Enumerable.Range(0, RenderContentProfile.MaximumSlots)
            .Select(index => (new Guid(index + 1, 0, 0, new byte[8]), White)).ToArray();
        Assert.True(SceneMaterialBindingReference.TryParse(Mesh, SceneMaterialBindingReference.Format(limit), out _, out var decoded));
        Assert.Equal(limit, decoded);
        var excess = SceneMaterialBindingReference.Format([.. limit, (ChangedMesh, Red)]);
        Assert.False(SceneMaterialBindingReference.TryParse(Mesh, duplicate, out _, out _));
        Assert.False(SceneMaterialBindingReference.TryParse(Mesh, empty, out _, out _));
        Assert.False(SceneMaterialBindingReference.TryParse(Mesh, excess, out _, out _));
    }
}
