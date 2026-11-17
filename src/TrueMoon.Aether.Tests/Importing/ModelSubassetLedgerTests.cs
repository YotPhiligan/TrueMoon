using TrueMoon.Aether.Tooling.Importing;
using Xunit;

namespace TrueMoon.Aether.Tests.Importing;

public sealed class ModelSubassetLedgerTests
{
    private static readonly Guid SourceId = new("868af9c7-eb6d-4c99-bc64-9a76dfe24951");
    private static ModelSubassetKey Mesh(string locator) => new(ModelSubassetKind.Mesh, locator);
    private static ModelSubassetEntry Entry(ModelSubassetKey key, bool active = true) => new(key, Guid.NewGuid(), active);

    [Fact]
    public void Reconcile_AllKindsAndOrdinalLocatorsGetDistinctSourceIndependentIds()
    {
        var keys = Enum.GetValues<ModelSubassetKind>().Select(kind => new ModelSubassetKey(kind, "shared"))
            .Append(Mesh("Hull")).Append(Mesh("hull")).ToArray();
        var result = new ModelSubassetLedger(SourceId, []).Reconcile(keys);

        Assert.Equal(keys.Length, result.Entries.Count);
        Assert.Equal(keys.Length, result.Entries.Select(entry => entry.Id).Distinct().Count());
        Assert.All(result.Entries, entry => { Assert.True(entry.Active); Assert.NotEqual(Guid.Empty, entry.Id); Assert.NotEqual(SourceId, entry.Id); });
        var reordered = result.Reconcile(keys.Reverse());
        Assert.Equal(result.Entries, reordered.Entries);
        Assert.NotEqual(result.Entries.Single(entry => entry.Key == Mesh("Hull")).Id,
            result.Entries.Single(entry => entry.Key == Mesh("hull")).Id);
    }

    [Fact]
    public void Reconcile_ReorderAddAndDeletePreservesIdsAndOriginalLedger()
    {
        var hull = Entry(Mesh("Hull")); var paint = Entry(new(ModelSubassetKind.Material, "Paint"));
        var original = new ModelSubassetLedger(SourceId, [paint, hull]);
        var result = original.Reconcile([Mesh("Wheel"), hull.Key]);

        Assert.Equal(hull, result.Entries.Single(entry => entry.Key == hull.Key));
        Assert.Equal(paint with { Active = false }, result.Entries.Single(entry => entry.Key == paint.Key));
        var added = result.Entries.Single(entry => entry.Key == Mesh("Wheel"));
        Assert.True(added.Active); Assert.DoesNotContain(added.Id, original.Entries.Select(entry => entry.Id));
        Assert.Equal(new[] { hull, paint }, original.Entries);
        Assert.All(original.Entries, entry => Assert.True(entry.Active));
    }

    [Fact]
    public void Reconcile_ExplicitRestoreAndActiveOrTombstoneRenameRetainIds()
    {
        var hull = Entry(Mesh("Hull")); var paint = Entry(new(ModelSubassetKind.Material, "Paint"));
        var original = new ModelSubassetLedger(SourceId, [hull, paint]);
        var deleted = original.Reconcile([hull.Key]);
        var restored = deleted.Reconcile([hull.Key, paint.Key], [new(paint.Key, paint.Key)]);
        Assert.Equal(original.Entries, restored.Entries);

        var body = Mesh("Body"); var finish = new ModelSubassetKey(ModelSubassetKind.Material, "Finish");
        var renamed = deleted.Reconcile([finish, body], [new(hull.Key, body), new(paint.Key, finish)]);
        Assert.Equal(new[] { hull with { Key = body }, paint with { Key = finish } }, renamed.Entries);
        Assert.Equal(paint with { Active = false }, deleted.Entries.Single(entry => entry.Key == paint.Key));
    }

