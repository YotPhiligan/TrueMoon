#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RunDirectory,
    [ValidateRange(10, 100000)][int]$Samples = 200,
    [ValidateRange(1, 1000)][int]$WarmupSamples = 20,
    [ValidateRange(0, 60000)][int]$WarmupMilliseconds = 250,
    [ValidateRange(1, 10)][int]$Passes = 2
)
. (Join-Path $PSScriptRoot '../../ManualTests/BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$RunDirectory) { $RunDirectory = Join-Path $workspace ('TestResults/ViewBaseline/run-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff')) }
$run = [IO.Path]::GetFullPath($RunDirectory)
if (Test-Path -LiteralPath $run) { throw "Use a fresh run directory: $run" }
[void][IO.Directory]::CreateDirectory($run)
$project = Join-Path $PSScriptRoot 'TrueMoon.Argentis.Benchmarks.csproj'
$dll = Join-Path $PSScriptRoot "bin/Release/$($trueMoonBuildProfile.Framework)/TrueMoon.Argentis.Benchmarks.dll"

# Scene sizes per group and measured methods per group, including the shared harness floor case.
$sizesByGroup = [ordered]@{
    'node-cost' = @(39, 312, 2496)
    'property-access' = @(39, 312, 2496)
    'property-store' = @(39, 312, 2496)
    'collection-binding' = @(39, 312, 2496)
    'keyed-binding' = @(39, 312, 2496)
    'virtualized-binding' = @(39, 312, 2496)
    'property-binding' = @(1, 64, 512)
    'conditional-slot' = @(39, 312, 2496)
}
$methodsByGroup = @{ 'node-cost' = 7; 'property-access' = 7; 'property-store' = 4; 'collection-binding' = 7; 'keyed-binding' = 7; 'virtualized-binding' = 8; 'property-binding' = 6; 'conditional-slot' = 5 }

$summary = [ordered]@{
    Completed = $false; Scene = 'inspector-v1'; Samples = $Samples; WarmupSamples = $WarmupSamples
    WarmupMilliseconds = $WarmupMilliseconds; Passes = $Passes; StartedUtc = [DateTime]::UtcNow.ToString('O')
    Commands = @(); Probes = @(); Results = @()
}

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
            try { $process.Kill($true) } catch { }
            throw "Timeout $name after ${timeoutSeconds}s"
        }
        $stdout.Result | Set-Content -LiteralPath (Join-Path $run "$name.log") -Encoding utf8
        $stderr.Result | Set-Content -LiteralPath (Join-Path $run "$name.stderr.log") -Encoding utf8
        $summary.Commands += @{ Name = $name; Arguments = $commandArguments; ExitCode = $process.ExitCode }
        if ($process.ExitCode -ne 0) { throw "Failed $name (exit $($process.ExitCode)); see $run/$name.log" }
        return $stdout.Result
    }
    finally { $process.Dispose() }
}

# Nearest rank, matching the K9 settings baseline so both suites are read the same way.
function Get-Statistics([long[]]$values) {
    $sorted = @($values | Sort-Object)
    if (!$sorted.Count) { throw 'No samples.' }
    @{
        Count = $sorted.Count
        Median = $sorted[[int][Math]::Ceiling(0.50 * $sorted.Count) - 1]
        P95 = $sorted[[int][Math]::Ceiling(0.95 * $sorted.Count) - 1]
        Min = $sorted[0]
        Max = $sorted[-1]
        Mean = [double](($sorted | Measure-Object -Average).Average)
    }
}

