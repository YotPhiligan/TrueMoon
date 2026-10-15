[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageRunDirectory, [string]$LegacyGeneratorPackage)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageRun = [IO.Path]::GetFullPath($PackageRunDirectory)
$allowed = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/CorePackages')) + [IO.Path]::DirectorySeparatorChar
if (!$packageRun.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a completed package run inside TestResults/CorePackages.' }
$packageSummary = Get-Content (Join-Path $packageRun 'summary.json') -Raw | ConvertFrom-Json
if (!$packageSummary.Completed) { throw 'The base package matrix must have completed.' }
$run = Join-Path $packageRun ('delivery-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
[IO.Directory]::CreateDirectory($run) | Out-Null
$version = $packageSummary.Version
$feed = [Security.SecurityElement]::Escape((Join-Path $packageRun 'feed'))
$cache = Join-Path $run 'packages'
$externalCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$fallback = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($externalCache))
$legacySource = ''; $legacyMapping = ''; $legacyVersion = $null
if ($LegacyGeneratorPackage) {
    $legacyPath = [IO.Path]::GetFullPath($LegacyGeneratorPackage)
    $archive = [IO.Compression.ZipFile]::OpenRead($legacyPath)
    try {
        $entry = @($archive.Entries | Where-Object FullName -like '*.nuspec')[0]
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$legacyNuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $legacyNuspec.SelectSingleNode('//*[local-name()="metadata"]')
        if ($metadata.id -ne 'TrueMoon.Cobalt.Generator') { throw 'Expected a legacy standalone Cobalt generator package.' }
        $legacyVersion = $metadata.version
    } finally { $archive.Dispose() }
    $legacyFeed = [Security.SecurityElement]::Escape((Split-Path $legacyPath))
    $legacySource = "<add key=`"legacy`" value=`"$legacyFeed`" />"
    $legacyMapping = '<packageSource key="legacy"><package pattern="TrueMoon.Cobalt.Generator" /></packageSource>'
}
@"
<configuration>
  <packageSources><clear /><add key="current" value="$feed" />$legacySource<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="current"><package pattern="TrueMoon" /><package pattern="TrueMoon.*" /></packageSource>$legacyMapping<packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
  <fallbackPackageFolders><clear /><add key="existing-third-party-cache" value="$fallback" /></fallbackPackageFolders>
</configuration>
"@ | Set-Content (Join-Path $run 'NuGet.Config') -Encoding utf8
foreach ($name in @('Directory.Build.props','Directory.Packages.props')) { Copy-Item (Join-Path $PSScriptRoot $name) $run }
@'
<Project>
  <Target Name="CaptureDeliveryAnalyzers" AfterTargets="CoreCompile">
    <WriteLinesToFile File="$(IntermediateOutputPath)resolved-analyzers.txt" Lines="@(Analyzer)" Overwrite="true" />
  </Target>
</Project>
'@ | Set-Content (Join-Path $run 'Directory.Build.targets') -Encoding utf8
$summary = [ordered]@{Completed=$false;Version=$version;RunDirectory=$run;ScriptSha256=(Get-FileHash $PSCommandPath).Hash;Commands=@();Generation=@();Cases=@();NegativeControls=@();LegacyVersion=$legacyVersion;Failure=$null}
function DotNet([string]$Name, [string[]]$Arguments, [string[]]$ExpectedDiagnostics = @()) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $run; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info; $started = $false
    try {
        $started = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(120000)) { throw "$Name timed out." }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $run "$Name.log"), $output)
        $summary.Commands += @{Name=$Name;Arguments=$Arguments;ExitCode=$process.ExitCode}
        if ($ExpectedDiagnostics.Count) {
            if ($process.ExitCode -eq 0) { throw "$Name must fail with constructor diagnostics." }
            $actual = @([regex]::Matches($output, 'error (COB\d+|CS\d+|MSB\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            if (Compare-Object $ExpectedDiagnostics $actual) { throw "$Name produced unexpected errors: $($actual -join ', ')." }
            foreach ($id in $ExpectedDiagnostics) {
                if ($output -notmatch ('Invalid.cs\(\d+,\d+\): error ' + $id + ':')) { throw "$id must point to the registration source." }
            }
            $summary.NegativeControls += @{Name=$Name;Diagnostics=$actual;ExitCode=$process.ExitCode}
        } elseif ($process.ExitCode -ne 0) { throw "$Name failed; see $run/$Name.log" }
    } finally {
        if ($started -and !$process.HasExited) { $process.Kill($true); if (!$process.WaitForExit(5000)) { throw 'Owned process was not reaped.' } }
        $process.Dispose()
    }
}
function Verify-Analyzer([string]$ProjectDirectory, [string]$Configuration) {
    $analyzers = @(Get-Content (Join-Path $ProjectDirectory "obj/$Configuration/net10.0/resolved-analyzers.txt") | Where-Object {$_ -match 'TrueMoon.Cobalt.Generator.dll$'})
    $expected = Join-Path $cache ("truemoon.cobalt/$version/analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll".ToLowerInvariant())
    if ($analyzers.Count -ne 1 -or !$analyzers[0].Equals($expected, [StringComparison]::OrdinalIgnoreCase)) { throw 'The compiler must receive exactly the bundled matching analyzer.' }
    if ((Get-FileHash $expected).Hash -ne $packageSummary.GeneratorSha256) { throw 'Bundled analyzer SHA mismatch.' }
    $summary.Generation += @{Project=$ProjectDirectory;Configuration=$Configuration;Analyzer=$analyzers[0];Sha256=(Get-FileHash $expected).Hash}
}
try {
    $library = Join-Path $run 'Library'; $app = Join-Path $run 'App'
    foreach ($path in @($library,$app)) { [IO.Directory]::CreateDirectory($path) | Out-Null }
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="TrueMoon.Cobalt" Version="$version" /></ItemGroup></Project>
"@ | Set-Content (Join-Path $library 'Library.csproj') -Encoding utf8
    @'
using TrueMoon.Services;
namespace LibraryFixture;
public interface IProbe { int Value { get; } bool HasDependency { get; } }
public interface IProbe<T> : IProbe;
public class Dependency;
internal sealed class Probe : IProbe {
    public int Value { get; }
    public bool HasDependency { get; }
    public Probe() { Value = -1; }
    public Probe(Dependency? dependency, int value = 42) { Value = value; HasDependency = dependency != null; }
}
internal sealed class Probe<T> : IProbe<T> {
    public int Value { get; }
    public bool HasDependency { get; }
    public Probe() { Value = -1; }
    public Probe(Dependency? dependency, int value = 43) { Value = value; HasDependency = dependency != null; }
}
public static class Module {
    public static IServicesRegistrationContext Register(IServicesRegistrationContext services) => services.TrySingleton<IProbe, Probe>()
        .Singleton(typeof(IProbe<>), typeof(Probe<>));
}
'@ | Set-Content (Join-Path $library 'Module.cs') -Encoding utf8
    @'
using TrueMoon.Cobalt;
using TrueMoon.Services;
namespace LibraryFixture;
public static class ErrorChecks {
    public static void VerifyRuntime() {
        var cycle = true; var loopCalls = 0; var nullCalls = 0;
        var owned = new Owned(); var borrowed = new Borrowed(); var state = new FailureState();
        using (var ownership = (IDisposable)new CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
            .Instance(borrowed).Instance(state).Singleton<Owned>(_ => owned)
            .Singleton<Loop>(r => {
                if (++loopCalls > 8) throw new Exception("Bounded package recursion guard.");
                r.Resolve<Owned>(); r.Resolve<Borrowed>();
                return cycle ? r.Resolve<Loop>() : new Loop();
            })
            .Singleton<NullProbe>(_ => ++nullCalls == 1 ? null! : new NullProbe())
            .Transient<NeedsMissing>().Transient<ConstructorFailure<int>>()
            .Singleton(typeof(ConstructorFailure<>), typeof(ConstructorFailure<>))])) {
            var resolver = (IServiceResolver)ownership;
            var circular = Error(() => resolver.Resolve<Loop>());
            Path(circular, typeof(Loop), typeof(Loop));
            if (circular is not ServiceResolvingException { Failure: ServiceResolutionFailure.CircularDependency } || loopCalls != 1)
                throw new Exception("Cycle classification failed.");
            cycle = false;
            var recovered = resolver.Resolve<Loop>();
            if (!ReferenceEquals(recovered, resolver.Resolve<Loop>()) || loopCalls != 2) throw new Exception("Cycle retry failed.");
            var missing = Error(() => resolver.Resolve<NeedsMissing>());
            Path(missing, typeof(NeedsMissing), typeof(Missing));
            if (missing is not ServiceResolvingException { Failure: ServiceResolutionFailure.MissingService, ServiceType: var missingType } || missingType != typeof(Missing))
                throw new Exception("Missing service classification failed.");
            var absent = Error(() => resolver.Resolve<NullProbe>());
            Path(absent, typeof(NullProbe));
            var value = resolver.Resolve<NullProbe>();
            if (!ReferenceEquals(value, resolver.Resolve<NullProbe>()) || nullCalls != 2) throw new Exception("Null singleton retry failed.");
            foreach (var type in new[] { typeof(ConstructorFailure<int>), typeof(ConstructorFailure<string>) }) {
                var failure = Error(() => resolver.GetService(type));
                if (!ReferenceEquals(state.Failure, failure) || !failure.StackTrace!.Contains(nameof(FailureState.Fail)))
                    throw new Exception("Original constructor failure/stack was lost.");
                Path(failure, type);
            }
            if (!ReferenceEquals(recovered, resolver.Resolve<Loop>()) || owned.Disposals != 0) throw new Exception("Failed construction corrupted the graph.");
        }
        if (owned.Disposals != 1 || borrowed.Disposals != 0) throw new Exception("Failure ownership cleanup failed.");
    }
    private static Exception Error(Action action) {
        try { action(); } catch (Exception error) { return error; }
        throw new Exception("Expected resolution error.");
    }
    private static void Path(Exception error, params Type[] expected) {
        if (!ServiceResolvingException.GetDependencyPath(error).SequenceEqual(expected)) throw new Exception("Incorrect dependency path.");
    }
    internal sealed class Loop;
    internal sealed class NullProbe;
    internal sealed class Missing;
    internal sealed class NeedsMissing(Missing dependency);
    internal class Owned : IDisposable { public int Disposals; public void Dispose() => Disposals++; }
    internal sealed class Borrowed : Owned;
    internal sealed class FailureState {
        public Exception Failure { get; } = new InvalidOperationException("constructor failure");
        public void Fail() => throw Failure;
    }
    internal sealed class ConstructorFailure<T> { public ConstructorFailure(FailureState state) => state.Fail(); }
}
'@ | Set-Content (Join-Path $library 'ErrorChecks.cs') -Encoding utf8
    @'
