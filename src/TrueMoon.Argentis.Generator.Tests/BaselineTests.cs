using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Argentis.Generator.Tests;

/// <summary>Freezes the generator behavior that exists before the UIA4.7/UIA4.8 slice.</summary>
public class BaselineTests
{
    [Fact]
    public void InlineFactoryEmitsTypedPartProperties()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body").Named("Footer"));
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("public static class FormViewParts", source);
        Assert.Contains("extension(global::App.FormView view)", source);
        Assert.Contains("public global::TrueMoon.Argentis.Layout.VStack Body => view.GetRequiredPart<global::TrueMoon.Argentis.Layout.VStack>(\"Body\");", source);
        Assert.Contains("public global::TrueMoon.Argentis.Layout.VStack Footer => view.GetRequiredPart<global::TrueMoon.Argentis.Layout.VStack>(\"Footer\");", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void NamedOptionalEmitsNullableProperty()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new Text().NamedOptional("Badge"));
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("public global::TrueMoon.Argentis.Text? Badge => view.TryGetPart<global::TrueMoon.Argentis.Text>(\"Badge\", out var part) ? part : null;", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void InternalViewAndInternalPartTypeEmitInternalAccess()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Views;

            internal sealed class Widget : Element { }

            internal sealed class PanelView : View
            {
                public PanelView() => Content(ui => new Widget().Named("Cell"));
            }
            """);
        var source = TestHelper.GeneratedFile(run, "PanelView.ViewParts.g.cs")!;
        Assert.Contains("internal static class PanelViewParts", source);
        Assert.Contains("internal global::App.Widget Cell => view.GetRequiredPart<global::App.Widget>(\"Cell\");", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void NonConstantNameReportsARG006()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView()
                {
                    var name = "Body";
                    Content(ui => new VStack().Named(name));
                }
            }
            """);
        TestHelper.AssertSingleDiagnostic(run, "ARG006", DiagnosticSeverity.Error,
            "A generated part requires a constant C# identifier and a control type that, along with all containing types, is public or internal, non-generic, non-file-local, and accessible from the generated class");
    }

    [Fact]
    public void DuplicatePartNameReportsARG007()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body").Named("Body"));
            }
            """);
        TestHelper.AssertDiagnostic(run, "ARG007", DiagnosticSeverity.Error,
            "View 'FormView' declares more than one generated part named 'Body'");
    }

    [Fact]
    public void PartNameClashingWithMemberReportsARG008()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body"));
                public Text Body => GetRequiredPart<Text>("Body");
            }
            """);
        TestHelper.AssertDiagnostic(run, "ARG008", DiagnosticSeverity.Error,
            "View 'FormView' cannot generate 'Body' because that name already exists");
    }

    [Fact]
    public void ContentWithoutNamedPartsEmitsNothing()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack());
            }
            """);
        Assert.Empty(run.GeneratedTrees);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void ChildViewPartGetsTypedProperty()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class DetailsView : View
            {
                public DetailsView() => Content(ui => new Text().Named("Caption"));
            }

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body").WithChildren(new DetailsView().Named("Details")));
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("public global::App.DetailsView Details => view.GetRequiredPart<global::App.DetailsView>(\"Details\");", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void InternalChildViewPartGetsInternalProperty()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            internal sealed class DetailsView : View
            {
                public DetailsView() => Content(ui => new Text().Named("Caption"));
            }

            internal sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body").WithChildren(new DetailsView().Named("Details")));
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("internal global::App.DetailsView Details => view.GetRequiredPart<global::App.DetailsView>(\"Details\");", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void ViewWithoutPublicConstructorReportsARG020()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis.Views;
            using TrueMoon.Services;

            internal sealed class VoidView : View
            {
                private VoidView() { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<VoidView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG020", diagnostic.Id);
        Assert.Contains("no public instance constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void TiedLongestPublicConstructorsReportARG024()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;
            using TrueMoon.Services;

            internal sealed class TiedView : View
            {
                public TiedView(Text a) { }
                public TiedView(Button a) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.View<TiedView>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("ARG024", diagnostic.Id);
        Assert.Contains("more than one longest public constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulActivationEmitsRegistration()
    {
        var (run, output) = TestHelper.Generate("""
            namespace App;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;
            using TrueMoon.Services;

            internal sealed class RowView : View
            {
                public RowView(Text label) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.Singleton<Text>(_ => new()).View<RowView>();
            }
            """);
        Assert.Empty(run.Diagnostics);
        var source = TestHelper.GeneratedFile(run, "ViewRegistrations.g.cs")!;
        Assert.Contains("global::TrueMoon.Alloy.Hosting.Generation.ViewActivation<global::App.RowView>.Register(static context => new global::App.RowView(context.Resolve<global::TrueMoon.Argentis.Text>()));", source);
        TestHelper.AssertCompiles(output);
    }

    [Fact]
    public void ViewWithArgsMissingParameterReportsARG021()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;
            using TrueMoon.Services;

            internal sealed class RowView : View
            {
                public RowView(Text label, int value) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.ViewWithArgs<RowView, string>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.True(diagnostic.Id == "ARG021", diagnostic.GetMessage());
        Assert.Contains("no parameter of this type in the selected constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ViewWithArgsAmbiguousParameterReportsARG021()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Alloy.Hosting;
            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;
            using TrueMoon.Services;

            internal sealed class RowView : View
            {
                public RowView(Text a, Text b) { }
            }

            internal static class Configure
            {
                public static void Register(IServicesRegistrationContext services) => services.ViewWithArgs<RowView, Text>();
            }
            """);
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.True(diagnostic.Id == "ARG021", diagnostic.GetMessage());
        Assert.Contains("more than one parameter of this type in the selected constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }
}
