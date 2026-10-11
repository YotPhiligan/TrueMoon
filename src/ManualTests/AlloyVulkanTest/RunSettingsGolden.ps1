[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [string] $ValidationLayerPath
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else {
    Join-Path $workspace ('TestResults/AlloyGolden/run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
}
$resultsRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults')) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($resultsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must be inside this workspace TestResults.' }
if (Test-Path -LiteralPath $output) { throw 'Use a new OutputDirectory.' }
if ($ValidationLayerPath -and -not (Test-Path -LiteralPath (Join-Path $ValidationLayerPath 'VkLayer_khronos_validation.json'))) { throw 'ValidationLayerPath must contain VkLayer_khronos_validation.json.' }
New-Item -ItemType Directory -Path $output | Out-Null
$project = Join-Path $PSScriptRoot 'AlloyVulkanTest.csproj'
$fixtures = Join-Path $PSScriptRoot 'Golden/Settings'
$fixtureBefore = @(Get-ChildItem -LiteralPath $fixtures -File | Sort-Object Name | ForEach-Object { $_.Name + '=' + (Get-FileHash -LiteralPath $_.FullName).Hash })
$summary = [ordered]@{ Passed = $false; CreatedUtc = [DateTime]::UtcNow; SDK = (& dotnet --version); Runs = @(); Error = $null }
function Invoke-Build([string] $name, [string[]] $arguments) {
    & dotnet @arguments *> (Join-Path $output ($name + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "$name failed: exit $LASTEXITCODE. See log." }
}
function Invoke-Probe([string] $configuration, [string] $assembly) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $workspace; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in @($assembly, '--settings-golden', '--golden-output', (Join-Path $output $configuration))) { $start.ArgumentList.Add($argument) }
    if ($ValidationLayerPath) { $start.Environment['VK_LAYER_PATH'] = [IO.Path]::GetFullPath($ValidationLayerPath); $start.ArgumentList.Add('--validation') }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start; $started = $false
    try {
        $started = $process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(180000)) { $process.Kill($true); $process.WaitForExit(); throw "$configuration native probe timed out." }
        $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $output "$configuration.log")
        $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $output "$configuration.err.log")
        if ($process.ExitCode -ne 0) { throw "$configuration native probe failed: exit $($process.ExitCode)." }
        $report = Get-Content -LiteralPath (Join-Path $output "$configuration/report.json") -Raw | ConvertFrom-Json
        if (-not $report.Passed -or $report.Comparisons.Count -ne 180 -or @($report.Comparisons | Where-Object { -not $_.Result.Passed }).Count -ne 0) { throw "$configuration report is incomplete/failed." }
        if ($ValidationLayerPath -and -not $report.ValidationPassedIncludingTeardown) { throw "$configuration validation incomplete." }
        $summary.Runs += [ordered]@{ Configuration = $configuration; ExitCode = $process.ExitCode; Comparisons = $report.Comparisons.Count; Report = "$configuration/report.json" }
    }
    finally { if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
Push-Location $workspace
try {
    Invoke-Build 'build-debug' @('build', $project, '--configuration', 'Debug', '--verbosity', 'quiet')
    Invoke-Probe 'Debug' (Join-Path $PSScriptRoot 'bin/Debug/net10.0/AlloyVulkanTest.dll')
    $publish = Join-Path $output 'publish'
    Invoke-Build 'publish-release' @('publish', $project, '--configuration', 'Release', '--output', $publish, '--verbosity', 'quiet')
    Invoke-Probe 'Release' (Join-Path $publish 'AlloyVulkanTest.dll')
    $fixtureAfter = @(Get-ChildItem -LiteralPath $fixtures -File | Sort-Object Name | ForEach-Object { $_.Name + '=' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    if (@(Compare-Object $fixtureBefore $fixtureAfter).Count) { throw 'Normal verification modified source fixtures.' }
    $summary.Passed = $true
    Write-Output "Settings golden Debug/Release: 360 full-frame comparisons passed; fixtures unchanged. $output"
}
catch { $summary.Error = $_.ToString(); throw }
finally {
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'summary.json')
    Pop-Location
}
