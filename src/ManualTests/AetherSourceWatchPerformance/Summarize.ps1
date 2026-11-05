param([Parameter(Mandatory)][string]$Verification)
$ErrorActionPreference='Stop'
$manifestPath=(Resolve-Path -LiteralPath $Verification).Path
$directory=Split-Path -Parent $manifestPath
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (!$manifest.Completed -or $manifest.Failure -or $manifest.Cases.Count -ne 6) { throw 'A completed six-case matrix is required.' }
function Median($values) { $sorted=@($values | Sort-Object); if (!$sorted.Count) { throw 'No samples.' }; if ($sorted.Count % 2) { return $sorted[[int][Math]::Floor($sorted.Count/2)] }; return ($sorted[$sorted.Count/2-1]+$sorted[$sorted.Count/2])/2 }
function F([double]$number,[int]$places=2) { $number.ToString("F$places",[Globalization.CultureInfo]::InvariantCulture) }
$sourceChecks=0
foreach($input in $manifest.Inputs) { if ((Get-FileHash -LiteralPath $input.Path -Algorithm SHA256).Hash -ne $input.Sha256) { throw "Input drift: $($input.Path)" }; $sourceChecks++ }
$reportHashes=[Collections.Generic.List[object]]::new()
$rows=foreach($case in $manifest.Cases) {
    if ($case.ExitCode -ne 0 -or $case.TimedOut) { throw "Invalid exit: $($case.Name)" }
    $result=Get-Content -LiteralPath $case.Report -Raw | ConvertFrom-Json
    if (!$result.Completed -or $result.SourceDrift.Count -ne 0 -or $result.StoppedSnapshot.State -ne 'Stopped' -or $result.StoppedSnapshot.WatcherCount -ne 0 -or $result.Build.Trees -ne 4) { throw "Incomplete evidence: $($case.Name)" }
    $steady=@($result.Passes | Where-Object Kind -eq steady)
    if ($steady.Count -ne 3 -or $result.Passes.Count -ne 4) { throw 'Expected initial + three steady passes.' }
    foreach($pass in $steady) {
        if ($pass.Snapshot.State -eq 'ObservedUnchanged' -and $pass.Metrics.ReadBytes -lt $result.Build.Bytes) { throw 'Same pass did not read all descriptor bytes.' }
    }
    $same=@($steady | Where-Object {$_.Snapshot.State -eq 'ObservedUnchanged'})
    $budgetMet=$same.Count -eq 3 -and $result.BudgetExceededPasses -eq 0
    if ($budgetMet -and (!$result.ContentChange.SameLength -or !$result.ContentChange.SameTimestamp -or $result.ContentChange.Result.Snapshot.State -ne 'Changed')) { throw 'Missing content-change acceptance.' }
    if ($result.NoBuildIdle.ReadBytes -ne 0 -or ($result.ChangedIdle -and $result.ChangedIdle.ReadBytes -ne 0)) { throw 'Idle window read source bytes.' }
    $reportHashes.Add(@{Path=$case.Report;Sha256=(Get-FileHash -LiteralPath $case.Report -Algorithm SHA256).Hash})
    [pscustomobject]@{
        Case=$case.Name; Files=$result.Build.Files; TreeFiles=$result.Build.TreeFiles; Trees=$result.Build.Trees; Probes=$result.Build.Probes; SourceMiB=$result.Build.Bytes/1MB;
        Mode=$result.StableSnapshot.Mode; Watchers=$result.StableSnapshot.WatcherCount; Reason=$result.StableSnapshot.Reason;
        SteadySamples=$steady.Count; SameSamples=$same.Count; BudgetMet=$budgetMet; BudgetExceededPasses=$result.BudgetExceededPasses;
        ScanLowerMedianMilliseconds=(Median $steady.ScanLowerMilliseconds); ScanUpperMedianMilliseconds=(Median $steady.ScanUpperMilliseconds);
        ScanUpperMaximumMilliseconds=($steady.ScanUpperMilliseconds | Measure-Object -Maximum).Maximum;
        CycleMedianMilliseconds=(Median $steady.Metrics.ElapsedMilliseconds); CpuMedianMilliseconds=(Median $steady.Metrics.CpuMilliseconds);
        CpuOneCoreMedianPercent=(Median $steady.Metrics.CpuOneCorePercent); AllocationMedianMiB=(Median $steady.Metrics.AllocatedBytes)/1MB;
        ReadMedianMiB=(Median $steady.Metrics.ReadBytes)/1MB; OtherOperationsMedian=(Median $steady.Metrics.OtherOperations);
        PeakPrivateMiB=($result.Passes.Metrics.PeakPrivateBytes | Measure-Object -Maximum).Maximum/1MB;
        PeakWorkingSetMiB=($result.Passes.Metrics.PeakWorkingSetBytes | Measure-Object -Maximum).Maximum/1MB;
        NoBuildPrivateMiB=$result.NoBuildIdle.PeakPrivateBytes/1MB; NoBuildCpuMilliseconds=$result.NoBuildIdle.CpuMilliseconds;
        ChangedIdleCpuMilliseconds=$result.ChangedIdle.CpuMilliseconds; ChangedIdleAllocationKiB=$result.ChangedIdle.AllocatedBytes/1KB;
        DetectionMilliseconds=$result.ContentChange.PublicationMilliseconds; CloseMilliseconds=$result.CloseMilliseconds;
        SamplerMaximumGapMilliseconds=($steady.SamplerMaxGapMilliseconds | Measure-Object -Maximum).Maximum;
        Gen0Collections=($steady.Metrics.Gen0 | Measure-Object -Sum).Sum; Gen1Collections=($steady.Metrics.Gen1 | Measure-Object -Sum).Sum; Gen2Collections=($steady.Metrics.Gen2 | Measure-Object -Sum).Sum;
        Report=$case.Report; RunDirectory=$result.RunDirectory
    }
}
$summary=[ordered]@{Completed=$true; Verification=$manifestPath; Environment=$manifest.Environment; Sdk=$manifest.Sdk;
    SourceHashChecks=$sourceChecks; SourceDrift=0; SteadyPasses=($rows.SteadySamples | Measure-Object -Sum).Sum; SamePasses=($rows.SameSamples | Measure-Object -Sum).Sum;
    BudgetExceededPasses=($rows.BudgetExceededPasses | Measure-Object -Sum).Sum; Cases=$rows; Reports=$reportHashes;
    Analysis=@{Path=$PSCommandPath;Sha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash}}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$directory/summary.json" -Encoding utf8
