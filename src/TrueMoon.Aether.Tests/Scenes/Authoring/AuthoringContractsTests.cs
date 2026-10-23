using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class AuthoringContractsTests
{
    [Theory]
    [InlineData("id")] [InlineData("revision")] [InlineData("null-actor")]
    [InlineData("empty-actor")] [InlineData("blank-actor")] [InlineData("long-actor")]
    public void Operation_RejectsInvalidIdentityRevisionAndActor(string invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AuthoringOperation(invalid == "id" ? Guid.Empty : Guid.NewGuid(),
            invalid == "revision" ? -1 : 0, invalid switch { "null-actor" => null!, "empty-actor" => "", "blank-actor" => " \t", "long-actor" => new('a', 257), _ => "actor" }));
    }

    [Fact]
    public void Operation_PreservesExactActorAndLongRevisionBoundary()
    {
        var id = Guid.NewGuid(); var actor = " автор " + new string('a', 249);
        Assert.Equal(256, actor.Length);
        var operation = new AuthoringOperation(id, long.MaxValue, actor);
        Assert.Equal(id, operation.Id); Assert.Equal(long.MaxValue, operation.ExpectedRevision); Assert.Equal(actor, operation.Actor);
        Assert.Equal(operation, new(id, long.MaxValue, actor)); Assert.NotEqual(operation, new(id, long.MaxValue, actor.ToUpperInvariant()));
    }

    [Fact]
    public void Batch_FreezesCallerOrderAndAcceptsExact4096Boundary()
    {
        var operation = Operation(7); var edits = new List<TransformEdit> { Edit(Second, 2), Edit(First, 1) };
        var batch = new TransformEditBatch(operation, edits); edits.Reverse(); edits.Clear();
        Assert.Same(operation, batch.Operation); Assert.Equal(new[] { Second, First }, batch.Edits.Select(e => e.EntityId));
        Assert.Equal(2, batch.Edits[0].Value.X); Assert.Equal(1, batch.Edits[1].Value.X);
        Assert.Equal(4096, new TransformEditBatch(operation, Enumerable.Repeat(Edit(First, 1), 4096)).Edits.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransformEditBatch(operation, Enumerable.Repeat(Edit(First, 1), 4097)));
        Assert.Throws<ArgumentException>(() => new TransformEditBatch(operation, []));
        Assert.Throws<ArgumentNullException>(() => new TransformEditBatch(null!, edits));
        Assert.Throws<ArgumentNullException>(() => new TransformEditBatch(operation, null!));
    }
}
