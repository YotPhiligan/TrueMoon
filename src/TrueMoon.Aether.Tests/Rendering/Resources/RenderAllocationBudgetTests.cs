using TrueMoon.Aether.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderAllocationBudgetTests
{
    [Fact]
    public void Reservations_AddIndependentByteClassesAndReleaseEachExactlyOnce()
    {
        var budget = new RenderAllocationBudget();
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
        using var first = budget.Reserve(7, 11, 13);
        using var second = budget.Reserve(17, 19, 23);
        var alias = first;
        Assert.Equal(new RenderAllocationCounts(24, 30, 36), budget.Counts);
        first.Dispose();
        alias.Dispose();
        Assert.Equal(new RenderAllocationCounts(17, 19, 23), budget.Counts);
        second.Dispose();
        second.Dispose();
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
    }

    [Theory]
    [InlineData(0, 10, true)]
    [InlineData(0, 11, true)]
    [InlineData(0, 12, false)]
    [InlineData(1, 10, true)]
    [InlineData(1, 11, true)]
    [InlineData(1, 12, false)]
    [InlineData(2, 10, true)]
    [InlineData(2, 11, true)]
    [InlineData(2, 12, false)]
    public void Reserve_EnforcesImmediateBeforeAtAndAfterBoundaryForEveryByteClass(int dimension, long bytes, bool admitted)
    {
        var budget = new RenderAllocationBudget(new()
        {
            MaximumGeometryBytes = 11,
            MaximumUploadStagingBytes = 11,
            MaximumCpuStagingBytes = 11
        });
        if (!admitted)
        {
            Error(RenderResourceError.LimitExceeded, () => Reserve(budget, dimension, bytes));
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
            using var retry = Reserve(budget, dimension, 11);
            Assert.Equal(Expected(dimension, 11), budget.Counts);
            return;
        }
        using var reservation = Reserve(budget, dimension, bytes);
        Assert.Equal(Expected(dimension, bytes), budget.Counts);
        reservation.Dispose();
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedReservation_LeavesAllThreeClassesUnchangedAndCanRetryAfterRelease(int exhaustedDimension)
    {
        var budget = new RenderAllocationBudget(new()
        {
            MaximumGeometryBytes = 11,
            MaximumUploadStagingBytes = 13,
            MaximumCpuStagingBytes = 17
        });
        using var current = budget.Reserve(3, 5, 7);
        var geometry = exhaustedDimension == 0 ? 9 : 2;
        var upload = exhaustedDimension == 1 ? 9 : 3;
        var cpu = exhaustedDimension == 2 ? 11 : 4;
        Error(RenderResourceError.LimitExceeded, () => budget.Reserve(geometry, upload, cpu));
        Assert.Equal(new RenderAllocationCounts(3, 5, 7), budget.Counts);
        using (budget.Reserve(8, 8, 10))
            Assert.Equal(new RenderAllocationCounts(11, 13, 17), budget.Counts);
        Assert.Equal(new RenderAllocationCounts(3, 5, 7), budget.Counts);

        current.Dispose();
        using var retry = budget.Reserve(geometry, upload, cpu);
        Assert.Equal(new RenderAllocationCounts(geometry, upload, cpu), budget.Counts);
        retry.Dispose();
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Reserve_RejectsNegativeByteClassWithoutChangingExistingOwnership(int dimension)
    {
        var budget = new RenderAllocationBudget();
        using var existing = budget.Reserve(2, 3, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Reserve(budget, dimension, -1));
        Assert.Equal(new RenderAllocationCounts(2, 3, 5), budget.Counts);
        using var next = budget.Reserve(7, 11, 13);
        Assert.Equal(new RenderAllocationCounts(9, 14, 18), budget.Counts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Reserve_RejectsLongMaxWithoutOverflowOrLossOfExistingCapacity(int dimension)
    {
        var budget = new RenderAllocationBudget(new()
        {
            MaximumGeometryBytes = 11,
            MaximumUploadStagingBytes = 11,
            MaximumCpuStagingBytes = 11
        });
        using var existing = budget.Reserve(5, 5, 5);
        Error(RenderResourceError.LimitExceeded, () => Reserve(budget, dimension, long.MaxValue));
        Assert.Equal(new RenderAllocationCounts(5, 5, 5), budget.Counts);
        existing.Dispose();
        using var retry = budget.Reserve(11, 11, 11);
        Assert.Equal(new RenderAllocationCounts(11, 11, 11), budget.Counts);
    }

    [Fact]
    public void ZeroByteLimits_AllowOnlyEmptyReservationsAndReleaseThemIdempotently()
    {
        var budget = new RenderAllocationBudget(new()
        {
            MaximumGeometryBytes = 0,
            MaximumUploadStagingBytes = 0,
            MaximumCpuStagingBytes = 0
        });
        using var first = budget.Reserve(0, 0, 0);
        using var second = budget.Reserve(0, 0, 0);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
        foreach (var dimension in new[] { 0, 1, 2 })
            Error(RenderResourceError.LimitExceeded, () => Reserve(budget, dimension, 1));
        first.Dispose();
        first.Dispose();
        second.Dispose();
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_CopiesEveryAllocationLimitBeforeCallerMutatesOptions(int dimension)
    {
        var options = new RenderResourceOptions
        {
            MaximumGeometryBytes = 11,
            MaximumUploadStagingBytes = 11,
            MaximumCpuStagingBytes = 11
        };
        var budget = new RenderAllocationBudget(options);
        options.MaximumGeometryBytes = 12;
        options.MaximumUploadStagingBytes = 12;
        options.MaximumCpuStagingBytes = 12;
        Error(RenderResourceError.LimitExceeded, () => Reserve(budget, dimension, 12));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
        using var reservation = Reserve(budget, dimension, 11);
        Assert.Equal(Expected(dimension, 11), budget.Counts);
    }

    [Fact]
    public async Task ConcurrentReservations_AdmitExactlyOneCompleteRequestAtCapacityAndAllowReuse()
    {
        var budget = new RenderAllocationBudget(new()
        {
            MaximumGeometryBytes = 11,
            MaximumUploadStagingBytes = 13,
            MaximumCpuStagingBytes = 17
        });
        var attempts = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            try { return (Reservation: budget.Reserve(11, 13, 17), Error: (RenderResourceException?)null); }
            catch (RenderResourceException error) { return (Reservation: (RenderAllocationReservation?)null, Error: error); }
        })));
        try
        {
            Assert.Single(attempts, attempt => attempt.Reservation is not null);
            var rejected = attempts.Where(attempt => attempt.Error is not null).ToArray();
            Assert.Equal(31, rejected.Length);
            Assert.All(rejected, attempt => Assert.Equal(RenderResourceError.LimitExceeded, attempt.Error!.Error));
            Assert.Equal(new RenderAllocationCounts(11, 13, 17), budget.Counts);
        }
        finally
        {
            foreach (var attempt in attempts) attempt.Reservation?.Dispose();
        }
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), budget.Counts);
        using var retry = budget.Reserve(11, 13, 17);
        Assert.Equal(new RenderAllocationCounts(11, 13, 17), budget.Counts);
    }

    [Theory]
    [MemberData(nameof(InvalidOptionValues))]
    public void OwnerConstructors_RejectEveryOptionBelowItsMinimumAndAboveItsProfileCeiling(string field, long value)
    {
        var options = new RenderResourceOptions();
        SetOption(options, field, value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderContentCatalog(options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderAllocationBudget(options));
    }

    public static IEnumerable<object[]> InvalidOptionValues()
    {
        (string Field, long Ceiling, bool ZeroAllowed)[] fields =
        [
            (nameof(RenderResourceOptions.MaximumCpuRevisions), 2048, false),
            (nameof(RenderResourceOptions.MaximumCpuScopes), 64, false),
            (nameof(RenderResourceOptions.MaximumCpuLeases), 16384, false),
            (nameof(RenderResourceOptions.MaximumGpuEntries), 2048, false),
            (nameof(RenderResourceOptions.MaximumActivePreparations), 4, false),
            (nameof(RenderResourceOptions.MaximumQueuedPreparations), 16, true),
            (nameof(RenderResourceOptions.MaximumCpuBytes), 67108864, true),
            (nameof(RenderResourceOptions.MaximumGeometryBytes), 67108864, true),
            (nameof(RenderResourceOptions.MaximumUploadStagingBytes), 16777216, true),
            (nameof(RenderResourceOptions.MaximumCpuStagingBytes), 16777216, true)
        ];
        foreach (var field in fields)
        {
            yield return [field.Field, -1L];
            if (!field.ZeroAllowed) yield return [field.Field, 0L];
            yield return [field.Field, field.Ceiling + 1];
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerConstructors_AcceptExactProfileCeilingsAndAllMinimumLimits(bool minimums)
    {
        var options = new RenderResourceOptions
        {
            MaximumCpuRevisions = minimums ? 1 : 2048,
            MaximumCpuScopes = minimums ? 1 : 64,
            MaximumCpuLeases = minimums ? 1 : 16384,
            MaximumGpuEntries = minimums ? 1 : 2048,
            MaximumActivePreparations = minimums ? 1 : 4,
            MaximumQueuedPreparations = minimums ? 0 : 16,
            MaximumCpuBytes = minimums ? 0 : 67108864,
            MaximumGeometryBytes = minimums ? 0 : 67108864,
            MaximumUploadStagingBytes = minimums ? 0 : 16777216,
            MaximumCpuStagingBytes = minimums ? 0 : 16777216
        };
        using var catalog = new RenderContentCatalog(options);
        Assert.Equal(new RenderCatalogCounts(0, 0, 0, 0, false), catalog.Counts);
        var budget = new RenderAllocationBudget(options);
        using var reservation = budget.Reserve(options.MaximumGeometryBytes, options.MaximumUploadStagingBytes, options.MaximumCpuStagingBytes);
        Assert.Equal(minimums ? new RenderAllocationCounts(0, 0, 0) : new RenderAllocationCounts(67108864, 16777216, 16777216), budget.Counts);
    }

    private static void SetOption(RenderResourceOptions options, string field, long value)
    {
        switch (field)
        {
            case nameof(RenderResourceOptions.MaximumCpuRevisions): options.MaximumCpuRevisions = checked((int)value); break;
            case nameof(RenderResourceOptions.MaximumCpuScopes): options.MaximumCpuScopes = checked((int)value); break;
            case nameof(RenderResourceOptions.MaximumCpuLeases): options.MaximumCpuLeases = checked((int)value); break;
            case nameof(RenderResourceOptions.MaximumGpuEntries): options.MaximumGpuEntries = checked((int)value); break;
            case nameof(RenderResourceOptions.MaximumActivePreparations): options.MaximumActivePreparations = checked((int)value); break;
            case nameof(RenderResourceOptions.MaximumQueuedPreparations): options.MaximumQueuedPreparations = checked((int)value); break;
            case nameof(RenderResourceOptions.MaximumCpuBytes): options.MaximumCpuBytes = value; break;
            case nameof(RenderResourceOptions.MaximumGeometryBytes): options.MaximumGeometryBytes = value; break;
            case nameof(RenderResourceOptions.MaximumUploadStagingBytes): options.MaximumUploadStagingBytes = value; break;
            case nameof(RenderResourceOptions.MaximumCpuStagingBytes): options.MaximumCpuStagingBytes = value; break;
            default: throw new ArgumentException("Unknown resource option.", nameof(field));
        }
    }

    private static RenderAllocationReservation Reserve(RenderAllocationBudget budget, int dimension, long bytes)
        => budget.Reserve(dimension == 0 ? bytes : 0, dimension == 1 ? bytes : 0, dimension == 2 ? bytes : 0);

    private static RenderAllocationCounts Expected(int dimension, long bytes)
        => new(dimension == 0 ? bytes : 0, dimension == 1 ? bytes : 0, dimension == 2 ? bytes : 0);

    private static void Error(RenderResourceError expected, Action action)
        => Assert.Equal(expected, Assert.Throws<RenderResourceException>(action).Error);
}
