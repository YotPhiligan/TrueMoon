#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageRunDirectory)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageRun=[IO.Path]::GetFullPath($PackageRunDirectory)
$base=Get-Content (Join-Path $packageRun 'summary.json') -Raw | ConvertFrom-Json
if (!$base.Completed) {throw 'Use a completed Core package run.'}
$run=Join-Path $packageRun ('comparison-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
[void][IO.Directory]::CreateDirectory($run)
$cache=Join-Path $run 'packages'
$feed=[Security.SecurityElement]::Escape((Join-Path $packageRun 'feed'))
$version=$base.Version
$summary=[ordered]@{Completed=$false;Version=$version;Commands=@();Cases=@();Generation=@();Sources=@();ScriptSha256=(Get-FileHash $PSCommandPath).Hash}
@"
<configuration><packageSources><clear/><add key="current" value="$feed"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><packageSource key="current"><package pattern="TrueMoon"/><package pattern="TrueMoon.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>
"@ | Set-Content (Join-Path $run 'NuGet.Config') -Encoding utf8
@'
<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally><IsPackable>false</IsPackable></PropertyGroup></Project>
'@ | Set-Content (Join-Path $run 'Directory.Build.props') -Encoding utf8
'<Project />' | Set-Content (Join-Path $run 'Directory.Packages.props') -Encoding utf8
@'
<Project><Target Name="CaptureComparisonAnalyzers" AfterTargets="CoreCompile"><WriteLinesToFile File="$(IntermediateOutputPath)analyzers.txt" Lines="@(Analyzer)" Overwrite="true"/></Target></Project>
'@ | Set-Content (Join-Path $run 'Directory.Build.targets') -Encoding utf8
function DotNet([string]$name,[string[]]$commandArguments) {
    $start=[Diagnostics.ProcessStartInfo]::new('dotnet'); $start.WorkingDirectory=$run
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach ($argument in $commandArguments) {$start.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(120000)) {throw "Timed out: $name"}
        $output=$stdout.Result+$stderr.Result
        $output | Set-Content (Join-Path $run "$name.log") -Encoding utf8
        $summary.Commands+=@{Name=$name;Arguments=$commandArguments;ExitCode=$process.ExitCode}
        if ($process.ExitCode -ne 0) {throw "Failed $name; see $run/$name.log"}
        Write-Host "$name passed"
        return $stdout.Result
    } finally {if (!$process.HasExited) {$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
try {
    foreach ($mode in @('generated','microsoft-no-analyzer')) {
        $directory=Join-Path $run $mode;[void][IO.Directory]::CreateDirectory($directory)
        foreach ($name in @('Scenario.cs','ProviderSelection.cs','WorkloadVerification.cs','ComparisonVerification.cs')) {
            $source=Join-Path $PSScriptRoot $name; $target=Join-Path $directory $name
            Copy-Item -LiteralPath $source -Destination $target
            $summary.Sources+=@{Source=[IO.Path]::GetRelativePath($workspace,$source);Target=[IO.Path]::GetRelativePath($run,$target);Sha256=(Get-FileHash $source).Hash}
        }
        $enable=if ($mode -eq 'generated') {'true'} else {'false'}
        @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableCobaltGenerator>$enable</EnableCobaltGenerator><EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles></PropertyGroup><ItemGroup><PackageReference Include="TrueMoon" Version="$version"/><PackageReference Include="TrueMoon.Extensions.DependencyInjection" Version="$version"/></ItemGroup></Project>
"@ | Set-Content (Join-Path $directory 'Consumer.csproj') -Encoding utf8
        @'
using System.Text.Json;
using TrueMoon.Cobalt.Benchmarks;
if (args is not [var provider]) return 2;
Console.WriteLine(JsonSerializer.Serialize(await ComparisonVerification.VerifyAsync(ProviderSelection.Parse(provider))));
return 0;
'@ | Set-Content (Join-Path $directory 'Program.cs') -Encoding utf8
        $project=Join-Path $directory 'Consumer.csproj'
        [void](DotNet "restore-$mode" @('restore',$project,'--packages',$cache,'--configfile',(Join-Path $run 'NuGet.Config'),'-v','quiet'))
        foreach ($configuration in @('Debug','Release')) {
            [void](DotNet "build-$mode-$configuration" @('build',$project,'-c',$configuration,'--no-restore','-v','quiet'))
            $analyzers=@(Get-Content (Join-Path $directory "obj/$configuration/net10.0/analyzers.txt") | Where-Object { [IO.Path]::GetFileName($_) -eq 'TrueMoon.Cobalt.Generator.dll' })
            if ($analyzers.Count -ne $(if ($mode -eq 'generated') {1} else {0})) {throw 'Unexpected analyzer delivery.'}
            if ($analyzers.Count -and ((Get-FileHash $analyzers[0]).Hash -ne $base.GeneratorSha256 -or !$analyzers[0].StartsWith($cache,[StringComparison]::OrdinalIgnoreCase))) {throw 'Analyzer must match fresh cached package.'}
            $generated=@(Get-ChildItem (Join-Path $directory "obj/$configuration/net10.0") -Recurse -Filter '*CobaltResolversRegistration.g.cs')
            if (($mode -eq 'generated' -and $generated.Count -ne 1) -or ($mode -ne 'generated' -and $generated.Count)) {throw 'Generated module initializer mismatch.'}
            $summary.Generation+=@{Mode=$mode;Configuration=$configuration;Analyzers=$analyzers;GeneratedModules=$generated.Count}
            $publish=Join-Path $directory "publish/$configuration"
            [void](DotNet "publish-$mode-$configuration" @('publish',$project,'-c',$configuration,'--no-restore','--output',$publish,'--self-contained','false','-v','quiet'))
            foreach ($delivery in @(@{Name='build';Path=Join-Path $directory "bin/$configuration/net10.0"},@{Name='publish';Path=$publish})) {
                if (@(Get-ChildItem $delivery.Path -File | Where-Object Name -match '^(TrueMoon.Cobalt.Generator|Microsoft.CodeAnalysis|BenchmarkDotNet)').Count) {throw 'Build/benchmark DLL leaked into runtime output.'}
                $providers=if ($mode -eq 'generated') {@('cobalt','microsoft')} else {@('microsoft')}
                foreach ($provider in $providers) {
                    $name="run-$mode-$configuration-$($delivery.Name)-$provider"
                    $report=(DotNet $name @((Join-Path $delivery.Path 'Consumer.dll'),$provider)) | ConvertFrom-Json
                    if (!$report.Completed -or $report.Cases.Count -ne 10 -or $report.Provider -ne $provider) {throw 'Comparison verification incomplete.'}
                    $summary.Cases+=@{Name=$name;Report=$report}
                }
            }
        }
    }
    foreach ($source in $summary.Sources) {
        if ((Get-FileHash (Join-Path $workspace $source.Source)).Hash -ne $source.Sha256 -or (Get-FileHash (Join-Path $run $source.Target)).Hash -ne $source.Sha256) {throw 'Consumer verification sources changed.'}
    }
    if ($summary.Cases.Count -ne 12 -or $summary.Generation.Count -ne 4) {throw 'Incomplete consumer matrix.'}
    $summary.Completed=$true
    Write-Host "Comparison delivery passed: 12 processes / 120 behavior scenarios. $run"
}
catch {$summary.Failure=$_.ToString();throw}
finally {$summary | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $run 'summary.json') -Encoding utf8}
