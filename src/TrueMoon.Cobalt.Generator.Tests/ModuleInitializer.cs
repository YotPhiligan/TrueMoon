using System.Runtime.CompilerServices;

namespace TrueMoon.Cobalt.Generator.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}