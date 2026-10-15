[CmdletBinding()]
param([string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/EneritBaseline')) + [IO.Path]::DirectorySeparatorChar
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else { Join-Path $allowed ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
if (!$run.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $run)) { throw 'Use a new RunDirectory inside TestResults/EneritBaseline.' }
[void][IO.Directory]::CreateDirectory((Join-Path $run 'logs'))
$summary = [ordered]@{ Completed = $false; Failure = $null; StartedUtc = [DateTime]::UtcNow.ToString('O'); SDK = (& dotnet --version).Trim();
    Commands = @(); Verification = @(); Measurements = @(); SourceFiles = @(); Binaries = @(); Results = @(); PowerPlanBefore = (& powercfg /getactivescheme | Out-String).Trim() }
$summary.Cpu = @(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors)

function Invoke-Owned([string]$Name, [string[]]$Arguments, [int]$TimeoutSeconds = 180) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $workspace; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start; $started = $false
    try {
        $started = $process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) { throw "$Name timed out." }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $output | Set-Content -LiteralPath (Join-Path $run "logs/$Name.log") -Encoding utf8
        $summary.Commands += @{ Name = $Name; Arguments = $Arguments; ExitCode = $process.ExitCode }
        if ($process.ExitCode -ne 0) { throw "$Name failed; see $run/logs/$Name.log" }
        Write-Host "$Name passed"
        return $output
    }
    finally { if ($started -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}

function Median([double[]]$Values) {
    $ordered = @($Values | Sort-Object); $middle = [int][Math]::Floor($ordered.Count / 2)
    if ($ordered.Count % 2) { return $ordered[$middle] }
    return ($ordered[$middle - 1] + $ordered[$middle]) / 2
}

function Check-Report([string]$Path, [string]$Mode, $Case) {
    $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if (!$report.Completed -or $report.Mode -ne $Mode -or $report.Case.Shape -ne $Case.Shape -or $report.Case.Concurrency -ne $Case.Concurrency -or
        $report.Environment.Configuration -ne 'Release' -or !$report.ClientClosed -or !$report.ClientWorkersCompleted -or $report.PendingAfterDispose -ne 0 -or $report.ChildExitCode -ne 0) { throw "Invalid report: $Path" }
    if ($report.Samples.Count -ne $(if ($Mode -eq 'verify') { 1 } else { 3 })) { throw 'Sample count mismatch.' }
    foreach ($sample in $report.Samples) {
        if ($sample.Operations -ne $sample.LatencyTicks.Count -or $sample.Server.Calls -ne $sample.Operations -or $sample.Server.Errors -ne 0) { throw 'Raw sample mismatch.' }
    }
    return $report
}

try {
    $directories = @('TrueMoon.Enerit','TrueMoon.Enerit.Generator','TrueMoon.Core','TrueMoon.Contracts','Benchmarks/TrueMoon.Enerit.Benchmarks')
    $inputs = @(foreach ($directory in $directories) {
        Get-ChildItem -LiteralPath (Join-Path $workspace $directory) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    })
    $inputs += foreach ($name in @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','TrueMoon.slnx')) { Get-Item -LiteralPath (Join-Path $workspace $name) }
    $summary.SourceFiles = @($inputs | Sort-Object FullName -Unique | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($workspace,$_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    $published = Join-Path $run 'published'
    Invoke-Owned 'publish-release' @('publish',(Join-Path $PSScriptRoot 'TrueMoon.Enerit.Benchmarks.csproj'),'-c','Release','--self-contained','false','-o',$published,'--verbosity','quiet') | Out-Null
    $summary.Binaries = @(Get-ChildItem -LiteralPath $published -File | ForEach-Object { @{ Path = $_.Name; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    $summary.GeneratedSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'obj/generated/Release') -Recurse -File -Filter '*.g.cs' | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($workspace,$_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    if ($summary.GeneratedSources.Count -lt 3) { throw 'Generated client/handler/DTO missing.' }
    if (@(Get-ChildItem $published -File -Filter '*.dll' | Where-Object Name -match 'Cobalt|Mithril|Skia|Silk|Microsoft.CodeAnalysis|Generator').Count) { throw 'Unexpected runtime dependency.' }
    $dll = Join-Path $published 'EneritBaseline.dll'
    $cases = @(Invoke-Owned 'list' @($dll,'--list') | ConvertFrom-Json)
    if ($cases.Count -ne 10) { throw 'Expected 10 cases.' }
    foreach ($case in $cases) {
        $name = "verify-$($case.Shape)-c$($case.Concurrency)"; $reportPath = Join-Path $run "$name.json"
        Invoke-Owned $name @($dll,'--client',$case.Shape,[string]$case.Concurrency,$reportPath,'--verify') | Out-Null
        [void](Check-Report $reportPath 'verify' $case)
        $summary.Verification += @{ Name = $name; Report = [IO.Path]::GetRelativePath($run,$reportPath); Sha256 = (Get-FileHash -LiteralPath $reportPath).Hash }
    }
    foreach ($trial in 0..2) {
        foreach ($position in 0..9) {
            $case = $cases[($position + $trial * 3) % $cases.Count]
            $name = "trial$($trial + 1)-$($case.Shape)-c$($case.Concurrency)"; $reportPath = Join-Path $run "$name.json"
            Invoke-Owned $name @($dll,'--client',$case.Shape,[string]$case.Concurrency,$reportPath) | Out-Null
            [void](Check-Report $reportPath 'measure' $case)
            $summary.Measurements += @{ Name = $name; Report = [IO.Path]::GetRelativePath($run,$reportPath); Sha256 = (Get-FileHash -LiteralPath $reportPath).Hash }
        }
    }
    foreach ($case in $cases) {
        $samples = @(foreach ($measurement in $summary.Measurements) {
            $report = Get-Content -LiteralPath (Join-Path $run $measurement.Report) -Raw | ConvertFrom-Json
            if ($report.Case.Shape -eq $case.Shape -and $report.Case.Concurrency -eq $case.Concurrency) { $report.Samples }
        })
        if ($samples.Count -ne 9) { throw 'Expected 3 fresh pairs x3 samples per case.' }
        $summary.Results += [pscustomobject]@{ Shape = $case.Shape; Concurrency = $case.Concurrency; Samples = 9;
            P50Microseconds = (Median @($samples.P50Microseconds)); P95Microseconds = (Median @($samples.P95Microseconds));
            CallsPerSecond = (Median @($samples.CallsPerSecond)); CallsPerSecondMin = ($samples.CallsPerSecond | Measure-Object -Minimum).Minimum;
            CallsPerSecondMax = ($samples.CallsPerSecond | Measure-Object -Maximum).Maximum;
            ClientBytesPerCall = (Median @($samples | ForEach-Object { $_.Client.AllocatedBytes / $_.Operations }));
            ServerBytesPerCall = (Median @($samples | ForEach-Object { $_.Server.AllocatedBytes / $_.Operations })) }
    }
    $summary.Results | Export-Csv -LiteralPath (Join-Path $run 'results.csv') -NoTypeInformation
    foreach ($source in $summary.SourceFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256) { throw "Source changed: $($source.Path)" }
    }
    foreach ($binary in $summary.Binaries) {
        if ((Get-FileHash -LiteralPath (Join-Path $published $binary.Path)).Hash -ne $binary.Sha256) { throw "Binary changed: $($binary.Path)" }
    }
    $summary.PowerPlanAfter = (& powercfg /getactivescheme | Out-String).Trim()
    if ($summary.PowerPlanBefore -ne $summary.PowerPlanAfter) { throw 'Power plan changed.' }
    if ($summary.Measurements.Count -ne 30 -or $summary.Verification.Count -ne 10) { throw 'Incomplete run.' }
    $summary.Completed = $true
    Write-Host "Enerit baseline:10 verify +30 fresh-pair measurements/90 samples passed. $run"
}
catch { $summary.Failure = $_.ToString(); throw }
finally {
    $summary.FinishedUtc = [DateTime]::UtcNow.ToString('O')
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
}
