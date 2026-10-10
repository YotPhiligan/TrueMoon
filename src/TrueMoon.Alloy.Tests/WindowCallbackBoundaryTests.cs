using TrueMoon.Alloy.Platform.Silk;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class WindowCallbackBoundaryTests
{
    [Fact]
    public void SuccessfulCallbacksContinueAndVerificationDoesNotDispatch()
    {
        var boundary = new WindowCallbackBoundary();
        var calls = 0;
        boundary.Execute(() => calls++);
        boundary.Verify();
        boundary.Execute(() => calls++);
        boundary.Verify();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void FailureReturnsNormallyThenVerificationPreservesCauseAndStack()
    {
        var boundary = new WindowCallbackBoundary();
        var failure = new InvalidOperationException("callback failure");
        Assert.Null(Record.Exception(() => boundary.Execute(() => ThrowFromNativeHandler(failure))));
        var reported = Assert.Throws<InvalidOperationException>(boundary.Verify);
        Assert.Same(failure, reported);
        Assert.Contains(nameof(ThrowFromNativeHandler), reported.StackTrace!);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(boundary.Verify));
    }

    [Fact]
    public void LaterCallbacksCannotRunUserCodeOrReplaceFirstCause()
    {
        var boundary = new WindowCallbackBoundary();
        var first = new ArgumentException("first");
        var calls = 0;
        boundary.Execute(() => throw first);
        boundary.Execute(() => { calls++; throw new Exception("second"); });
        Assert.Equal(0, calls);
        Assert.Same(first, Assert.Throws<ArgumentException>(boundary.Verify));
    }

    [Fact]
    public void ReentrantFailurePreservesFirstCapturedCause()
    {
        var boundary = new WindowCallbackBoundary();
        var first = new ArgumentException("nested first");
        boundary.Execute(() =>
        {
            boundary.Execute(() => throw first);
            throw new InvalidOperationException("outer second");
        });
        Assert.Same(first, Assert.Throws<ArgumentException>(boundary.Verify));
    }

    [Fact]
    public void FreshBoundaryIsIndependentOfFaultedWindow()
    {
        var faulted = new WindowCallbackBoundary();
        faulted.Execute(() => throw new InvalidOperationException("old"));
        var next = new WindowCallbackBoundary();
        var ran = false;
        next.Execute(() => ran = true);
        next.Verify();
        Assert.True(ran);
        Assert.Throws<InvalidOperationException>(faulted.Verify);
    }

    [Fact]
    public void PendingHookFailureIsCapturedBeforeUserHandlerAndLaterVerification()
    {
        var pending = new ArgumentException("native hook cause");
        var verifies = 0; var handlers = 0;
        var boundary = new WindowCallbackBoundary(() => { verifies++; throw pending; });
        Assert.Null(Record.Exception(() => boundary.Execute(() => handlers++)));
        boundary.Execute(() => handlers++);
        Assert.Equal(1, verifies);
        Assert.Equal(0, handlers);
        Assert.Same(pending, Assert.Throws<ArgumentException>(boundary.Verify));
    }

    private static void ThrowFromNativeHandler(Exception failure) => throw failure;
}