using TrueMoon.Cobalt;
using TrueMoon.Services;
namespace LibraryFixture;
public static class RegistrationChecks {
    public static void VerifyRuntime() {
        var context = new CobaltServicesRegistrationContext();
        var borrowed = new First();
        context.Instance<IMarker>(borrowed);
        var snapshot = context.GetContainer().GetHandles();
        try { ((IList<ServiceRegistrationHandle>)snapshot).Clear(); throw new Exception("Mutable snapshot collection."); }
        catch (NotSupportedException) { }
        snapshot[0].ServiceType = typeof(Unrelated); snapshot[0].ImplementationType = typeof(Unrelated);
        snapshot[0].Instance = null; snapshot[0].Resolver = null;
        context.TryTransient<IMarker, First>();
        if (!context.Exist<IMarker, First>() || context.GetContainer().GetHandles().Count != 1 ||
            !ReferenceEquals(borrowed, context.GetContainer().GetHandles()[0].Instance)) throw new Exception("Instance pair/snapshot isolation failed.");
        Type service = typeof(IMarker); Type implementation = typeof(Unrelated);
        try { context.Singleton(service, implementation); throw new Exception("Invalid mapping accepted."); }
        catch (ArgumentException error) { if (error.ParamName != "implementation") throw; }
        if (context.GetContainer().GetHandles().Count != 1) throw new Exception("Invalid registration mutated context.");
        context.Remove<IMarker, First>();
        if (context.Exist<IMarker>()) throw new Exception("Known instance removal failed.");
        CobaltServicesRegistrationContext source = null!;
        using var owner = (IDisposable)new CobaltServiceResolverBuilder().Build(null!, [(_, c) => {
            source = (CobaltServicesRegistrationContext)c;
            c.Singleton<First>().TryTransient<First, First>()
                .Singleton(typeof(IBox<>), typeof(Box<>)).Singleton<IBox<int>, IntBox>()
                .Transient(typeof(IBox<>), typeof(OtherBox<>));
        }]);
        var resolver = (IServiceResolver)owner;
        var self = resolver.Resolve<IEnumerable<First>>().ToArray();
        if (self.Length != 1 || !ReferenceEquals(self[0], resolver.Resolve<First>())) throw new Exception("Self/Try duplicate contract failed.");
        var items = resolver.Resolve<IEnumerable<IBox<int>>>().ToArray();
        var repeated = resolver.Resolve<IEnumerable<IBox<int>>>().ToArray();
        if (!items.Select(x => x.GetType()).SequenceEqual(new[] { typeof(Box<int>), typeof(IntBox), typeof(OtherBox<int>) }) ||
            !ReferenceEquals(items[0], repeated[0]) || !ReferenceEquals(items[1], repeated[1]) || ReferenceEquals(items[2], repeated[2]) ||
            !ReferenceEquals(items[1], resolver.Resolve<IBox<int>>())) throw new Exception("Mixed generic order/lifetime/closed precedence failed.");
        source.RemoveAll<IBox<int>>();
        if (!ReferenceEquals(items[1], resolver.Resolve<IBox<int>>())) throw new Exception("Built graph changed with registration context.");
    }
    internal interface IMarker;
    internal sealed class First : IMarker;
    internal sealed class Unrelated;
    internal interface IBox<T>;
    internal sealed class Box<T> : IBox<T>;
    internal sealed class OtherBox<T> : IBox<T>;
    internal sealed class IntBox : IBox<int>;
}
'@ | Set-Content (Join-Path $library 'RegistrationChecks.cs') -Encoding utf8
    @'
