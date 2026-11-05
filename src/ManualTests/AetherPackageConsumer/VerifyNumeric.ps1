param(
    [string]$EvidenceDirectory,
    [ValidateSet('All', 'SourceDebug', 'PublishedRelease')][string]$RunProfile = 'All',
    [ValidateSet('Standard', 'Embedded')][string]$HostKind = 'Standard',
    [ValidateSet('Routed', 'WindowsPointer', 'WindowsArrows')][string]$InputMode = 'Routed'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$EvidenceDirectory) { $EvidenceDirectory = Join-Path $repo ('TestResults/NumericNative-' + [guid]::NewGuid().ToString('N')) }
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$scenario = switch ($InputMode) { Routed { 'numeric' } WindowsPointer { 'numeric-pointer' } WindowsArrows { 'numeric-os' } }
$composition = if ($HostKind -eq 'Embedded') { 'embedded' } else { 'standard' }
$provider = if ($HostKind -eq 'Embedded') { 'di' } else { 'cobalt' }
$runCases = if ($RunProfile -eq 'All') { @('SourceDebug', 'PublishedRelease') } else { @($RunProfile) }
$results = [Collections.Generic.List[object]]::new()
Push-Location $repo
try {
    foreach ($runCase in $runCases) {
        $configuration = if ($runCase -eq 'SourceDebug') { 'Debug' } else { 'Release' }
        $guiProject = 'ManualTests/AetherPackageConsumer/Gui/GuiConsumer.csproj'
        $runtimeProject = 'Examples/AetherRuntimeHost/AetherRuntimeHost.csproj'
        foreach ($target in @('gui', 'runtime')) {
            $project = if ($target -eq 'gui') { $guiProject } else { $runtimeProject }
            $command = @('build', $project, '-c', $configuration)
            if ($runCase -eq 'PublishedRelease') {
                $command = @('publish', $project, '-c', $configuration, '-o', (Join-Path $evidence "$runCase-$target"))
            }
            $log = Join-Path $evidence "$runCase-$target.log"
            & dotnet @command "-bl:$evidence/$runCase-$target-{}.binlog" *> $log
            if ($LASTEXITCODE -ne 0) { throw "SDK operation failed. See $log" }
        }
        $gui = if ($runCase -eq 'SourceDebug') { Join-Path $repo 'ManualTests/AetherPackageConsumer/Gui/bin/Debug/net10.0/GuiConsumer.exe' }
            else { Join-Path $evidence "$runCase-gui/GuiConsumer.exe" }
        $runtime = if ($runCase -eq 'SourceDebug') { Join-Path $repo 'Examples/AetherRuntimeHost/bin/Debug/net10.0/AetherRuntimeHost.exe' }
            else { Join-Path $evidence "$runCase-runtime/AetherRuntimeHost.exe" }
        $reportDirectory = Join-Path $evidence "$runCase-report"
        $start = [Diagnostics.ProcessStartInfo]::new($gui)
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        foreach ($argument in @($runtime, $reportDirectory, $composition, $provider, $scenario)) { $start.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            if ($InputMode -ne 'Routed') { Write-Host "Follow $reportDirectory/stage.json in the native window ($InputMode)." }
            if (!$process.WaitForExit(960000)) { $process.Kill($true); $process.WaitForExit(); throw "$runCase numeric acceptance timed out." }
            [IO.File]::WriteAllText((Join-Path $evidence "$runCase.stdout.log"), $stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText((Join-Path $evidence "$runCase.stderr.log"), $stderr.GetAwaiter().GetResult())
            if ($process.ExitCode -ne 0) { throw "$runCase numeric acceptance failed; inspect failure.txt." }
        } finally { $process.Dispose() }
        $report = Get-Content (Join-Path $reportDirectory 'report.json') -Raw | ConvertFrom-Json
        if (!$report.Passed -or !$report.NativeWindowCreated -or !$report.WindowCompleted -or
            $report.ActiveUiSessions -ne 0 -or $report.GpuResources -ne 0 -or $report.GpuHandles -ne 0 -or
            @($report.Children).Count -ne 0 -or @($report.RuntimeReports).Count -ne 0 -or
            !$report.Workflow.StableControls -or @($report.Workflow.Proofs | Where-Object { !$_.Passed }).Count -ne 0) {
            throw "$runCase did not prove native numeric behavior and cleanup."
        }
        $results.Add([pscustomobject]@{ Profile = $runCase; Composition = $composition; Provider = $provider; Input = $InputMode
            Report = $reportDirectory; Passed = $true; Proofs = @($report.Workflow.Proofs).Count })
    }
    $sources = @('ManualTests/AetherPackageConsumer/Gui/Program.cs', 'ManualTests/AetherPackageConsumer/Gui/NumericWorkflow.cs',
        'ManualTests/AetherPackageConsumer/VerifyNumeric.ps1', 'TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs',
        'TrueMoon.Alloy.Platform.Silk/WindowPointerState.cs', 'TrueMoon.Alloy.Tests/WindowPointerStateTests.cs',
        'TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj') | ForEach-Object {
        [pscustomobject]@{ Path = $_; Sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    }
    [pscustomobject]@{ Completed = $true; Cases = $results; Sources = $sources } | ConvertTo-Json -Depth 10 |
        Set-Content (Join-Path $evidence 'verification.json') -Encoding utf8
    $results | Format-Table -AutoSize
} finally { Pop-Location }
