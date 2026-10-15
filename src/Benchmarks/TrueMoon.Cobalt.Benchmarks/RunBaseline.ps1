#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RunDirectory,
    [ValidateRange(3, 100)][int]$ColdSamples = 12,
    [switch]$CompareProviders
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$RunDirectory) { $RunDirectory = Join-Path $workspace ('TestResults/CobaltBaseline/run-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff')) }
$run = [IO.Path]::GetFullPath($RunDirectory)
if (Test-Path -LiteralPath $run) { throw "Use a fresh run directory: $run" }
[void][IO.Directory]::CreateDirectory($run)
$project = Join-Path $PSScriptRoot 'TrueMoon.Cobalt.Benchmarks.csproj'
$dll = Join-Path $PSScriptRoot 'bin/Release/net10.0/TrueMoon.Cobalt.Benchmarks.dll'
$summary = [ordered]@{ Completed=$false; Graph='cobalt-app-v1'; StartedUtc=[DateTime]::UtcNow.ToString('O'); Commands=@(); Results=@(); ColdSamples=@() }
$summary.Mode = if ($CompareProviders) { 'ProviderComparison' } else { 'CobaltBaseline' }
$expectedCaseCount = if ($CompareProviders) { 20 } else { 10 }

function Invoke-DotNet([string]$name, [string[]]$commandArguments, [int]$timeoutSeconds = 900) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $PSScriptRoot
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $commandArguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($timeoutSeconds * 1000)) {
            $process.Kill($true); $process.WaitForExit()
            throw "Timed out: $name (owned process tree terminated)."
        }
        $stdout.Result | Set-Content -LiteralPath (Join-Path $run "$name.log") -Encoding utf8
        $stderr.Result | Set-Content -LiteralPath (Join-Path $run "$name.stderr.log") -Encoding utf8
        $summary.Commands += @{Name=$name;Arguments=$commandArguments;ExitCode=$process.ExitCode}
        if ($process.ExitCode -ne 0) { throw "Failed $name (exit $($process.ExitCode)); see $run/$name.log" }
        Write-Host "$name passed"
        return $stdout.Result
    }
    finally { $process.Dispose() }
}

function Read-Reports([string]$directory) {
    $files = @(Get-ChildItem -LiteralPath $directory -Recurse -Filter '*-report-full.json')
    if (!$files.Count) { throw "Missing BDN reports: $directory" }
    foreach ($file in $files) {
        $report = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        foreach ($benchmark in $report.Benchmarks) {
            if (!$benchmark.Statistics -or $benchmark.Statistics.Mean -le 0 -or !$benchmark.Memory) { throw "Incomplete benchmark: $($benchmark.FullName)" }
            $benchmark
        }
    }
}