using TrueMoon.Cobalt;
using TrueMoon.Services;
using Helpers = TrueMoon.Services.ServicesRegistrationContextExtensions;
namespace LibraryFixture;
public static class WorkflowChecks {
    public static void VerifyRuntime() {
        foreach (var enabled in new[] { false, true }) {
            using var owner = (IDisposable)new CobaltServiceResolverBuilder().Build(null!, [(_, c) => {
                IServicesRegistrationContext? optional = enabled ? c : null;
                Register(optional); Register(optional);
            }]);
            var resolver = (IServiceResolver)owner;
            var probes = resolver.Resolve<IEnumerable<IWorkflow>>().ToArray();
            if (!enabled) {
                if (probes.Length != 0 || resolver.TryResolve<IWorkflow>() != null || resolver.TryResolve<IBox<int>>() != null ||
                    resolver.TryResolve<Dependency>() != null || resolver.TryResolve<Both>() != null)
                    throw new Exception("Skipped module registered services.");
                continue;
            }
            var probe = resolver.Resolve<IWorkflow>();
            if (probes.Length != 1 || probe is not Workflow || !ReferenceEquals(probe, probes[0]) ||
                !ReferenceEquals(probe, resolver.Resolve<IWorkflow>()) || resolver.TryResolve<Workflow>() != null ||
                resolver.Resolve<IEnumerable<Dependency>>().Count() != 1) throw new Exception("Repeated Try module contract failed.");
            var box = resolver.Resolve<IBox<int>>();
            if (box is not Box<int> || ReferenceEquals(box, resolver.Resolve<IBox<int>>()) ||
                resolver.Resolve<IEnumerable<IBox<int>>>().Count() != 2 || resolver.Resolve<IBox<string>>() is not Box<string>)
                throw new Exception("Conditional named open mapping failed.");
            var both = resolver.Resolve<Both>();
            if (!ReferenceEquals(both, resolver.Resolve<IFirst>()) || !ReferenceEquals(both, resolver.Resolve<ISecond>()))
                throw new Exception("Conditional Composite module alias failed.");
        }
    }
    private static void Register(IServicesRegistrationContext? c) {
        c?.TrySingleton<IWorkflow, Workflow>()?.TrySingleton<Dependency>();
        c?.Transient(implementation: (Type)(typeof(Box<>)), service: (typeof(IBox<>)));
        if (c != null) Helpers.TrySingleton<Dependency>(context: c);
        if (c != null && !c.Exist<Both>()) c?.Composite<Both, IFirst, ISecond>();
    }
    internal interface IWorkflow;
    internal sealed class Workflow : IWorkflow;
    internal interface IBox<T>;
    internal sealed class Box<T> : IBox<T>;
    internal interface IFirst;
    internal interface ISecond;
    internal sealed class Both : IFirst, ISecond;
}
'@ | Set-Content (Join-Path $library 'WorkflowChecks.cs') -Encoding utf8
    @'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="../Library/Library.csproj" /></ItemGroup></Project>