$markdown=[Collections.Generic.List[string]]::new()
$markdown.Add('# Observer performance — measured 2026-11-04')
$markdown.Add('')
$markdown.Add('Release/net10.0, six sequential isolated processes. Actual frozen compiler descriptors; ordinary 10 s polling after completion, 5 s cooperative scan budget. Three steady samples per row; initial pass retained separately in raw reports.')
$markdown.Add('')
$markdown.Add('| Case | Files | MiB | Median scan bounds ms | Median CPU ms/cycle | CPU % of one core | Alloc MiB/cycle | Logical read MiB/cycle | Change latency ms |')
$markdown.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach($row in $rows) { $markdown.Add("| $($row.Case) | $($row.Files) | $(F $row.SourceMiB) | $(F $row.ScanLowerMedianMilliseconds)–$(F $row.ScanUpperMedianMilliseconds) | $(F $row.CpuMedianMilliseconds) | $(F $row.CpuOneCoreMedianPercent) | $(F $row.AllocationMedianMiB) | $(F $row.ReadMedianMiB) | $(F $row.DetectionMilliseconds) |") }
$markdown.Add('')
$markdown.Add('| Case | NoBuild private MiB | Sampled private peak MiB | Sampled working-set peak MiB | Max sampler gap ms | GC 0/1/2 across steady samples | Close/drain ms |')
$markdown.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach($row in $rows) { $markdown.Add("| $($row.Case) | $(F $row.NoBuildPrivateMiB) | $(F $row.PeakPrivateMiB) | $(F $row.PeakWorkingSetMiB) | $(F $row.SamplerMaximumGapMilliseconds) | $($row.Gen0Collections)/$($row.Gen1Collections)/$($row.Gen2Collections) | $(F $row.CloseMilliseconds) |") }
$markdown.Add('')
$markdown.Add("Completed cases: $($rows.Count). Steady Same passes: $($summary.SamePasses)/$($summary.SteadyPasses). Budget-exceeded passes: $($summary.BudgetExceededPasses). Source/harness/loaded-framework input hashes: $sourceChecks, drift 0. All observer owners returned Stopped/watcher count 0; each process exited normally. NoBuild and Changed idle read bytes: 0.")
$markdown.Add('')
$markdown.Add('CPU/allocation/IO windows include the whole process and sampler, plus the ordinary polling wait. CPU percentage is for one core, not the whole machine. NoBuild is a short startup calibration; Changed idle is post-work calibration. Do not subtract these short windows as an exact observer CPU estimate. Private/working-set peaks are sampled every 100 ms, include retained setup/build memory, and can miss shorter peaks. Scan durations are snapshot-derived bounds with actual scheduler gaps, not exact method instrumentation. File caches were warmed by fixture generation/build; this is not cold-disk acceptance. ReadTransfer is logical IO, not physical disk traffic.')
$markdown.Add('')
$markdown.Add('Raw reports, descriptor provenance, case commands, CPU/OS/SDK and input hashes are in verification.json and the per-case report.json. Reproduce with ManualTests/AetherSourceWatchPerformance/Run.ps1, then Summarize.ps1 -Verification <run>/verification.json. The harness does not change production scheduling limits; input hashes identify the measured implementation. This matrix does not claim native UI, packaged consumer, network, unit/full-solution or hardware/release acceptance.')
$markdown | Set-Content -LiteralPath "$directory/REPORT.md" -Encoding utf8
$rows | Select-Object Case,Files,SourceMiB,ScanUpperMedianMilliseconds,CpuOneCoreMedianPercent,DetectionMilliseconds,BudgetMet | ConvertTo-Json -Depth 3
