using System.Runtime.CompilerServices;
using VerifyTests;

namespace TrueMoon.Thorium.Generator.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
    }
}