'@ | Set-Content (Join-Path $app 'App.csproj') -Encoding utf8
    @'
using LibraryFixture;
using TrueMoon.Cobalt;
using TrueMoon.Services;
foreach (var registered in new[] { false, true }) {
    using var ownership = (IDisposable)new CobaltServiceResolverBuilder().Build(null!, [(_, services) => {
        if (registered) services.Instance<int>(0).Instance<Dependency>(new Dependency());
        Module.Register(services);
    }]);
    var resolver = (IServiceResolver)ownership;
    var probe = resolver.Resolve<IProbe>();
    var generic = resolver.Resolve<IProbe<int>>();
    if (probe.Value != (registered ? 0 : 42) || generic.Value != (registered ? 0 : 43) ||
        probe.HasDependency != registered || generic.HasDependency != registered ||
        !ReferenceEquals(probe, resolver.Resolve<IProbe>()) || !ReferenceEquals(generic, resolver.Resolve<IProbe<int>>()) ||
        ReferenceEquals(generic, resolver.Resolve<IProbe<string>>())) throw new Exception("Library constructors/optional parameters/lifetime failed.");
}
ErrorChecks.VerifyRuntime();
RegistrationChecks.VerifyRuntime();
WorkflowChecks.VerifyRuntime();
Console.WriteLine("LIBRARY_APP_OK");
'@ | Set-Content (Join-Path $app 'Program.cs') -Encoding utf8
    DotNet 'restore-library-app' @('restore',(Join-Path $app 'App.csproj'),'--configfile',(Join-Path $run 'NuGet.Config'),'--packages',$cache,'-v','quiet')
    foreach ($configuration in @('Debug','Release')) {
        DotNet "build-library-app-$configuration" @('build',(Join-Path $app 'App.csproj'),'--configuration',$configuration,'--no-restore','-v','quiet')
        Verify-Analyzer $library $configuration; Verify-Analyzer $app $configuration
        $publish = Join-Path $run "publish/$configuration"
        DotNet "publish-library-app-$configuration" @('publish',(Join-Path $app 'App.csproj'),'--configuration',$configuration,'--no-restore','--output',$publish,'--self-contained','false','-v','quiet')
        foreach ($delivery in @(@{Name='build';Path=Join-Path $app "bin/$configuration/net10.0"},@{Name='publish';Path=$publish})) {
            if (@(Get-ChildItem $delivery.Path -File | Where-Object Name -match '^(TrueMoon.Cobalt.Generator|Microsoft.CodeAnalysis)').Count) { throw 'Build-only DLL leaked into app output.' }
            $name = "run-library-app-$configuration-$($delivery.Name)"
            DotNet $name @((Join-Path $delivery.Path 'App.dll'))
            if ((Get-Content (Join-Path $run "$name.log") -Raw).Trim() -ne 'LIBRARY_APP_OK') { throw 'Library/app execution did not prove its contract.' }
            $summary.Cases += $name
        }
    }
    $invalid = Join-Path $run 'Invalid'; [IO.Directory]::CreateDirectory($invalid) | Out-Null
    Copy-Item (Join-Path $library 'Library.csproj') (Join-Path $invalid 'Invalid.csproj')
    @'