try {
    $summary.GitHead = (& git -C $workspace rev-parse HEAD).Trim()
    $summary.PowerPlanBefore = (& powercfg /getactivescheme | Out-String).Trim()
    $summary.Environment = @{}
    foreach ($key in @('DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_gcServer','COMPlus_TieredCompilation','COMPlus_TieredPGO','COMPlus_gcServer')) {
        $summary.Environment[$key] = [Environment]::GetEnvironmentVariable($key)
    }
    $summary.Sdk = (Invoke-DotNet 'sdk' @('--version')).Trim()
    [void](Invoke-DotNet 'build-release' @('build',$project,'--configuration','Release','-v','quiet'))
    $inputs = @(
        foreach ($directory in @('TrueMoon','TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator','TrueMoon.Extensions.DependencyInjection','Benchmarks/TrueMoon.Cobalt.Benchmarks')) {
            Get-ChildItem -LiteralPath (Join-Path $workspace $directory) -Recurse -File |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.props','.targets','.ps1','.md') }
        }
        foreach ($file in @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','TrueMoon.slnx')) { Get-Item -LiteralPath (Join-Path $workspace $file) }
    )
    $summary.SourceFiles = @($inputs | Sort-Object FullName -Unique | ForEach-Object { @{Path=[IO.Path]::GetRelativePath($workspace,$_.FullName);Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    $summary.Binaries = @('TrueMoon.Cobalt.Benchmarks.dll','TrueMoon.Cobalt.dll','TrueMoon.dll','TrueMoon.Contracts.dll','BenchmarkDotNet.dll','TrueMoon.Extensions.DependencyInjection.dll','Microsoft.Extensions.DependencyInjection.dll') | ForEach-Object {
        $path = Join-Path (Split-Path $dll) $_
        @{Name=$_;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    $verified = Invoke-DotNet 'verify-workload' @($dll,'--verify')
    if ($verified.Trim() -ne 'COBALT_BASELINE_VERIFIED') { throw 'Workload verification incomplete.' }
    if ($CompareProviders) {
        $summary.Verification = @()
        foreach ($provider in @('cobalt','microsoft')) {
            $report = (Invoke-DotNet "verify-$provider" @($dll,'--verify-provider',$provider)) | ConvertFrom-Json
            if (!$report.Completed -or $report.Cases.Count -ne 10) { throw "Provider verification incomplete: $provider" }
            $summary.Verification += $report
        }
    }
    $dryPatterns = if ($CompareProviders) { @('*ProviderComparisonBenchmarks*') } else { @('*ResolutionBenchmarks*','*ResolverLifecycleBenchmarks*','*AppLifecycleBenchmarks*') }
    [void](Invoke-DotNet 'dry' (@($dll,'--filter') + $dryPatterns + @('--job','Dry','--noOverwrite','--artifacts',(Join-Path $run 'dry'))))
    $dry = @(Read-Reports (Join-Path $run 'dry'))
    if ($dry.Count -ne $expectedCaseCount) { throw "Expected $expectedCaseCount Dry cases, found $($dry.Count)." }
    $summary.DryCases = $dry.Count

    # Validate one representative case with the final job before running the remainder.
    $groups = @(
        @{Name='singleton';Patterns=@('*ResolutionBenchmarks.Singleton')},
        @{Name='resolution';Patterns=@('*ResolutionBenchmarks.TransientRequest','*ResolutionBenchmarks.OpenGenericSingleton','*ResolutionBenchmarks.ClosedGenericSingleton','*ResolutionBenchmarks.MaterializeMultiplePlugins')},
        @{Name='resolver-lifecycle';Patterns=@('*ResolverLifecycleBenchmarks*')},
        @{Name='app-lifecycle';Patterns=@('*AppLifecycleBenchmarks*')}
    )
    if ($CompareProviders) {
        $groups = @(
            @{Name='singleton';Patterns=@('*ProviderComparisonBenchmarks.*Singleton')},
            @{Name='resolution';Patterns=@('*ProviderComparisonBenchmarks.*Transient','*ProviderComparisonBenchmarks.*OpenGeneric','*ProviderComparisonBenchmarks.*ClosedGeneric','*ProviderComparisonBenchmarks.*Multiple')},
            @{Name='resolver-lifecycle';Patterns=@('*ProviderComparisonBenchmarks.*EmptyBuild','*ProviderComparisonBenchmarks.*GraphBuild','*ProviderComparisonBenchmarks.*FirstGraph')},
            @{Name='app-lifecycle';Patterns=@('*ProviderComparisonBenchmarks.*AppBuild','*ProviderComparisonBenchmarks.*AppLifecycle')}
        )
    }
    foreach ($group in $groups) {
        $artifacts = Join-Path $run ('measure-' + $group.Name)
        [void](Invoke-DotNet $group.Name (@($dll,'--filter') + $group.Patterns + @('--noOverwrite','--artifacts',$artifacts)))
        foreach ($benchmark in @(Read-Reports $artifacts)) {
            if ($benchmark.Statistics.N -lt 10) { throw "Too few final observations: $($benchmark.FullName)" }
            $summary.Results += @{Name=$benchmark.FullName;MeanNanoseconds=$benchmark.Statistics.Mean;MedianNanoseconds=$benchmark.Statistics.Median;
                StandardDeviation=$benchmark.Statistics.StandardDeviation;ConfidenceInterval=$benchmark.Statistics.ConfidenceInterval;
                AllocatedBytes=$benchmark.Memory.BytesAllocatedPerOperation;Samples=$benchmark.Statistics.N;Job=$benchmark.DisplayInfo}
        }
    }
    $expected = @('ResolverLifecycleBenchmarks.BuildAndDisposeEmpty','ResolverLifecycleBenchmarks.BuildAndDisposeGraph','ResolverLifecycleBenchmarks.BuildResolveAndDisposeGraph',
        'ResolutionBenchmarks.Singleton','ResolutionBenchmarks.TransientRequest','ResolutionBenchmarks.OpenGenericSingleton','ResolutionBenchmarks.ClosedGenericSingleton',
        'ResolutionBenchmarks.MaterializeMultiplePlugins','AppLifecycleBenchmarks.BuildAndDispose','AppLifecycleBenchmarks.BuildStartStopDispose') | ForEach-Object { 'TrueMoon.Cobalt.Benchmarks.' + $_ }
    if ($CompareProviders) {
        $expected = foreach ($operation in @('Singleton','Transient','OpenGeneric','ClosedGeneric','Multiple','EmptyBuild','GraphBuild','FirstGraph','AppBuild','AppLifecycle')) {
            foreach ($provider in @('Microsoft','Cobalt')) { 'TrueMoon.Cobalt.Benchmarks.ProviderComparisonBenchmarks.'+$provider+$operation }
        }
    }
    if ($summary.Results.Count -ne $expectedCaseCount -or @(Compare-Object ($expected | Sort-Object) ($summary.Results.Name | Sort-Object)).Count) { throw 'Final case set differs from graph v1 protocol.' }

    # Prime executable/OS file caches only; every measured sample still has a new CLR process.
    $providers = if ($CompareProviders) { @('cobalt','microsoft') } else { @('cobalt') }
    foreach ($provider in $providers) { [void](Invoke-DotNet "cold-prime-discarded-$provider" (@($dll,'--cold-sample') + $(if ($CompareProviders) { @($provider) } else { @() })) 120) }
    foreach ($sample in 1..$ColdSamples) {
      $order = if ($CompareProviders -and $sample % 2 -eq 0) { @('microsoft','cobalt') } else { $providers }
      foreach ($provider in $order) {
        $item = (Invoke-DotNet "cold-$sample-$provider" (@($dll,'--cold-sample') + $(if ($CompareProviders) { @($provider) } else { @() })) 120) | ConvertFrom-Json
        if (!$item.Completed -or $item.Graph -ne $summary.Graph -or $item.BuildStartMilliseconds -le 0 -or $item.BuildStartAllocatedBytes -le 0) { throw "Invalid cold sample $sample" }
        $summary.ColdSamples += $item
      }
    }
    if (@($summary.ColdSamples.ProcessId | Select-Object -Unique).Count -ne $ColdSamples*$providers.Count) { throw 'Cold samples reused a process.' }
    $summary.ColdStatistics = @()
    foreach ($provider in $providers) {
    $samples = @($summary.ColdSamples | Where-Object Provider -eq $provider)
    if ($samples.Count -ne $ColdSamples) { throw "Missing cold samples: $provider" }
    $times = @($samples.BuildStartMilliseconds | Sort-Object)
    $bytes = @($samples.BuildStartAllocatedBytes | Sort-Object)
    $middle = [int][Math]::Floor($ColdSamples / 2)
    $summary.ColdStatistics += @{Provider=$provider;Count=$ColdSamples;MedianMilliseconds=if ($ColdSamples % 2) {$times[$middle]} else {($times[$middle-1]+$times[$middle])/2};
        MinMilliseconds=$times[0];MaxMilliseconds=$times[-1];P95NearestRankMilliseconds=$times[[int][Math]::Ceiling(0.95*$ColdSamples)-1];
        MinAllocatedBytes=$bytes[0];MaxAllocatedBytes=$bytes[-1]}
    }
    $mismatches = @($summary.SourceFiles | Where-Object { (Get-FileHash -LiteralPath (Join-Path $workspace $_.Path)).Hash -ne $_.Sha256 })
    if ($mismatches.Count) { throw ('Sources changed during measurement: '+($mismatches.Path -join ', ')) }
    $summary.PowerPlanAfter = (& powercfg /getactivescheme | Out-String).Trim()
    if ($summary.PowerPlanAfter -ne $summary.PowerPlanBefore) { throw 'Active power plan changed during the run.' }
    $summary.Reports = @(Get-ChildItem -LiteralPath $run -Recurse -Filter '*-report-full.json' | ForEach-Object { @{Path=[IO.Path]::GetRelativePath($run,$_.FullName);Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    $summary.Completed = $true
    Write-Host "Baseline complete: $expectedCaseCount measured cases + $($summary.ColdSamples.Count) fresh-process startup samples. $run"
}
catch { $summary.Failure = $_.ToString(); throw }
finally {
    $summary.FinishedUtc = [DateTime]::UtcNow.ToString('O')
    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
}