    [Fact]
    public void Reconcile_TombstoneReappearanceWithoutMappingFailsWithoutMutation()
    {
        var original = new ModelSubassetLedger(SourceId, [Entry(Mesh("Hull"), false)]);
        var before = original.Entries.ToArray();
        Assert.Throws<InvalidDataException>(() => original.Reconcile([Mesh("Hull")]));
        Assert.Equal(before, original.Entries);
        Assert.False(Assert.Single(original.Entries).Active);
    }

    [Fact]
    public void Reconcile_EmptyDiscoveryRetainsEveryIdentityAsTombstone()
    {
        Assert.Empty(new ModelSubassetLedger(SourceId, []).Reconcile([]).Entries);
        var original = new ModelSubassetLedger(SourceId, [Entry(Mesh("A")), Entry(Mesh("B"), false)]);
        var deleted = original.Reconcile([]);
        Assert.Equal(original.Entries.Select(entry => entry with { Active = false }), deleted.Entries);
        Assert.Equal(deleted.Entries, deleted.Reconcile([]).Entries);
    }

    [Fact]
    public void ConstructorAndReconcileDetachInputsAndExposeCanonicalReadOnlyEntries()
    {
        var first = Entry(Mesh("B")); var second = Entry(Mesh("a"));
        var entries = new[] { second, first }; var original = new ModelSubassetLedger(SourceId, entries);
        entries[0] = Entry(Mesh("changed"));
        Assert.Equal(new[] { first, second }, original.Entries);
        Assert.Throws<NotSupportedException>(() => ((IList<ModelSubassetEntry>)original.Entries).Clear());

        var keys = new[] { second.Key, first.Key }; var result = original.Reconcile(keys);
        keys[0] = Mesh("changed");
        Assert.Equal(original.Entries, result.Entries);
        Assert.Throws<NotSupportedException>(() => ((IList<ModelSubassetEntry>)result.Entries).Clear());
    }

    [Theory]
    [InlineData("empty-source")]
    [InlineData("empty-id")]
    [InlineData("source-id")]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-key")]
    [InlineData("unknown-kind")]
    [InlineData("null-entry")]
    public void Constructor_RejectsInvalidOrAmbiguousIdentities(string invalid)
    {
        var first = Entry(Mesh("A")); var entries = new[] { first }; var source = SourceId;
        switch (invalid)
        {
            case "empty-source": source = Guid.Empty; break;
            case "empty-id": entries = [first with { Id = Guid.Empty }]; break;
            case "source-id": entries = [first with { Id = SourceId }]; break;
            case "duplicate-id": entries = [first, first with { Key = Mesh("B") }]; break;
            case "duplicate-key": entries = [first, Entry(first.Key)]; break;
            case "unknown-kind": entries = [first with { Key = new((ModelSubassetKind)0, "A") }]; break;
            case "null-entry": entries = [null!]; break;
        }
        if (invalid == "empty-source") Assert.Throws<ArgumentException>(() => new ModelSubassetLedger(source, entries));
        else if (invalid == "null-entry") Assert.Throws<ArgumentNullException>(() => new ModelSubassetLedger(source, entries));
        else Assert.Throws<InvalidDataException>(() => new ModelSubassetLedger(source, entries));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("a\nb")] [InlineData("e\u0301")]
    public void Reconcile_RejectsMissingControlAndNonNfcLocators(string? locator)
        => Assert.Throws<InvalidDataException>(() => new ModelSubassetLedger(SourceId, []).Reconcile([Mesh(locator!)]));

    [Theory]
    [InlineData(false, 255, true)] [InlineData(false, 256, true)] [InlineData(false, 257, false)]
    [InlineData(true, 127, true)] [InlineData(true, 128, true)] [InlineData(true, 129, false)]
    public void Reconcile_LocatorBudgetCountsUtf8Bytes(bool unicode, int count, bool accepted)
    {
        var key = Mesh(new string(unicode ? '\u00e9' : 'a', count)); var empty = new ModelSubassetLedger(SourceId, []);
        if (accepted) Assert.Equal(key, Assert.Single(empty.Reconcile([key]).Entries).Key);
        else Assert.Throws<InvalidDataException>(() => empty.Reconcile([key]));
        Assert.Empty(empty.Entries);
    }

