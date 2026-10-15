param(
    [string]$ResultsDirectory = 'TestResults/MithrilBaseline/manual'
)
$ErrorActionPreference = 'Stop'
$srcRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resultRoot = [IO.Path]::GetFullPath((Join-Path $srcRoot $ResultsDirectory))
if (!$resultRoot.StartsWith($srcRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ResultsDirectory must be inside the source workspace.'
}
[IO.Directory]::CreateDirectory($resultRoot) | Out-Null
$scenarios = @('graceful', 'timeout', 'cancel-stop', 'dispose', 'async-dispose', 'already-exited', 'same-name', 'duplicate-start', 'throwing-sink')
$project = Join-Path $PSScriptRoot 'MithrilLifecycleProbe.csproj'
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
foreach ($relative in @('TrueMoon.Mithril/UnitProcessDiagnostic.cs', 'TrueMoon.Mithril/Units/ProcessUnitHandle.cs', 'TrueMoon.Mithril/Units/ChildProcessUnitHandle.cs', 'TrueMoon.Mithril/UnitsController.cs',
    'ManualTests/MithrilLifecycleProbe/Program.cs', 'ManualTests/MithrilLifecycleProbe/MithrilLifecycleProbe.csproj',
    'ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1')) {
    $summary.SourceSha256[$relative] = (Get-FileHash -LiteralPath (Join-Path $srcRoot $relative) -Algorithm SHA256).Hash
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
        $executable = Join-Path $directory $(if ($IsWindows) { 'MithrilLifecycleProbe.exe' } else { 'MithrilLifecycleProbe' })
        foreach ($scenario in $scenarios) {
            $stdout = Invoke-OwnedProcess $executable @($scenario) (Join-Path $resultRoot "$configuration-$scenario") 25
            $records = @($stdout -split '\r?\n' | Where-Object { $_.StartsWith('RESULT ') })
            if ($records.Count -ne 1) { throw "Expected one RESULT in $configuration/$scenario" }
            $report = $records[0].Substring(7) | ConvertFrom-Json
            if ($report.Scenario -ne $scenario -or !$report.ExitSeen -or !$report.HandleReleased -or !$report.RepeatedStopSucceeded) {
                throw "Invalid lifecycle report in $configuration/$scenario"
            }
            $report | Add-Member -NotePropertyName Configuration -NotePropertyValue $configuration
            $cases.Add($report)
            Write-Host "$configuration/$scenario passed (child $($report.ChildPid), exit $($report.ExitCode))"
        }
    }
    if ($cases.Count -ne 18) { throw 'Incomplete lifecycle matrix.' }
    foreach ($relative in $summary.SourceSha256.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $srcRoot $relative) -Algorithm SHA256).Hash -ne $summary.SourceSha256[$relative]) { throw "Source changed: $relative" }
    }
    $summary.Completed = $true
}
finally {
    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $resultRoot 'summary.json') -Encoding utf8
}
