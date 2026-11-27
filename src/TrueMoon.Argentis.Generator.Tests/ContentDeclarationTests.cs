using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Argentis.Generator.Tests;

/// <summary>UIA4.7: recognized view content declaration positions and ARG022.</summary>
public class ContentDeclarationTests
{
    [Fact]
    public void MethodGroupFactoryIsRecognized()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(Build);
                private Element Build(IUiBuildContext<object> ui) => new VStack().Named("Body");
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("public global::TrueMoon.Argentis.Layout.VStack Body => view.GetRequiredPart<global::TrueMoon.Argentis.Layout.VStack>(\"Body\");", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void DeclareContentWithMethodFactoryIsRecognized()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => DeclareContent<object>(null, Build);
                private Element Build(IUiBuildContext<object> ui) => new VStack().Named("Body");
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("VStack Body", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void CallOfThisViewMethodFromInlineFactoryStepsOneLevel()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body").WithChildren(BuildSection()));

                private Element BuildSection() => new Text().Named("Section");
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("VStack Body", source);
        Assert.Contains("Text Section", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void StepsIntoMethodAreNotFollowedByFurtherSteps()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().WithChildren(BuildSection()));

                private Element BuildSection() => new Text().Named("Section").WithChildren(BuildLeaf());
                private Element BuildLeaf() => new Text().Named("Leaf");
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("Text Section", source);
        Assert.DoesNotContain("Leaf", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void MethodGroupStepsIntoItsOwnCallees()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(Build);

                private Element Build(IUiBuildContext<object> ui) => new VStack().WithChildren(BuildSection());
                private Element BuildSection() => new Text().Named("Section");
            }
            """);
        var source = TestHelper.GeneratedFile(run, "FormView.ViewParts.g.cs")!;
        Assert.Contains("Text Section", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void SetContentInOnContentPreparedIsRecognized()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class LiveView : View
            {
                protected override void OnContentPrepared() => SetContent(Build());

                private Element Build() => new VStack().Named("Live");
            }
            """);
        var source = TestHelper.GeneratedFile(run, "LiveView.ViewParts.g.cs")!;
        Assert.Contains("VStack Live", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void SetContentInlineExpressionIsRecognized()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class InlineView : View
            {
                public InlineView() => SetContent(new HStack().Named("Inline"));
            }
            """);
        var source = TestHelper.GeneratedFile(run, "InlineView.ViewParts.g.cs")!;
        Assert.Contains("HStack Inline", source);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void MethodWithoutNamedPartsEmitsNothingAndNoDiagnostic()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(Build);
                private Element Build(IUiBuildContext<object> ui) => new VStack();
            }
            """);
        Assert.Empty(run.GeneratedTrees);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void FieldFactoryReportsARG022()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using System;
            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                private readonly Func<IUiBuildContext<object>, Element> _factory = ui => new VStack();

                public FormView() => Content(_factory);
            }
            """);
        TestHelper.AssertSingleDiagnostic(run, "ARG022", DiagnosticSeverity.Error,
            "View 'FormView' declares content the generator cannot analyze; use an inline lambda, a method of this view, or a call to a method of this view");
        Assert.Empty(run.GeneratedTrees);
    }

    [Fact]
    public void ForeignTypeFactoryReportsARG022()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            internal sealed class Helper
            {
                public Element Build(IUiBuildContext<object> ui) => new Text().Named("Foreign");
            }

            public sealed class FormView : View
            {
                public FormView() => Content(new Helper().Build(null!));
            }
            """);
        TestHelper.AssertSingleDiagnostic(run, "ARG022", DiagnosticSeverity.Error,
            "View 'FormView' declares content the generator cannot analyze; use an inline lambda, a method of this view, or a call to a method of this view");
        Assert.Empty(run.GeneratedTrees);
    }

    [Fact]
    public void ManualLookupOfGeneratedPartReportsARG023()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body"));

                private Text FindBody() => GetRequiredPart<Text>("Body");
            }
            """);
        TestHelper.AssertDiagnostic(run, "ARG023", DiagnosticSeverity.Warning,
            "View 'FormView' looks up part 'Body' by name; the generated property 'Body' replaces it");
    }

    [Fact]
    public void LookupOfUnknownNameDoesNotReportARG023()
    {
        var run = TestHelper.Verify("""
            namespace App;

            using TrueMoon.Argentis;
            using TrueMoon.Argentis.Layout;
            using TrueMoon.Argentis.Views;

            public sealed class FormView : View
            {
                public FormView() => Content(ui => new VStack().Named("Body"));

                public Text FindOther() => GetRequiredPart<Text>("Other");
            }
            """);
        Assert.Empty(run.Diagnostics);
    }
}
