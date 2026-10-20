[CmdletBinding()]
param(
    [string]$RunDirectory,
    [string[]]$Configurations = @('Debug', 'Release'),
    [switch]$IncludeViewport,
    [string[]]$Scenarios = @('repeat','stale','bad-hello','bad-scene','startup-timeout','startup-cancel','command-timeout','command-cancel','stop-pending','crash','disconnect','heartbeat','forced-stop','hang-stop','launch-failure','parent-exit','app-stop')
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resultsRoot = Join-Path $workspace 'TestResults/EngineSessions'
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else { Join-Path $resultsRoot ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
if (!$run.StartsWith([IO.Path]::GetFullPath($resultsRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a directory within TestResults/EngineSessions.' }
if (Test-Path -LiteralPath $run) { throw 'Use a fresh RunDirectory.' }
[IO.Directory]::CreateDirectory($run) | Out-Null
$summary = [ordered]@{ Completed = $false; RunDirectory = $run; Commands = @(); Cases = @(); SourceFiles = @(); Binaries = @(); Failure = $null }
function Sources {
    $roots = @('TrueMoon.Aether','TrueMoon.Aether.Tests','ManualTests/EngineSessionProbe','ManualTests/Game.Logic','TrueMoon','TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit','TrueMoon.Enerit.Generator','TrueMoon.Mithril')
    if ($IncludeViewport) { $roots += @('TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Hosting','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Rendering.Skia','ManualTests/AlloyTest/Assets/Fonts') }
    $files = @('Directory.Build.props','Directory.Packages.props','global.json') | ForEach-Object { Get-Item (Join-Path $workspace $_) }
    $files += foreach ($root in $roots) { Get-ChildItem (Join-Path $workspace $root) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.ps1','.props','.targets','.dll','.json','.ttf','.txt' } }
    @($files | Sort-Object FullName | ForEach-Object { [ordered]@{ Path = [IO.Path]::GetRelativePath($workspace, $_.FullName); Sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } })
}
function Invoke-Owned([string]$name, [string]$file, [string[]]$arguments, [int]$timeout = 90) {
    $info = [Diagnostics.ProcessStartInfo]::new($file)
    $info.WorkingDirectory = $workspace; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($timeout * 1000)) { $process.Kill($true); $process.WaitForExit(); throw "$name timed out." }
        [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"), $stderr.GetAwaiter().GetResult())
        $summary.Commands += [ordered]@{ Name = $name; FileName = $file; Arguments = $arguments; ExitCode = $process.ExitCode }
        if ($process.ExitCode) { throw "$name failed: exit $($process.ExitCode)." }
        Write-Output "$name passed"
    }
    finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
try {
    $summary.SourceFiles = Sources
    foreach ($configuration in $Configurations) {
        if ($configuration -eq 'Debug') {
            Invoke-Owned 'build-Debug' 'dotnet' @('build',(Join-Path $PSScriptRoot 'EngineSessionProbe.csproj'),'--configuration','Debug','--verbosity','quiet') 180
            $binaryDirectory = Join-Path $PSScriptRoot 'bin/Debug/net10.0'
        }
        elseif ($configuration -eq 'Release') {
            $binaryDirectory = Join-Path $run 'publish'
            Invoke-Owned 'publish-Release' 'dotnet' @('publish',(Join-Path $PSScriptRoot 'EngineSessionProbe.csproj'),'--configuration','Release','--output',$binaryDirectory,'--verbosity','quiet') 180
        }
        else { throw 'Only Debug apphost and published Release are defined.' }
        $summary.Binaries += @(Get-ChildItem $binaryDirectory -File | Where-Object { $_.Extension -in '.dll','.exe','.json' } | ForEach-Object { [ordered]@{ Configuration = $configuration; Path = $_.FullName; Sha256 = (Get-FileHash $_.FullName).Hash } })
        if ($IncludeViewport) {
            $summary.Binaries += @(Get-ChildItem (Join-Path $binaryDirectory 'Assets/Fonts'),(Join-Path $binaryDirectory 'truemoon-native') -Recurse -File | ForEach-Object { [ordered]@{ Configuration = $configuration; Path = $_.FullName; Sha256 = (Get-FileHash $_.FullName).Hash } })
            $name = "$configuration-viewport"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--viewport',$caseDirectory) 180
            $report = Get-Content (Join-Path $caseDirectory 'viewport-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or !$report.ChildrenReleased -or $report.Mappings -ne 0 -or $report.ActiveUiSessions -ne 0 -or $report.PixelChecks -lt 20 -or $report.Copy.Samples -eq 0) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'viewport'; Report = "$name/viewport-report.json"; Passed = $true }
        }
        foreach ($scenario in $Scenarios) {
            $name = "$configuration-$scenario"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @($scenario,$caseDirectory) 65
            $report = Get-Content (Join-Path $caseDirectory 'report.json') -Raw | ConvertFrom-Json
            if (!$report.Passed) { throw "$name report failed." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = $scenario; Report = "$name/report.json"; Passed = $true }
        }
    }
    if (($summary.SourceFiles | ConvertTo-Json -Depth 5 -Compress) -cne ((Sources) | ConvertTo-Json -Depth 5 -Compress)) { throw 'Sources changed during verification.' }
    $summary.Completed = $true
}
catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'summary.json') }
