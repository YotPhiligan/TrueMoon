#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ProbeAssembly,
    [Parameter(Mandatory)][string] $BuildDirectory,
    [Parameter(Mandatory)][string] $ScenarioPath,
    [Parameter(Mandatory)][string] $ScenePath,
    [Parameter(Mandatory)][string] $EvidenceDirectory,
    [switch] $LegacyScene
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$output = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory.' }
[void][IO.Directory]::CreateDirectory($output)
$probe = (Resolve-Path -LiteralPath $ProbeAssembly).Path
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$scenario = (Resolve-Path -LiteralPath $ScenarioPath).Path
$scene = (Resolve-Path -LiteralPath $ScenePath).Path
$reports = @{}
foreach ($profile in 'windowed', 'headless') {
    $directory = Join-Path $output $profile
    $arguments = @($probe, '--scenario', $scenario, $build, $scene, $directory)
    if ($LegacyScene) { $arguments += '--legacy-scene' }
    if ($profile -eq 'headless') { $arguments += '--headless' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "$profile exited with $LASTEXITCODE. See $directory." }
    $reports[$profile] = Get-Content -LiteralPath (Join-Path $directory 'report.json') -Raw | ConvertFrom-Json
}

$windowed = $reports.windowed
$headless = $reports.headless
foreach ($key in 'scenarioHash', 'sceneHash', 'buildId', 'hostContractId') {
    if (!$windowed.$key -or $windowed.$key -cne $headless.$key) { throw "Input identity differs for $key." }
}
if ($windowed.exitCode -ne 0 -or $headless.exitCode -ne 0 -or
    $windowed.profile -cne 'Windowed' -or $headless.profile -cne 'Headless' -or
    $windowed.state -cne 'Stopped' -or $headless.state -cne 'Stopped' -or
    !$windowed.windowClosed -or !$windowed.surfaceReleased -or $null -ne $headless.surfaceReleased -or
    $null -ne $headless.windowClosed) {
    throw 'Unexpected scenario profile or cleanup result.'
}
$headlessPoints = @{}
foreach ($point in $headless.checkpoints) { $headlessPoints.Add($point.requested.id, $point) }
if ($windowed.checkpoints.Count -eq 0 -or $windowed.checkpoints.Count -ne $headlessPoints.Count) {
    throw 'Checkpoint counts differ.'
}
$points = foreach ($point in $windowed.checkpoints) {
    $id = $point.requested.id
    $other = $headlessPoints[$id]
    if (!$other -or !$point.passed -or !$other.passed) { throw "Missing or failed checkpoint $id." }
    foreach ($key in 'tick', 'input', 'hudStatus', 'observationTick', 'observationVersion', 'observationChecks') {
        $left = ConvertTo-Json -InputObject $point.$key -Compress -Depth 12
        $right = ConvertTo-Json -InputObject $other.$key -Compress -Depth 12
        if ($left -cne $right) { throw "Different $key at checkpoint $id." }
    }
    if ($point.frameAvailability -cne 'Captured' -or $point.renderedTick -ne $point.tick -or
        $other.frameAvailability -cne 'UnavailableHeadless' -or $null -ne $other.file -or $null -ne $other.renderedTick) {
        throw "Unexpected frame availability at checkpoint $id."
    }
    $png = Join-Path (Join-Path $output windowed) $point.file
    if ((Get-FileHash -LiteralPath $png).Hash -cne $point.sha256) { throw "Frame hash mismatch at checkpoint $id." }
    [ordered]@{
        id = $id
        tick = $point.tick
        input = $point.input
        observations = @($point.observationChecks | ForEach-Object { $_.actual })
        windowedFrame = $point.frameAvailability
        headlessFrame = $other.frameAvailability
    }
}
if (Get-ChildItem -LiteralPath (Join-Path $output headless) -Filter '*.png' -File) {
    throw 'The headless profile emitted a frame.'
}
[ordered]@{
    formatVersion = 1
    passed = $true
    scenarioHash = $headless.scenarioHash
    sceneHash = $headless.sceneHash
    buildId = $headless.buildId
    checkpoints = @($points)
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'comparison.json') -Encoding utf8
