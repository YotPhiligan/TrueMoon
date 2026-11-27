using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Argentis.Generator.Tests;

/// <summary>UIA4.8b: the generated activation graph is closed inside the compilation — ARG025/ARG026.</summary>
public class RegistrationGraphTests
{
    private const string Prelude = """
        namespace App;

        using TrueMoon.Alloy.Hosting;
        using TrueMoon.Argentis;
        using TrueMoon.Argentis.Views;
        using TrueMoon.Services;

        internal sealed class Leaked { }

        internal sealed class ChildView : View
        {
        }

        """;

    [Fact]
    public void ChildViewParameterWithoutRegistrationReportsARG025()
    {
        var run = TestHelper.Verify(Prelude + """
            internal sealed class ParentView : View
            {
                public ParentView(ChildView child) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<ParentView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG025", diagnostic.Id);
        Assert.Contains("App.ChildView", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ChildViewWithExplicitRegistrationPasses()
    {
        var run = TestHelper.Verify(Prelude + """
            internal sealed class ParentView : View
            {
                public ParentView(ChildView child) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) =>
                    services.View<ChildView>().View<ParentView>();
            }
            """);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void RequiredServiceWithoutRegistrationReportsARG026()
    {
        var run = TestHelper.Verify(Prelude + """
            internal sealed class ParentView : View
            {
                public ParentView(Leaked dependency) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<ParentView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG026", diagnostic.Id);
        Assert.Contains("App.Leaked", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceRegisteredBySingletonPasses()
    {
        var run = TestHelper.Verify(Prelude + """
            internal sealed class ParentView : View
            {
                public ParentView(Leaked dependency) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) =>
                    services.Singleton<Leaked>(_ => new()).View<ParentView>();
            }
            """);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void OptionalAndNullableParametersSkipTheCheck()
    {
        var run = TestHelper.Verify(Prelude + """
            internal sealed class ParentView : View
            {
                public ParentView(Leaked? optional = null, Leaked defaulted = default!) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<ParentView>();
            }
            """);
        Assert.Empty(run.Diagnostics);
    }
}
