param(
    [string]$ResultsDirectory = 'TestResults/AppUnitsPipes/manual'
)
$ErrorActionPreference = 'Stop'
$srcRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resultRoot = [IO.Path]::GetFullPath((Join-Path $srcRoot $ResultsDirectory))
if (!$resultRoot.StartsWith($srcRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ResultsDirectory must be inside the source workspace.'
}
[IO.Directory]::CreateDirectory($resultRoot) | Out-Null
$scenarios = @('payload','concurrent','cancel-request','remote-error','restart','budget','stop-pending','startup-failure','parent-exit','saturated-stop','saturated-kill')
$project = Join-Path $PSScriptRoot 'AppUnitsPipesProbe.csproj'
$cases = [Collections.Generic.List[object]]::new()
$summary = [ordered]@{
    Completed = $false
    TimestampUtc = [DateTime]::UtcNow.ToString('O')
    OS = [Environment]::OSVersion.VersionString
    SDK = (& dotnet --version)
    Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    Commands = [Collections.Generic.List[object]]::new()
    SourceSha256 = [ordered]@{}
    Cases = $cases
}
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $srcRoot 'TrueMoon.Enerit'), (Join-Path $srcRoot 'TrueMoon.Enerit.Generator'), (Join-Path $srcRoot 'TrueMoon.Mithril'), (Join-Path $srcRoot 'TrueMoon'), (Join-Path $srcRoot 'TrueMoon.Extensions.DependencyInjection'), (Join-Path $srcRoot 'TrueMoon.Contracts'), (Join-Path $srcRoot 'TrueMoon.Core'), (Join-Path $srcRoot 'TrueMoon.Cobalt'), (Join-Path $srcRoot 'TrueMoon.Cobalt.Generator'), $PSScriptRoot -Recurse -File |
    Where-Object { $_.Extension -in @('.cs', '.csproj', '.ps1') -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
foreach ($file in $sourceFiles) {
    $relative = [IO.Path]::GetRelativePath($srcRoot, $file.FullName)
    $summary.SourceSha256[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
function Invoke-OwnedProcess([string]$FileName, [string[]]$Arguments, [string]$LogPrefix, [int]$TimeoutSeconds) {
    $info = [Diagnostics.ProcessStartInfo]::new($FileName)
    $info.WorkingDirectory = $srcRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $started = $false
    try {
        $started = $process.Start()
        if (!$started) { throw "Could not start $FileName" }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) { throw "Timeout: $FileName $Arguments" }
        if (!$stdoutTask.Wait(5000) -or !$stderrTask.Wait(5000)) { throw 'Output stream did not close.' }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($LogPrefix + '.stdout.log', $stdout)
        [IO.File]::WriteAllText($LogPrefix + '.stderr.log', $stderr)
        $summary.Commands.Add([ordered]@{ FileName = $FileName; Arguments = $Arguments; ExitCode = $process.ExitCode })
        if ($process.ExitCode -ne 0) { throw "Exit $($process.ExitCode): $FileName $Arguments. See $LogPrefix" }
        return $stdout
    }
    finally {
        if ($started -and !$process.HasExited) {
            $process.Kill($true)
            if (!$process.WaitForExit(5000)) { throw 'Owned probe could not be reaped.' }
        }
        $process.Dispose()
    }
}
try {
    Invoke-OwnedProcess 'dotnet' @('build', $project, '--configuration', 'Debug', '--verbosity', 'quiet') (Join-Path $resultRoot 'build-debug') 60 | Out-Null
    $published = Join-Path $resultRoot 'published-release'
    Invoke-OwnedProcess 'dotnet' @('publish', $project, '--configuration', 'Release', '--output', $published, '--verbosity', 'quiet') (Join-Path $resultRoot 'publish-release') 60 | Out-Null
    foreach ($configuration in @('Debug', 'PublishedRelease')) {
        $directory = if ($configuration -eq 'Debug') { Join-Path $PSScriptRoot 'bin/Debug/net10.0' } else { $published }
        $executable = Join-Path $directory $(if ($IsWindows) { 'AppUnitsPipesProbe.exe' } else { 'AppUnitsPipesProbe' })
        foreach ($provider in @('cobalt', 'di')) {
            foreach ($scenario in $scenarios) {
                $caseDirectory = Join-Path $resultRoot ("$configuration-$provider-$scenario-" + [Guid]::NewGuid().ToString('N'))
                $stdout = Invoke-OwnedProcess $executable @($scenario, $provider, $caseDirectory) (Join-Path $resultRoot "$configuration-$provider-$scenario") 40
                $records = @($stdout -split '\r?\n' | Where-Object { $_.StartsWith('RESULT ') })
                if ($records.Count -ne 1) { throw "Expected one RESULT in $configuration/$provider/$scenario" }
                $report = $records[0].Substring(7) | ConvertFrom-Json
                if ($report.Scenario -ne $scenario -or $report.Provider -ne $provider -or !$report.Cleanup) { throw 'Invalid composition result.' }
                if ($report.ClientCleanupObserved -ne ($scenario -ne 'parent-exit') -or $report.WorkersCompleted -ne ($scenario -ne 'parent-exit')) { throw 'Cleanup scope mismatch.' }
                if ($report.AppCancelled -ne ($scenario -eq 'budget')) { throw 'App lifetime mismatch.' }
                if ($scenario -in @('saturated-stop', 'saturated-kill')) {
                    $killed = $scenario -eq 'saturated-kill'
                    if ($report.PendingBefore -ne 2 -or $report.QueuedBefore -ne 1 -or $report.FailedRequests -ne 3 -or
                        $report.Pending -ne 0 -or $report.Queued -ne 0 -or $report.Restarts -ne 0) { throw 'Saturated admission cleanup mismatch.' }
                    if ($report.OverloadError -ne 'PipesInvocationOverloadedException' -or $report.RequestErrors.Count -ne 3 -or
                        @($report.RequestErrors | Where-Object { $_ -notin @('IOException', 'EndOfStreamException', 'ObjectDisposedException', 'OperationCanceledException', 'TaskCanceledException') }).Count -ne 0) { throw 'Saturated request failure mismatch.' }
                    if ($report.ChildPids.Count -ne 1 -or $report.ControlObservedPid -ne $report.ChildPids[0] -or
                        $report.ReleasedPid -ne $report.ChildPids[0] -or !$report.MonitorCompleted -or $report.EnteredHandlers -ne 2 -or
                        $report.ActiveHandlersAtControl -ne 2) { throw 'Loaded child identity/monitor mismatch.' }
                    if ([string]::IsNullOrWhiteSpace($report.ControlEndpoint) -or $report.ControlEndpoint -eq $report.DomainEndpoint -or
                        $report.ControlEndpoint -eq ('tm_' + $report.DomainEndpoint)) { throw 'Lifecycle control endpoint is not independent.' }
                    if (($report.ExitCodes[0] -ne 0) -ne $killed -or $report.ServerDrained -eq $killed -or
                        $report.HandlersDrained -ne $(if ($killed) { 0 } else { 2 }) -or
                        $report.KillCount -ne $(if ($killed) { 1 } else { 0 }) -or
                        $report.ReleaseOutcome -ne $(if ($killed) { 'Killed' } else { 'Graceful' })) { throw 'Loaded child shutdown outcome mismatch.' }
                    if ($report.TerminationDelay -ne 3000 -or $report.StopMilliseconds -ge 10000) { throw 'Loaded child stop exceeded its budget.' }
                }
                $report | Add-Member -NotePropertyName Configuration -NotePropertyValue $configuration
                $cases.Add($report); Write-Host "$configuration/$provider/$scenario passed"
            }
        }
    }
    if ($cases.Count -ne 44) { throw 'Incomplete composition matrix.' }
    $summary.Completed = $true
}
finally { $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $resultRoot 'summary.json') -Encoding utf8 }
