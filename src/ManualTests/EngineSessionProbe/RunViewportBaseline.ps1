[CmdletBinding()]
param(
    [string]$RunDirectory,
    [ValidateSet('Debug', 'Release')][string[]]$Configurations = @('Debug', 'Release'),
    [ValidateRange(1, 5)][int]$Repetitions = 3,
    [ValidateRange(1, 30)][int]$WarmupSeconds = 2,
    [ValidateRange(1, 30)][int]$MeasurementSeconds = 5
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resultsRoot = Join-Path $workspace 'TestResults/ViewportBaseline'
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else { Join-Path $resultsRoot ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
if (!$run.StartsWith([IO.Path]::GetFullPath($resultsRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a directory within TestResults/ViewportBaseline.' }
if (Test-Path -LiteralPath $run) { throw 'Use a fresh RunDirectory.' }
[IO.Directory]::CreateDirectory($run) | Out-Null
$summary = [ordered]@{
    Completed = $false; StartedUtc = [DateTime]::UtcNow.ToString('O'); RunDirectory = $run
    WarmupSeconds = $WarmupSeconds; MeasurementSeconds = $MeasurementSeconds; Repetitions = $Repetitions
    Order = 'Alternating cpu/gpu then gpu/cpu per repetition; sequential isolated processes, one Editor window per case.'
    PerformanceAcceptance = 'Advisory budgets are fixed before the run; a breach is a measured result, not an invalid experiment.'
    Commands = @(); Cases = @(); SourceFiles = @(); Binaries = @(); Environment = $null; Failure = $null
}
# Written before builds/runs: a failed/partial experiment must remain visibly incomplete.
function Save-Summary { $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding UTF8 }
function Sources {
    $roots = @('TrueMoon.Aether','TrueMoon.Aether.Tests','ManualTests/EngineSessionProbe','ManualTests/Game.Logic',
        'TrueMoon','TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator','TrueMoon.Extensions.DependencyInjection',
        'TrueMoon.Enerit','TrueMoon.Enerit.Generator','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy',
        'TrueMoon.Alloy.Hosting','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Rendering.Skia','ManualTests/AlloyTest/Assets/Fonts')
    $files = @('Directory.Build.props','Directory.Packages.props','global.json') | ForEach-Object { Get-Item (Join-Path $workspace $_) }
    $files += foreach ($root in $roots) { Get-ChildItem (Join-Path $workspace $root) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.ps1','.props','.targets','.dll','.json','.ttf','.txt' } }
    @($files | Sort-Object FullName | ForEach-Object { [ordered]@{ Path = [IO.Path]::GetRelativePath($workspace, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
}
function Invoke-Owned([string]$name, [string]$file, [string[]]$arguments, [int]$timeout) {
    $info = [Diagnostics.ProcessStartInfo]::new($file)
    $info.WorkingDirectory = $workspace; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    # Measurement is explicitly without validation layers. No global/user environment is changed.
    [void]$info.Environment.Remove('TRUEMOON_GPU_VALIDATION')
    [void]$info.Environment.Remove('VK_INSTANCE_LAYERS')
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $finished = $process.WaitForExit($timeout * 1000)
        if (!$finished) { $process.Kill($true); $process.WaitForExit() }
        [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"), $stderr.GetAwaiter().GetResult())
        $summary.Commands += [ordered]@{ Name = $name; FileName = $file; Arguments = $arguments; ExitCode = $process.ExitCode; TimedOut = !$finished }
        Save-Summary
        if (!$finished -or $process.ExitCode) { throw "$name failed: exit $($process.ExitCode), timeout $(!$finished). See its logs." }
        Write-Output "$name completed"
    }
    finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
function Check-Binaries {
    foreach ($entry in $summary.Binaries) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -cne $entry.Sha256) { throw "Binary changed: $($entry.Path)" } }
}
Save-Summary
try {
    $summary.SourceFiles = Sources
    $summary.Environment = [ordered]@{
        OS = [Environment]::OSVersion.VersionString; ProcessArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        LogicalProcessors = [Environment]::ProcessorCount; QpcFrequency = [Diagnostics.Stopwatch]::Frequency
        VideoControllers = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion)
        ValidationEnabled = $false; GpuUtilizationAndVramAvailable = $false
    }
    Invoke-Owned 'sdk' 'dotnet' @('--version') 30
    $summary.Environment.Sdk = (Get-Content (Join-Path $run 'sdk.stdout.log') -Raw).Trim()
    foreach ($configuration in $Configurations) {
        if ($configuration -eq 'Debug') {
            Invoke-Owned 'build-Debug' 'dotnet' @('build',(Join-Path $PSScriptRoot 'EngineSessionProbe.csproj'),'--configuration','Debug','--verbosity','quiet') 240
            $binaryDirectory = Join-Path $PSScriptRoot 'bin/Debug/net10.0'
        }
        else {
            $binaryDirectory = Join-Path $run 'publish'
            Invoke-Owned 'publish-Release' 'dotnet' @('publish',(Join-Path $PSScriptRoot 'EngineSessionProbe.csproj'),'--configuration','Release','--output',$binaryDirectory,'--verbosity','quiet') 240
        }
        $summary.Binaries += @(Get-ChildItem $binaryDirectory -Recurse -File | Where-Object { $_.Extension -in '.dll','.exe','.json','.ttf','.txt' } |
            Sort-Object FullName | ForEach-Object { [ordered]@{ Configuration = $configuration; Path = $_.FullName; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
        for ($repeat = 1; $repeat -le $Repetitions; $repeat++) {
            $modes = if ($repeat % 2) { @('cpu','gpu') } else { @('gpu','cpu') }
            foreach ($mode in $modes) {
                Check-Binaries
                $name = "$configuration-$repeat-$mode"; $caseDirectory = Join-Path $run $name
                $timeout = 120 + 3 * ($WarmupSeconds + $MeasurementSeconds)
                Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--viewport-baseline',$mode,$caseDirectory,"$WarmupSeconds","$MeasurementSeconds") $timeout
                $reportPath = Join-Path $caseDirectory 'baseline-report.json'
                $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
                if (!$report.Completed -or $report.Mode -ne $mode -or $report.Windows.Count -ne 3 -or $report.PixelChecks -ne 3 -or
                    $report.GpuResources -ne 0 -or $report.GpuHandles -ne 0 -or $report.Mappings -ne 0 -or $report.ActiveUiSessions -ne 0 -or
                    $report.ImportReadbacks -ne 0 -or $report.GameLoadedInEditor -or $report.Children.Count -ne 2 -or
                    @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased -or $_.StandardError }).Count -or
                    @($report.Diagnostics | Where-Object { $_.Stage -eq 'Killing' -or $_.Stage -eq 'ProcessReleased' -and $_.ExitCode -ne 0 }).Count) { throw "$name has an incomplete/native faulted report." }
                if ($report.Artifacts.Count -ne 2 -or $report.Artifacts[0].BuildId -eq $report.Artifacts[1].BuildId -or $report.Artifacts[0].DllSha256 -ne $report.Artifacts[1].DllSha256) { throw "$name changed the compared game workload." }
                foreach ($rawPath in @('baseline-editor.json','baseline-runtime-1.json','baseline-runtime-2.json')) {
                    $raw = Get-Content -LiteralPath (Join-Path $caseDirectory $rawPath) -Raw | ConvertFrom-Json
                    if ($raw.Overflow -ne 0 -or $raw.Errors.Count -ne 0 -or $raw.Frequency -ne $summary.Environment.QpcFrequency) { throw "$name has an invalid raw trace." }
                }
                $summary.Cases += [ordered]@{ Configuration = $configuration; Repetition = $repeat; Mode = $mode; Report = "$name/baseline-report.json"; Sha256 = (Get-FileHash $reportPath).Hash }
                Save-Summary
            }
        }
    }
    if (($summary.SourceFiles | ConvertTo-Json -Depth 5 -Compress) -cne ((Sources) | ConvertTo-Json -Depth 5 -Compress)) { throw 'Sources changed during measurement.' }
    Check-Binaries
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add('# ENG4e CPU/GPU viewport baseline')
    $rows.Add('')
    $rows.Add("Warmup ${WarmupSeconds}s; measured ${MeasurementSeconds}s per extent; $Repetitions repetitions per mode/configuration. Sequential alternating order; validation disabled for measurements.")
    $rows.Add('CPU-observed instrumented reference workload; no physical-display latency, GPU timestamps, utilization or VRAM claim. Unavailable values are n/a. Private memory is not VRAM.')
    $rows.Add('')
    $rows.Add('| Configuration | Repeat | Mode | Stage | Produced/received/presented | FPS | Produce→present p50/p95 ms | Production p95 ms | Retained submit p95 ms | Editor/runtime allocated MiB/s |')
    $rows.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |')
    function Number($value) { if ($null -eq $value) { 'n/a' } else { ([double]$value).ToString('F2', [Globalization.CultureInfo]::InvariantCulture) } }
    foreach ($case in $summary.Cases) {
        $report = Get-Content (Join-Path $run $case.Report) -Raw | ConvertFrom-Json
        foreach ($result in $report.Windows) {
            $rows.Add("| $($case.Configuration) | $($case.Repetition) | $($case.Mode) | $($result.Window.Name) | $($result.Produced)/$($result.Received)/$($result.Presented) | $(Number $result.PresentedFps) | $(Number $result.ProduceToPresent.P50Ms)/$(Number $result.ProduceToPresent.P95Ms) | $(Number $result.Stages.Production.P95Ms) | $(Number $result.Stages.RetainedCopySubmission.P95Ms) | $(Number $result.EditorResources.AllocatedMiBPerSecond)/$(Number $result.RuntimeResources[0].AllocatedMiBPerSecond) |")
        }
    }
    $rows.Add('')
    $rows.Add('Raw QPC stage samples and cumulative200ms process counters are stored in each case directory. Per-case reports contain warmup-free production cohorts, advisory budget results, lifecycle durations and import timings. Unreceived/unpresented counts overlap; they must not be summed. Resource rates cover the explicitly reported inner polling interval. Retained-copy times measure CPU submission, not GPU execution; gpu-use includes owner scheduling and observed completion.')
    [IO.File]::WriteAllLines((Join-Path $run 'REPORT.md'), $rows)
    $summary.Completed = $true
}
catch { $summary.Failure = $_.ToString(); throw }
finally { $summary.FinishedUtc = [DateTime]::UtcNow.ToString('O'); Save-Summary }
