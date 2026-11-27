using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Argentis.Generator.Tests;

/// <summary>UIA4.8a: [ViewConstructor] selects the generated activation constructor; ARG024 reports ambiguity.</summary>
public class ViewConstructorTests
{
    [Fact]
    public void MarkedConstructorResolvesATieAndIsUsedInTheFactory()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Services;
            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Views;

            internal sealed class ChosenMarker { }

            internal sealed class TiedView : View
            {
                public TiedView(Text a) { }
                [ViewConstructor] public TiedView(ChosenMarker a) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.Singleton<ChosenMarker>(_ => new()).View<TiedView>();
            }
            """);
        Assert.Empty(run.Diagnostics);
        var source = TestHelper.GeneratedFile(run, "ViewRegistrations.g.cs")!;
        Assert.Contains("global::App.ChosenMarker", source);
        Assert.DoesNotContain("TrueMoon.Argentis.Text", source);
    }

    [Fact]
    public void MarkedConstructorOverridesTheLongestHeuristic()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Services;
            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis.Views;

            internal sealed class ChosenMarker { }
            internal sealed class OtherMarker { }

            internal sealed class ShortView : View
            {
                public ShortView(OtherMarker a, OtherMarker b) { }
                [ViewConstructor] public ShortView(ChosenMarker a) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.Singleton<ChosenMarker>(_ => new()).View<ShortView>();
            }
            """);
        Assert.Empty(run.Diagnostics);
        var source = TestHelper.GeneratedFile(run, "ViewRegistrations.g.cs")!;
        Assert.Contains("global::App.ChosenMarker", source);
        Assert.DoesNotContain("OtherMarker b", source);
    }

    [Fact]
    public void TiedConstructorsWithoutMarkingReportARG024()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Services;
            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis.Views;

            internal sealed class MarkerA { }
            internal sealed class MarkerB { }

            internal sealed class TiedView : View
            {
                public TiedView(MarkerA a) { }
                public TiedView(MarkerB a) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<TiedView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG024", diagnostic.Id);
        Assert.Contains("mark exactly one with [ViewConstructor]", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleMarkedConstructorsReportARG020()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Services;
            using TrueMoon.Argentis.Views;

            internal sealed class MarkerA { }
            internal sealed class MarkerB { }

            internal sealed class MarkedView : View
            {
                [ViewConstructor] public MarkedView(MarkerA a) { }
                [ViewConstructor] public MarkedView(MarkerB a) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<MarkedView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG020", diagnostic.Id);
        Assert.Contains("more than one constructor is marked with ViewConstructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void MarkedNonPublicConstructorReportsARG020()
    {
        var run = TestHelper.Verify("""
            namespace App;
            using TrueMoon.Services;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis.Views;

            internal sealed class MarkerA { }
            internal sealed class MarkerB { }

            internal sealed class SealedView : View
            {
                public SealedView(MarkerA a) { }
                [ViewConstructor] private SealedView(MarkerB a) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<SealedView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG020", diagnostic.Id);
        Assert.Contains("must be public", diagnostic.GetMessage(), StringComparison.Ordinal);
    }
}
