using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Argentis.Generator.Tests;

public static partial class TestHelper
{
    /// <summary>Asserts exactly one diagnostic with this id, severity and full message.</summary>
    public static void AssertDiagnostic(GeneratorDriverRunResult run, string id, DiagnosticSeverity severity, string message)
    {
        var diagnostic = Assert.Single(run.Diagnostics, candidate => candidate.Id == id);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.Equal(message, diagnostic.GetMessage());
    }

    /// <summary>Asserts the only diagnostic reported by the run.</summary>
    public static void AssertSingleDiagnostic(GeneratorDriverRunResult run, string id, DiagnosticSeverity severity, string message)
    {
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.Equal(message, diagnostic.GetMessage());
    }

    /// <summary>Asserts the compiled output (generators included) has no errors.</summary>
    public static void AssertCompiles(Compilation output) =>
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
}
