param(
    [string]$Executable = 'ManualTests/AlloyVulkanTest/bin/Release/net10.0/AlloyVulkanTest.exe',
    [string]$OutputDirectory = 'TestResults/AlloyPerformance',
    [ValidateRange(1, 256)][int]$Rows = 32,
    [ValidateRange(2, 10000)][int]$Samples = 500,
    [ValidateRange(1, 10000)][int]$Warmup = 100,
    [ValidateRange(0, 5000)][int]$WarmupMilliseconds = 500,
    [ValidateRange(1, 10)][int]$Runs = 2,
    [switch]$Validation
)
$ErrorActionPreference = 'Stop'
if ($Samples % 2 -ne 0) { throw 'Samples must be even so every append is removed.' }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $repository
try {
    $executablePath = (Resolve-Path -LiteralPath $Executable).Path
    $outputPath = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    $label = Get-Date -Format 'yyyyMMdd-HHmmss'
    $environmentFile = Join-Path $outputPath "$label-environment.json"
    $environmentInfo = [ordered]@{
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
        OS = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber
        CPU = @(Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors)
        GPU = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, DriverDate)
        SDK = (& dotnet --version)
        GitHead = (& git rev-parse HEAD)
        GitBranch = (& git branch --show-current)
        WorktreeDirty = (@(& git status --porcelain).Count -ne 0)
        PowerScheme = (& powercfg /getactivescheme | Out-String).Trim()
        MeasurementValidation = [bool]$Validation
        Notes = 'No affinity/priority/power/GC settings changed. Independent sequential processes; same arguments; source/binary/font/asset hashes in every report.'
    }
    $environmentInfo | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $environmentFile -Encoding utf8
    for ($run = 1; $run -le $Runs; $run++) {
        $output = Join-Path $outputPath "$label-run$run.json"
        $log = Join-Path $outputPath "$label-run$run.log"
        $baselineArguments = @('--settings-baseline', '--baseline-rows', $Rows, '--baseline-samples', $Samples,
            '--baseline-warmup', $Warmup, '--baseline-warmup-ms', $WarmupMilliseconds,
            '--baseline-output', $output, '--baseline-environment', $environmentFile)
        if ($Validation) { $baselineArguments += '--validation' }
        & $executablePath @baselineArguments *> $log
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 25; throw "Baseline run$run failed: exit $LASTEXITCODE" }
        $report = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        if ($report.Configuration -ne 'Release') { throw 'Performance baseline must use Release; Debug is for correctness only.' }
        if ($report.Cases.Count -ne 12 -or $report.Scene.Samples -ne $Samples -or $report.Scene.Rows -ne $Rows) { throw 'Unexpected scene/case/sample count.' }
        foreach ($case in $report.Cases) {
            if ($case.Samples -ne $Samples -or $case.Frames.Count -ne $Samples) { throw 'Missing raw samples.' }
            if ($case.Calls.Resize -ne 0 -or $case.Calls.SurfaceCreates -ne 0) { throw 'Measured batch recreated UI surfaces.' }
            if ($case.Mode -eq 'static') {
                if ($case.Calls.Measure -ne 0 -or $case.Calls.Arrange -ne 0 -or $case.Calls.Draw -ne 0) { throw 'Static UI performs layout/draw.' }
                if (($case.Frames.Update.AllocatedBytes | Measure-Object -Sum).Sum -ne 0) { throw 'Static Update allocated managed memory.' }
            }
        }
        Get-Content -LiteralPath $log -Tail 2
    }
    "Environment: $environmentFile"
}
finally { Pop-Location }