try {
    $summary.GitHead = (& git -C $workspace rev-parse HEAD).Trim()
    $gitStatus = (& git -C $workspace status --porcelain | Out-String).Trim()
    $summary.GitDirty = $gitStatus.Length -gt 0
    $summary.PowerPlanBefore = (& powercfg /getactivescheme | Out-String).Trim()
    $summary.Environment = @{}
    foreach ($key in @('DOTNET_TieredCompilation', 'DOTNET_TieredPGO', 'DOTNET_gcServer', 'COMPlus_TieredCompilation', 'COMPlus_TieredPGO', 'COMPlus_gcServer')) {
        $summary.Environment[$key] = [Environment]::GetEnvironmentVariable($key)
    }
    $summary.Sdk = (Invoke-DotNet 'sdk' @('--version')).Trim()
    $summary.Runtime = @((Invoke-DotNet 'runtimes' @('--list-runtimes')) -split "`r?`n" | Where-Object { $_ -like 'Microsoft.NETCore.App *' })
    [void](Invoke-DotNet 'build-release' @('build', $project, '--configuration', 'Release', '-v', 'quiet'))

    $inputs = @(
        foreach ($directory in @('TrueMoon.Argentis', 'TrueMoon.Alloy', 'Benchmarks/TrueMoon.Argentis.Benchmarks')) {
            Get-ChildItem -LiteralPath (Join-Path $workspace $directory) -Recurse -File |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.ps1', '.md') }
        }
        foreach ($file in @('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'global.json', 'TrueMoon.slnx')) { Get-Item -LiteralPath (Join-Path $workspace $file) }
    )
    $summary.SourceFiles = @($inputs | Sort-Object FullName -Unique | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($workspace, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    $summary.Binaries = @('TrueMoon.Argentis.Benchmarks.dll', 'TrueMoon.Argentis.dll', 'TrueMoon.Alloy.dll') | ForEach-Object {
        $path = Join-Path (Split-Path $dll) $_
        @{ Name = $_; Sha256 = (Get-FileHash -LiteralPath $path).Hash }
    }

    $verified = Invoke-DotNet 'verify-workload' @($dll, '--verify')
    if ($verified.Trim() -ne 'VIEW_BASELINE_VERIFIED') { throw 'Workload verification incomplete.' }

    # One process per group, scene size and pass; distributions are never merged across passes.
    foreach ($pass in 1..$Passes) {
        foreach ($group in $sizesByGroup.Keys) {
            foreach ($size in $sizesByGroup[$group]) {
                $name = "probe-$group-$size-pass$pass"
                $output = Join-Path $run "$name.json"
                [void](Invoke-DotNet $name @($dll, '--measure', '--group', $group, '--size', "$size",
                    '--samples', "$Samples", '--warmup-samples', "$WarmupSamples", '--warmup-ms', "$WarmupMilliseconds",
                    '--output', $output))
                $probe = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
                if (!$probe.Completed -or $probe.Group -ne $group -or $probe.Size -ne $size -or
                    $probe.Samples -ne $Samples -or $probe.ProcessId -le 0) { throw "Invalid probe payload: $name" }
                $summary.Probes += @{ Name = $name; Pass = $pass; Group = $group; Size = $size; ProcessId = $probe.ProcessId
                    ProcessStartedAt = $probe.ProcessStartedAt
                    TickFrequency = $probe.TickFrequency; Runtime = $probe.Runtime
                    Methods = @($probe.Cases.Method).Count; Path = $output; Sha256 = (Get-FileHash -LiteralPath $output).Hash }
                if (@($probe.Cases.Method).Count -ne $methodsByGroup[$group]) { throw "$($name): missing methods." }
            }
        }
    }

    # A process id alone can be reused by the operating system; the start stamp is what makes the pair unique.
    $identities = @($summary.Probes | ForEach-Object { "$($_.ProcessId)@$($_.ProcessStartedAt)" })
    if (@($identities | Select-Object -Unique).Count -ne @($summary.Probes).Count) { throw 'Probe batches reused a process.' }

    foreach ($probeRecord in $summary.Probes) {
        $probe = Get-Content -LiteralPath $probeRecord.Path -Raw | ConvertFrom-Json
        $frequency = $probe.TickFrequency
        foreach ($case in $probe.Cases) {
            $microseconds = @($case.Ticks | ForEach-Object { [long][Math]::Round($_ * 1000000 / $frequency) })
            $summary.Results += @{
                Pass = $probeRecord.Pass; Group = $probeRecord.Group; Size = $probeRecord.Size; Method = $case.Method
                Microseconds = Get-Statistics $microseconds
                Bytes = Get-Statistics @($case.Bytes)
                WarmupCalls = $case.WarmupCalls
                Collections = $case.Collections
                Source = $probeRecord.Name
            }
        }
    }

    $expectedCases = 0
    foreach ($group in $sizesByGroup.Keys) { $expectedCases += $sizesByGroup[$group].Count * $methodsByGroup[$group] }
    $expectedCases *= $Passes
    if ($summary.Results.Count -ne $expectedCases) { throw "Expected $expectedCases case results, found $($summary.Results.Count)." }
    $noisyFloor = @($summary.Results | Where-Object { $_.Method -eq 'NoOp' -and $_.Bytes.Median -ne 0 })
    if ($noisyFloor.Count) { throw "Harness floor is not allocation-free: $($noisyFloor.Count) cases." }

    $mismatches = @($summary.SourceFiles | Where-Object { (Get-FileHash -LiteralPath (Join-Path $workspace $_.Path)).Hash -ne $_.Sha256 })
    if ($mismatches.Count) { throw ('Sources changed during measurement: ' + ($mismatches.Path -join ', ')) }
    $summary.PowerPlanAfter = (& powercfg /getactivescheme | Out-String).Trim()
    if ($summary.PowerPlanAfter -ne $summary.PowerPlanBefore) { throw 'Active power plan changed during the run.' }

    $markdown = @('| Pass | Группа | Размер | Операция | Медиана мкс | P95 мкс | Байт медиана | P95 байт | Samples |')
    $markdown += '| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |'
    foreach ($result in $summary.Results) {
        $markdown += "| $($result.Pass) | $($result.Group) | $($result.Size) | $($result.Method) | $($result.Microseconds.Median) | $($result.Microseconds.P95) | $($result.Bytes.Median) | $($result.Bytes.P95) | $($result.Microseconds.Count) |"
    }
    $markdown | Set-Content -LiteralPath (Join-Path $run 'summary.md') -Encoding utf8
    $summary.Reports = @($summary.Probes | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($run, $_.Path); Sha256 = $_.Sha256 } })
    $summary.Completed = $true
    Write-Host "View baseline complete: $($summary.Probes.Count) probe processes, $($summary.Results.Count) case results. $run"
}
catch { $summary.Failure = $_.ToString(); throw }
finally {
    $summary.FinishedUtc = [DateTime]::UtcNow.ToString('O')
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
}
