[CmdletBinding()]
param([string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/CoreHeadless')) + [IO.Path]::DirectorySeparatorChar
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path $allowed ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
}
if (!$run.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be inside TestResults/CoreHeadless.' }
if (Test-Path -LiteralPath $run) { throw 'Use a new RunDirectory; previous evidence is preserved.' }
[void][IO.Directory]::CreateDirectory((Join-Path $run 'logs'))
$summary = [ordered]@{ Completed = $false; Failure = $null; SDK = (& dotnet --version).Trim(); Cases = @(); Commands = @(); SourceFiles = @(); PackageRun = $null }

function Invoke-Child([string]$Name, [string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds = 300) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $workspace
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $started = $false
    try {
        $started = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) { throw "$Name timed out." }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $log = Join-Path $run "logs/$Name.log"
        $output | Set-Content -LiteralPath $log -Encoding utf8
        $summary.Commands += @{ Name = $Name; Executable = $Executable; Arguments = $Arguments; ExitCode = $process.ExitCode; Log = [IO.Path]::GetRelativePath($run, $log) }
        if ($process.ExitCode -ne 0) { throw "$Name failed (exit $($process.ExitCode)); see $log" }
        Write-Host "$Name passed"
    }
    finally {
        if ($started -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

try {
    $sourceDirectories = @('TrueMoon','TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator',
        'TrueMoon.Extensions.DependencyInjection','ManualTests/CorePackageConsumer','ManualTests/CoreHeadlessProbe')
    $files = foreach ($directory in $sourceDirectories) {
        Get-ChildItem -LiteralPath (Join-Path $workspace $directory) -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }
    $files += foreach ($name in @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json')) {
        Get-Item -LiteralPath (Join-Path $workspace $name)
    }
    $summary.SourceFiles = @($files | Sort-Object FullName -Unique | ForEach-Object {
        @{ Path = [IO.Path]::GetRelativePath($workspace, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
    })
    $project = Join-Path $PSScriptRoot 'CoreHeadlessProbe.csproj'
    foreach ($configuration in @('Debug','Release')) {
        Invoke-Child "build-$configuration" 'dotnet' @('build',$project,'-c',$configuration,'--verbosity','quiet')
        $publish = Join-Path $run "publish/$configuration"
        Invoke-Child "publish-$configuration" 'dotnet' @('publish',$project,'-c',$configuration,'--no-restore','--self-contained','false','--output',$publish,'--verbosity','quiet')
        foreach ($delivery in @(@{ Name = 'build'; Path = (Join-Path $PSScriptRoot "bin/$configuration/net10.0") }, @{ Name = 'publish'; Path = $publish })) {
            foreach ($provider in @('cobalt','microsoft')) {
                foreach ($scenario in @('normal','cancel','start-failure','stop-failure','build-failure','config-failure','cleanup-failure')) {
                    if ($scenario -eq 'cleanup-failure' -and $provider -ne 'cobalt') { continue }
                    $name = "$configuration-$($delivery.Name)-$provider-$scenario"
                    $reportPath = Join-Path $run "$name.json"
                    Invoke-Child $name 'dotnet' @((Join-Path $delivery.Path 'HeadlessConsumer.dll'),$provider,$scenario,$reportPath) 30
                    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
                    if (!$report.Completed -or $report.Provider -ne $provider -or $report.Scenario -ne $scenario -or $report.Configuration -ne $configuration -or !$report.ConsumerGeneration) {
                        throw "Incomplete report: $name"
                    }
                    $summary.Cases += @{ Name = $name; Report = [IO.Path]::GetRelativePath($run,$reportPath); Sha256 = (Get-FileHash -LiteralPath $reportPath).Hash }
                }
            }
        }
    }
    if ($summary.Cases.Count -ne 52) { throw 'Source matrix incomplete.' }
    $packageRun = Join-Path $workspace ('TestResults/CorePackages/core-quality-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
    $summary.PackageRun = $packageRun
    Invoke-Child 'package-proof' (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-File',(Join-Path $workspace 'ManualTests/CorePackageConsumer/RunPackageSmoke.ps1'),'-RunDirectory',$packageRun) 600
    $package = Get-Content -LiteralPath (Join-Path $packageRun 'summary.json') -Raw | ConvertFrom-Json
    if (!$package.Completed -or $package.Cases.Count -ne 80 -or $package.ReadmeExamples.Count -ne 3 -or !$package.NegativeControl) { throw 'Package proof incomplete.' }
    foreach ($source in $summary.SourceFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256) { throw "Source changed: $($source.Path)" }
    }
    foreach ($source in $package.SourceFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256) { throw "Package source changed: $($source.Path)" }
    }
    $summary.PackageSummarySha256 = (Get-FileHash -LiteralPath (Join-Path $packageRun 'summary.json')).Hash
    $summary.Completed = $true
    Write-Host "Headless proof:52 source +80 package +3 README processes passed. $run"
}
catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8 }
