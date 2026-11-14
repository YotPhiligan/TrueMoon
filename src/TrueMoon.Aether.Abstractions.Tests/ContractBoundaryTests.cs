using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.ManualTests.ContractHost;
using Xunit;

namespace TrueMoon.Aether.Abstractions.Tests;

public sealed class ContractBoundaryTests
{
    [Fact]
    public void EntireAssemblyAndPublicSignatureClosure_ContainOnlyBclAndNeutralContracts()
    {
        var assembly = typeof(IEcsSystem).Assembly;
        Assert.Equal("TrueMoon.Aether.Abstractions", assembly.GetName().Name);
        Assert.All(assembly.GetReferencedAssemblies(), name => Assert.True(Allowed(name.Name!), name.FullName));
        using var stream = File.OpenRead(assembly.Location); using var pe = new PEReader(stream); var metadata = pe.GetMetadataReader();
        Assert.All(metadata.MethodDefinitions, handle => Assert.False((metadata.GetMethodDefinition(handle).Attributes & MethodAttributes.PinvokeImpl) != 0));
        var visited = new HashSet<Type>();
        void Type(Type type)
        {
            if (!visited.Add(type)) return;
            if (type.HasElementType) { Type(type.GetElementType()!); return; }
            if (type.IsGenericParameter) { foreach (var constraint in type.GetGenericParameterConstraints()) Type(constraint); return; }
            Assert.True(type.Assembly == assembly || Allowed(type.Assembly.GetName().Name!), type.FullName);
            foreach (var argument in type.GetGenericArguments()) Type(argument);
        }
        foreach (var type in assembly.GetExportedTypes())
        {
            Type(type); if (type.BaseType != null) Type(type.BaseType); foreach (var contract in type.GetInterfaces()) Type(contract);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                switch (member)
                {
                    case MethodInfo method: Type(method.ReturnType); foreach (var p in method.GetParameters()) Type(p.ParameterType); foreach (var g in method.GetGenericArguments()) Type(g); break;
                    case ConstructorInfo ctor: foreach (var p in ctor.GetParameters()) Type(p.ParameterType); break;
                    case PropertyInfo property: Type(property.PropertyType); break;
                    case FieldInfo field: Type(field.FieldType); break;
                    case EventInfo value: Type(value.EventHandlerType!); break;
                }
        }
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.Name is "World" or "EcsScheduler" or "GameBuildCompiler" or "AetherHost" or "GpuResourceHost");
        Assert.Null(typeof(EcsSystemContext).GetProperty("World"));
        Assert.Null(typeof(EcsCommandWriter).GetMethod("Playback")); Assert.Null(typeof(EcsCommandWriter).GetMethod("Dispose"));
        Assert.True(typeof(EcsSystemContext).IsValueType); Assert.True(typeof(EcsQuery<int>).IsValueType); Assert.True(typeof(EcsCommandWriter).IsValueType);
        Assert.True(visited.Count > 100, "Walk must reach the complete model closure, not one interface.");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentExtension_SharesExactHostContracts_AndFactoryResultHasOneOwner(bool microsoft)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ContractExtension.dll");
        var load = new ContractLoadContext(path);
        try
        {
            var assembly = load.LoadFromAssemblyPath(path); var type = assembly.GetType("TrueMoon.ManualTests.ContractExtension.ExtensionSystem", true)!;
            Assert.Same(load, AssemblyLoadContext.GetLoadContext(assembly));
            Assert.All(assembly.GetReferencedAssemblies(), name => Assert.True(Allowed(name.Name!), name.FullName));
            Assert.Same(typeof(IEcsSystem), type.GetInterfaces().Single(value => value.FullName == typeof(IEcsSystem).FullName));
            var calls = 0;
            await using var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context => context.Services(services =>
                services.Singleton<IFactory<IEcsSystem>>(_ => new Factory<IEcsSystem>(() => { calls++; return (IEcsSystem)Activator.CreateInstance(type)!; })))).Build();
            var factory = app.Services.Resolve<IFactory<IEcsSystem>>()!; Assert.Equal(0, calls);
            await app.StartAsync(); var system = factory.Create()!; Assert.Equal(1, calls);
            var plan = (EcsSystemMetadata)type.GetMethod("Describe")!.Invoke(null, null)!; var access = new ProbeAccess(plan);
            system.Execute(new(access, 1, 7, TimeSpan.FromSeconds(.01), EcsPhase.Gameplay)); access.End();
            Assert.Equal(1, access.QueryCallbacks); Assert.Equal(2, access.Commands); Assert.Equal(1, access.ValueRecorded);
            system.Stop(); system.Dispose(); await app.StopAsync(); await app.DisposeAsync();
            Assert.Equal(1, (int)type.GetProperty("Executions")!.GetValue(system)!);
            Assert.Equal(1, (int)type.GetProperty("Stops")!.GetValue(system)!); Assert.Equal(1, (int)type.GetProperty("Disposals")!.GetValue(system)!);
            Assert.DoesNotContain(load.Assemblies, value => value.GetName().Name is "TrueMoon.Aether.Abstractions" or "TrueMoon.Contracts");
        }
        finally { load.Unload(); }
    }
    [Fact]
    public void PrivateMismatchedSharedCopy_IsRejectedBeforeExtensionLoad()
    {
        var root = Path.Combine(Path.GetTempPath(), "TrueMoon-contract-mismatch-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "ContractExtension.dll"); File.Copy(Path.Combine(AppContext.BaseDirectory, "ContractExtension.dll"), path);
        File.WriteAllText(Path.Combine(root, "TrueMoon.Aether.Abstractions.dll"), "not the shared contract");
        Assert.Throws<InvalidDataException>(() => new ContractLoadContext(path));
    }
    private static bool Allowed(string name) => name == "TrueMoon.Contracts" || name == "TrueMoon.Aether.Abstractions" || name == "netstandard" || name == "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal);
}
