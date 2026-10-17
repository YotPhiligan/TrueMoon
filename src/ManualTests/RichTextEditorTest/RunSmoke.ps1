[CmdletBinding()]
param([string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = Join-Path $workspace 'TestResults/RichTextEditor'
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else { Join-Path $root ('smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
if (!$run.StartsWith([IO.Path]::GetFullPath($root) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be within TestResults/RichTextEditor.' }
if (Test-Path -LiteralPath $run) { throw 'Use a fresh RunDirectory.' }
[IO.Directory]::CreateDirectory($run) | Out-Null
$summary = [ordered]@{ Completed = $false; RunDirectory = $run; Commands = @(); Cases = @(); Failure = $null }
function Invoke-Owned([string]$Name, [string[]]$Arguments, [int]$TimeoutSeconds = 120) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $workspace; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null; throw "$Name timed out." }
        [IO.File]::WriteAllText((Join-Path $run "$Name.stdout.log"), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $run "$Name.stderr.log"), $stderr.GetAwaiter().GetResult())
        $summary.Commands += @{ Name = $Name; Arguments = $Arguments; ExitCode = $process.ExitCode }
        if ($process.ExitCode) { throw "$Name failed exit$($process.ExitCode)." }
        Write-Host "$Name passed"
    } finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }; $process.Dispose() }
}
try {
    $project = Join-Path $PSScriptRoot 'RichTextEditorTest.csproj'
    Invoke-Owned 'build-debug' @('build', $project, '--configuration', 'Debug', '--verbosity', 'quiet')
    $publish = Join-Path $run 'publish'
    Invoke-Owned 'publish-release' @('publish', $project, '--configuration', 'Release', '--self-contained', 'false', '--output', $publish, '--verbosity', 'quiet')
    foreach ($delivery in @(@{ Name = 'Debug'; Path = Join-Path $PSScriptRoot 'bin/Debug/net10.0' }, @{ Name = 'Release'; Path = $publish })) {
        foreach ($backend in @('raster','opengl','vulkan')) {
            $name = "$($delivery.Name)-$backend"; $reportPath = Join-Path $run "$name.json"
            Invoke-Owned $name @((Join-Path $delivery.Path 'RichTextEditorTest.dll'), $backend, $reportPath) 50
            $report = Get-Content $reportPath -Raw | ConvertFrom-Json
            if (!$report.Completed -or $report.Backend -ne $backend -or $report.Frames -lt 14 -or $report.Failure -or $report.RichTextKit -notlike 'Topten.RichTextKit,*') { throw "Invalid report $name" }
            $summary.Cases += @{ Name = $name; Report = $report }
        }
    }
    if ($summary.Cases.Count -ne 6) { throw 'Incomplete renderer matrix.' }
    $summary.Completed = $true
} catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'summary.json') }