using TrueMoon.Services;
public abstract class Abstract;
public class NoConstructor { private NoConstructor() {} }
public class Ambiguous { public Ambiguous(string value) {} public Ambiguous(int value) {} }
public class ByRef { public ByRef(ref int value) {} }
public class Required { public required string Name { get; init; } }
public interface IService<T>;
public class WrongArity<T, U> : IService<T>;
public static class Module {
    public static void Register(IServicesRegistrationContext context) {
        context.Singleton<Abstract>();
        context.TrySingleton<NoConstructor>();
        context.Transient<Ambiguous>();
        context.Singleton<ByRef>();
        context.Singleton<Required>();
        context.Singleton(typeof(IService<>), typeof(WrongArity<,>));
    }
}
'@ | Set-Content (Join-Path $invalid 'Invalid.cs') -Encoding utf8
    DotNet 'restore-invalid' @('restore',(Join-Path $invalid 'Invalid.csproj'),'--configfile',(Join-Path $run 'NuGet.Config'),'--packages',$cache,'-v','quiet')
    foreach ($configuration in @('Debug','Release')) {
        DotNet "build-invalid-$configuration" @('build',(Join-Path $invalid 'Invalid.csproj'),'--configuration',$configuration,'--no-restore','-v','quiet') @('COB001','COB002','COB003','COB004','COB005','COB006')
    }
    if ($legacyVersion) {
        $legacy = Join-Path $run 'Legacy'; [IO.Directory]::CreateDirectory($legacy) | Out-Null
        Copy-Item (Join-Path $PSScriptRoot 'DirectCobalt/*.cs') $legacy
        @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>DirectCobaltConsumer</AssemblyName><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="TrueMoon.Cobalt" Version="$version" /><PackageReference Include="TrueMoon.Cobalt.Generator" Version="$legacyVersion" PrivateAssets="all" /></ItemGroup></Project>
"@ | Set-Content (Join-Path $legacy 'Legacy.csproj') -Encoding utf8
        DotNet 'restore-legacy' @('restore',(Join-Path $legacy 'Legacy.csproj'),'--configfile',(Join-Path $run 'NuGet.Config'),'--packages',$cache,'-v','quiet')
        DotNet 'build-legacy' @('build',(Join-Path $legacy 'Legacy.csproj'),'--configuration','Release','--no-restore','-v','quiet')
        Verify-Analyzer $legacy 'Release'
        $report = Join-Path $run 'legacy-result.json'
        DotNet 'run-legacy' @((Join-Path $legacy 'bin/Release/net10.0/DirectCobaltConsumer.dll'),$report)
        if (!(Get-Content $report -Raw | ConvertFrom-Json).Completed) { throw 'Legacy reference consumer failed.' }
        $summary.Cases += 'run-legacy'
    }
    $summary.Completed = $true
    Write-Host "Delivery checks passed: $($summary.Cases.Count) processes + $($summary.NegativeControls.Count) constructor diagnostic controls. $run"
} catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'summary.json') -Encoding utf8 }
