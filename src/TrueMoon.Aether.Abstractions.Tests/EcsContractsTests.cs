using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.ManualTests.ContractHost;
using Xunit;

namespace TrueMoon.Aether.Abstractions.Tests;

public sealed class EcsContractsTests
{
    private readonly record struct Counter(int Value) : IComponent;
    private readonly record struct Velocity(int Value);
    private readonly record struct Managed(string Value) : IComponent;
    private readonly record struct Excluded;
    private readonly record struct E1; private readonly record struct E2; private readonly record struct E3; private readonly record struct E4;
    private readonly record struct E5; private readonly record struct E6; private readonly record struct E7; private readonly record struct E8;
    private static EcsSystemContext Context(ProbeAccess access) => new(access, access.Invocation, 17, TimeSpan.FromSeconds(.02), EcsPhase.Gameplay, access.Cancellation);
    private static ProbeAccess Access() => new(new EcsSystemDescription("counter").Write<Counter>().Write<Velocity>().Read<Excluded>().Freeze());
    [Fact]
    public void Metadata_IsFrozen_RequiresDeclaredAccess_AndRetainsUnmarkedValueCompatibility()
    {
        var description = new EcsSystemDescription("counter").Read<Counter>().Write<Velocity>().After("input").Before("render");
        var frozen = description.Freeze(); description.Write<Counter>().After("later");
        Assert.Equal("counter", frozen.Id); Assert.Equal(EcsPhase.Gameplay, frozen.Phase);
        Assert.Equal(new[] { typeof(Counter) }, frozen.Reads); Assert.Equal(new[] { typeof(Velocity) }, frozen.Writes);
        Assert.True(frozen.CanRead(typeof(Velocity))); Assert.False(frozen.CanWrite(typeof(Counter))); Assert.False(frozen.CanRead(typeof(Excluded)));
        Assert.Equal(new[] { "input" }, frozen.After); Assert.Equal(new[] { "render" }, frozen.Before);
        Assert.Throws<NotSupportedException>(() => ((IList<Type>)frozen.Reads)[0] = typeof(Managed));
        Assert.Throws<NotSupportedException>(() => description.Read<Managed>()); Assert.Throws<NotSupportedException>(() => description.Write<Managed>());
        ComponentStorage.Validate<Velocity>(); Assert.False(typeof(IComponent).IsAssignableFrom(typeof(Velocity)));
    }
    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("кириллица")]
    public void InvalidSystemIdentifiers_AreRejected(string id) => Assert.ThrowsAny<ArgumentException>(() => new EcsSystemDescription(id));
    [Fact]
    public void IdentifierOrderAndBudgetBoundaries_AreExplicit()
    {
        var d = new EcsSystemDescription(new('a', 128), EcsPhase.Extraction); Assert.Equal(128, d.Id.Length);
        Assert.Throws<ArgumentException>(() => new EcsSystemDescription(new('a', 129)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemDescription("bad", (EcsPhase)123));
        Assert.Throws<ArgumentException>(() => d.After(d.Id)); Assert.Throws<ArgumentException>(() => d.Before(d.Id));
        for (var i = 0; i < 1024; i++) d.After("dependency." + i); d.After("dependency.0"); Assert.Equal(1024, d.Freeze().After.Count);
        Assert.Throws<InvalidOperationException>(() => d.After("overflow")); Assert.Equal(1024, d.Freeze().After.Count);
    }
    [Fact]
    public void ContextQueriesAndWriters_UseTypedCallbacks_AndExpireAcrossLaterInvocations()
    {
        var access = Access(); var context = Context(access); var query = context.Query<Counter>(); var pair = context.Query<Counter, Velocity>(); var writer = context.Commands;
        Assert.Equal(17UL, context.TickNumber); Assert.Equal(TimeSpan.FromSeconds(.02), context.DeltaTime); Assert.Equal(EcsPhase.Gameplay, context.Phase);
        Assert.True(context.IsAlive(access.Entity)); Assert.True(context.HasComponent<Counter>(access.Entity)); Assert.True(context.TryGetComponent<Counter>(access.Entity, out var copy)); Assert.Equal(default, copy);
        var seen = 0;
        query.ForEach((EntityHandle e, in Counter value) => { Assert.Equal(access.Entity, e); Assert.Equal(0, value.Value); seen++; });
        query.ForEachWrite((EntityHandle e, ref Counter value) => { value = new(3); Assert.Equal(3, value.Value); seen++; });
        pair.ForEach((EntityHandle e, in Counter first, in Velocity second) => { Assert.Equal(0, first.Value + second.Value); seen++; });
        pair.ForEachWrite((EntityHandle e, ref Counter first, in Velocity second) => { first = new(5); Assert.Equal(5, first.Value); seen++; });
        pair.ForEachWriteBoth((EntityHandle e, ref Counter first, ref Velocity second) => { first = new(7); second = new(2); Assert.Equal(9, first.Value + second.Value); seen++; });
        var deferred = writer.CreateEntity(); writer.AddComponent(deferred, new Counter(11)); writer.SetComponent(deferred, new Counter(13)); writer.RemoveComponent<Counter>(deferred); writer.DestroyEntity(deferred);
        Assert.Equal(5, writer.Count); Assert.Equal(2, access.ValueRecorded); Assert.Equal(5, seen);
        var marker = new IOException("callback marker");
        Assert.Same(marker, Assert.Throws<IOException>(() => query.ForEachWrite((EntityHandle entity, ref Counter value) => throw marker)));
        context.RequestStop(); Assert.True(access.StopRequested);
        access.End(); Assert.Throws<InvalidOperationException>(() => _ = context.TickNumber); Assert.Throws<InvalidOperationException>(() => _ = writer.Count);
        access.Next(); Assert.Throws<InvalidOperationException>(() => query.ForEach((EntityHandle e, in Counter value) => { }));
        Assert.Throws<InvalidOperationException>(() => pair.ForEachWriteBoth((EntityHandle e, ref Counter a, ref Velocity b) => { }));
        Assert.Throws<InvalidOperationException>(() => writer.CreateEntity()); Assert.Equal(17UL, Context(access).TickNumber);
    }
    [Fact]
    public void Exclusions_AreIndependentDistinctAndBounded_WithRequiredTypesPreserved()
    {
        var access = Access(); var original = Context(access).Query<Counter>();
        Assert.Throws<ArgumentException>(() => original.Without<Counter>());
        Assert.Throws<ArgumentException>(() => Context(access).Query<Counter, Counter>());
        Assert.Throws<ArgumentException>(() => Context(access).Query<Counter, Velocity>().Without<Velocity>());
        Assert.Throws<NotSupportedException>(() => original.Without<Managed>());
        var eight = original.Without<E1>().Without<E2>().Without<E3>().Without<E4>().Without<E5>().Without<E6>().Without<E7>().Without<E8>();
        _ = eight.Without<E1>(); Assert.Throws<InvalidOperationException>(() => eight.Without<Excluded>());
        original.ForEach((EntityHandle e, in Counter value) => { }); Assert.Equal(1, access.QueryCallbacks);
        Assert.Throws<InvalidOperationException>(() => eight.ForEach((EntityHandle e, in Counter value) => { })); // Undeclared exclusions reach the host guard.
        original.Without<Excluded>().ForEach((EntityHandle e, in Counter value) => { }); Assert.Equal(2, access.QueryCallbacks);
    }
    [Fact]
    public async Task UndeclaredAccessCancellationAndWrongThread_AreRejectedBeforeDispatch()
    {
        var access = new ProbeAccess(new EcsSystemDescription("reader").Read<Counter>().Freeze()); var context = Context(access);
        Assert.Throws<InvalidOperationException>(() => context.Query<Counter>().ForEachWrite((EntityHandle e, ref Counter c) => { }));
        Assert.Throws<InvalidOperationException>(() => context.HasComponent<Velocity>(access.Entity));
        Assert.Throws<InvalidOperationException>(() => context.Commands.AddComponent(access.Entity, new Counter(1)));
        Assert.Throws<ArgumentException>(() => context.TryGetComponent<Counter>(new(Guid.NewGuid(), 0, 1), out _));
        Assert.Throws<ArgumentException>(() => context.HasComponent<Counter>(new(access.Entity.WorldId, 0, 2)));
        var crossThread = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() => { try { Assert.Throws<InvalidOperationException>(() => _ = context.TickNumber); crossThread.SetResult(); } catch (Exception error) { crossThread.SetException(error); } }).Start();
        await crossThread.Task;
        Assert.Equal(0, access.Commands); Assert.Equal(0, access.QueryCallbacks);
        // Create the next owner-bound double on this continuation's actual thread.
        access = Access(); using var end = new CancellationTokenSource(); access.Cancellation = end.Token; context = Context(access); var writer = context.Commands; end.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => writer.CreateEntity()); Assert.Equal(0, access.Commands);
    }
    [Fact]
    public void DefaultInvalidAndCancelledContexts_NeverPublishAccess()
    {
        Assert.Throws<InvalidOperationException>(() => _ = default(EcsSystemContext).Commands);
        Assert.Throws<InvalidOperationException>(() => default(EcsQuery<Counter>).ForEach((EntityHandle e, in Counter c) => { }));
        Assert.Throws<InvalidOperationException>(() => default(EcsCommandWriter).CreateEntity());
        var access = Access();
        Assert.Throws<ArgumentNullException>(() => new EcsSystemContext(null!, 1, 1, TimeSpan.FromSeconds(1), EcsPhase.Input));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemContext(access, 0, 1, TimeSpan.FromSeconds(1), EcsPhase.Input));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemContext(access, 1, 0, TimeSpan.FromSeconds(1), EcsPhase.Input));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemContext(access, 1, 1, TimeSpan.Zero, EcsPhase.Input));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemContext(access, 1, 1, TimeSpan.FromSeconds(1), (EcsPhase)99));
        using var end = new CancellationTokenSource(); end.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new EcsSystemContext(access, 1, 1, TimeSpan.FromSeconds(1), EcsPhase.Input, end.Token));
        Assert.Throws<NotSupportedException>(() => Context(access).Query<Managed>());
        Assert.Throws<NotSupportedException>(() => Context(access).Commands.AddComponent(access.Entity, new Managed("not unmanaged")));
    }
    [Fact]
    public void TransientReservationsAndPersistentSchemaIds_HaveSeparateExactIdentityRules()
    {
        Assert.Equal(Guid.Empty, default(EntityHandle).WorldId);
        Assert.Throws<ArgumentException>(() => new EntityHandle(Guid.Empty, 0, 1)); Assert.Throws<ArgumentOutOfRangeException>(() => new EntityHandle(Guid.NewGuid(), -1, 1)); Assert.Throws<ArgumentOutOfRangeException>(() => new EntityHandle(Guid.NewGuid(), 0, 0));
        var world = Guid.NewGuid(); var handle = new EntityHandle(world, 0, 1); Assert.NotEqual(handle, new EntityHandle(world, 0, 2)); Assert.NotEqual(handle, new EntityHandle(Guid.NewGuid(), 0, 1));
        var buffer = Guid.NewGuid(); var batch = Guid.NewGuid(); var reserved = new DeferredEntity(buffer, batch, 0); var values = new[] { handle }; var result = new CommandPlaybackResult(buffer, batch, values); values[0] = default;
        Assert.Equal(1, result.CreatedEntityCount); Assert.Equal(handle, result.Resolve(reserved));
        Assert.Throws<ArgumentException>(() => result.Resolve(new(buffer, Guid.NewGuid(), 0))); Assert.Throws<ArgumentException>(() => result.Resolve(new(Guid.NewGuid(), batch, 0))); Assert.Throws<ArgumentException>(() => result.Resolve(new(buffer, batch, 1))); Assert.Throws<ArgumentException>(() => result.Resolve(default));
        Assert.Throws<ArgumentException>(() => new DeferredEntity(buffer, Guid.Empty, 0)); Assert.Throws<ArgumentOutOfRangeException>(() => new DeferredEntity(buffer, batch, -1));
        Assert.Throws<ArgumentException>(() => new CommandPlaybackResult(buffer, batch, new[] { default(EntityHandle) }));
        var schema = new ComponentSchemaReference(new ComponentTypeId(Guid.NewGuid()), 1); Assert.Equal(1, schema.Version); Assert.NotEqual(schema, new ComponentSchemaReference(schema.TypeId, 2));
        Assert.Throws<ArgumentException>(() => new ComponentSchemaReference(default, 1)); Assert.Throws<ArgumentOutOfRangeException>(() => new ComponentSchemaReference(schema.TypeId, 0));
    }
}