    [Theory]
    [InlineData(1023, true)] [InlineData(1024, true)] [InlineData(1025, false)]
    public void ActiveBudgetAppliesToConstructorAndReconciliation(int count, bool accepted)
    {
        var entries = Enumerable.Range(0, count).Select(index => Entry(Mesh($"mesh/{index}"))).ToArray();
        var empty = new ModelSubassetLedger(SourceId, []);
        if (accepted)
        {
            Assert.Equal(count, new ModelSubassetLedger(SourceId, entries).Entries.Count);
            Assert.Equal(count, empty.Reconcile(entries.Select(entry => entry.Key)).Entries.Count);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => new ModelSubassetLedger(SourceId, entries));
            Assert.Throws<InvalidDataException>(() => empty.Reconcile(entries.Select(entry => entry.Key)));
        }
        Assert.Empty(empty.Entries);
    }

    [Theory]
    [InlineData(4095, true)] [InlineData(4096, true)] [InlineData(4097, false)]
    public void TotalBudgetRetainsAndCountsTombstonesWhenAdding(int count, bool accepted)
    {
        var entries = Enumerable.Range(0, count).Select(index => Entry(Mesh($"old/{index}"), false)).ToArray();
        if (!accepted) { Assert.Throws<InvalidDataException>(() => new ModelSubassetLedger(SourceId, entries)); return; }
        var ledger = new ModelSubassetLedger(SourceId, entries);
        Assert.Equal(count, ledger.Entries.Count);
        if (count == 4095)
        {
            var added = ledger.Reconcile([Mesh("new")]);
            Assert.Equal(4096, added.Entries.Count); Assert.Single(added.Entries, entry => entry.Active);
            Assert.Equal(ledger.Entries, added.Entries.Where(entry => !entry.Active));
        }
        else Assert.Throws<InvalidDataException>(() => ledger.Reconcile([Mesh("new")]));
        Assert.Equal(entries.OrderBy(entry => entry.Key.Locator, StringComparer.Ordinal), ledger.Entries);
    }

    [Theory]
    [InlineData("cross-kind")] [InlineData("unknown-source")] [InlineData("absent-target")]
    [InlineData("duplicate-source")] [InlineData("duplicate-target")] [InlineData("existing-target")]
    [InlineData("still-present-source")] [InlineData("duplicate-discovery")]
    public void Reconcile_InvalidMappingsAndAmbiguityLeaveOriginalLedgerUnchanged(string invalid)
    {
        var a = Mesh("A"); var b = Mesh("B"); var c = Mesh("C"); var d = Mesh("D");
        var ledger = new ModelSubassetLedger(SourceId, [Entry(a), Entry(b)]); var before = ledger.Entries.ToArray();
        ModelSubassetKey[] discovered = [c]; ModelSubassetRename[] renames = [new(a, c)];
        switch (invalid)
        {
            case "cross-kind": discovered = [new(ModelSubassetKind.Material, "C")]; renames = [new(a, discovered[0])]; break;
            case "unknown-source": renames = [new(d, c)]; break;
            case "absent-target": discovered = [d]; break;
            case "duplicate-source": discovered = [c, d]; renames = [new(a, c), new(a, d)]; break;
            case "duplicate-target": renames = [new(a, c), new(b, c)]; break;
            case "existing-target": discovered = [b]; renames = [new(a, b)]; break;
            case "still-present-source": discovered = [a, c]; break;
            case "duplicate-discovery": discovered = [c, c]; renames = []; break;
        }
        Assert.Throws<InvalidDataException>(() => ledger.Reconcile(discovered, renames));
        Assert.Equal(before, ledger.Entries);
    }
}
