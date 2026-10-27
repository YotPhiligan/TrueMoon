[CmdletBinding()]
param(
    [string]$RunDirectory,
    [string[]]$Configurations = @('Debug', 'Release'),
    [switch]$IncludeViewport,
    [switch]$IncludeGpuCapabilities,
    [switch]$IncludeGpuImport,
    [switch]$IncludeGpuSurface,
    [switch]$IncludeGpuViewport,
    [switch]$IncludeEditorGpu,
    [switch]$IncludeScene,
    [switch]$IncludeInput,
    [switch]$IncludeRoom,
    [switch]$IncludeInteraction,
    [string]$GpuValidationLayerDirectory,
    [string[]]$Scenarios = @('repeat','stale','bad-hello','bad-scene','startup-timeout','startup-cancel','command-timeout','command-cancel','stop-pending','crash','disconnect','heartbeat','forced-stop','hang-stop','launch-failure','parent-exit','app-stop','build-artifacts','checkpoint','replacement')
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resultsRoot = Join-Path $workspace 'TestResults/EngineSessions'
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else { Join-Path $resultsRoot ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
if (!$run.StartsWith([IO.Path]::GetFullPath($resultsRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a directory within TestResults/EngineSessions.' }
if (Test-Path -LiteralPath $run) { throw 'Use a fresh RunDirectory.' }
[IO.Directory]::CreateDirectory($run) | Out-Null
$summary = [ordered]@{ Completed = $false; RunDirectory = $run; Commands = @(); Cases = @(); SourceFiles = @(); Binaries = @(); Failure = $null }
if ($GpuValidationLayerDirectory) {
    $env:VK_LAYER_PATH = [IO.Path]::GetFullPath($GpuValidationLayerDirectory)
    $summary.GpuValidation = @(foreach ($name in @('VkLayer_khronos_validation.dll','VkLayer_khronos_validation.json')) {
        $file = Get-Item -LiteralPath (Join-Path $env:VK_LAYER_PATH $name) -ErrorAction Stop
        [ordered]@{ Path = $file.FullName; Sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash }
    })
    $env:TRUEMOON_GPU_VALIDATION = '1'
}
function Sources {
    $roots = @('TrueMoon.Aether','TrueMoon.Aether.Tests','ManualTests/EngineSessionProbe','ManualTests/Game.Logic','TrueMoon','TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit','TrueMoon.Enerit.Generator','TrueMoon.Mithril')
    if ($IncludeViewport -or $IncludeGpuCapabilities -or $IncludeGpuImport -or $IncludeGpuSurface -or $IncludeGpuViewport -or $IncludeEditorGpu -or $IncludeScene -or $IncludeInput -or $IncludeRoom -or $IncludeInteraction) { $roots += @('TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Hosting','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Rendering.Skia','ManualTests/AlloyTest/Assets/Fonts') }
    $files = @('Directory.Build.props','Directory.Packages.props','global.json') | ForEach-Object { Get-Item (Join-Path $workspace $_) }
    $files += foreach ($root in $roots) { Get-ChildItem (Join-Path $workspace $root) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.ps1','.props','.targets','.dll','.json','.ttf','.txt','.vert','.frag','.spv' } }
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
            Invoke-Owned 'build-Debug' 'dotnet' @('build',(Join-Path $PSScriptRoot 'EngineSessionProbe.csproj'),'--configuration','Debug','--verbosity','quiet',"-bl:$(Join-Path $run 'build-Debug-{}.binlog')") 180
            $binaryDirectory = Join-Path $PSScriptRoot 'bin/Debug/net10.0'
        }
        elseif ($configuration -eq 'Release') {
            $binaryDirectory = Join-Path $run 'publish'
            Invoke-Owned 'publish-Release' 'dotnet' @('publish',(Join-Path $PSScriptRoot 'EngineSessionProbe.csproj'),'--configuration','Release','--output',$binaryDirectory,'--verbosity','quiet',"-bl:$(Join-Path $run 'publish-Release-{}.binlog')") 180
        }
        else { throw 'Only Debug apphost and published Release are defined.' }
        $summary.Binaries += @(Get-ChildItem $binaryDirectory -File | Where-Object { $_.Extension -in '.dll','.exe','.json' } | ForEach-Object { [ordered]@{ Configuration = $configuration; Path = $_.FullName; Sha256 = (Get-FileHash $_.FullName).Hash } })
        if ($IncludeViewport -or $IncludeGpuCapabilities -or $IncludeGpuImport -or $IncludeGpuSurface -or $IncludeGpuViewport -or $IncludeEditorGpu -or $IncludeScene -or $IncludeInput -or $IncludeRoom -or $IncludeInteraction) {
            $summary.Binaries += @(Get-ChildItem (Join-Path $binaryDirectory 'Assets/Fonts'),(Join-Path $binaryDirectory 'truemoon-native') -Recurse -File | ForEach-Object { [ordered]@{ Configuration = $configuration; Path = $_.FullName; Sha256 = (Get-FileHash $_.FullName).Hash } })
        }
        if ($IncludeGpuCapabilities) {
            $name = "$configuration-gpu-capabilities"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--gpu-capabilities',$caseDirectory) 60
            $report = Get-Content (Join-Path $caseDirectory 'gpu-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or !$report.CpuTransportSelected -or $report.ImportTested -or !$report.EditorPreserved -or $report.Mappings -ne 0 -or $report.ActiveUiSessions -ne 0 -or $report.Children.Count -ne 1 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'gpu-capabilities'; Report = "$name/gpu-report.json"; Passed = $true }
        }
        if ($IncludeInteraction) {
            $name = "$configuration-interaction"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--interaction-proof',$caseDirectory) 200
            $report = Get-Content (Join-Path $caseDirectory 'interaction-report.json') -Raw | ConvertFrom-Json
            if (!$report.Passed -or $report.Cases.Count -ne 10 -or $report.Captures.Count -ne 9 -or $report.ImportReadbacks -ne 0 -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.Mappings -ne 0 -or $report.UiSessions -ne 0 -or $report.Children.Count -ne 4 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            foreach ($capture in $report.Captures) {
                if ($capture.ObjectPixels.Count -ne 3 -or @($capture.ObjectPixels | Where-Object { $_ -le 20 -or $_ -ge 1500 }).Count) { throw "$name object pixel proof incomplete." }
                if ((Get-FileHash -LiteralPath (Join-Path $caseDirectory $capture.Image)).Hash -cne $capture.Sha256) { throw "$name image fingerprint mismatch." }
            }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'interaction'; Report = "$name/interaction-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeRoom) {
            $name = "$configuration-room"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--room-proof',$caseDirectory) 200
            $report = Get-Content (Join-Path $caseDirectory 'room-report.json') -Raw | ConvertFrom-Json
            if (!$report.Passed -or $report.Captures.Count -ne 5 -or $report.ImportReadbacks -ne 0 -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.Mappings -ne 0 -or $report.UiSessions -ne 0 -or $report.Children.Count -ne 2 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            foreach ($capture in $report.Captures) {
                if ($capture.ObjectPixels.Count -ne 3 -or @($capture.ObjectPixels | Where-Object { $_ -le 20 -or $_ -ge 1500 }).Count) { throw "$name object pixel proof incomplete." }
                if ((Get-FileHash -LiteralPath (Join-Path $caseDirectory $capture.Image)).Hash -cne $capture.Sha256) { throw "$name image fingerprint mismatch." }
            }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'room'; Report = "$name/room-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeScene) {
            $name = "$configuration-scene"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--scene-proof',$caseDirectory) 200
            $report = Get-Content (Join-Path $caseDirectory 'scene-report.json') -Raw | ConvertFrom-Json
            if (!$report.Passed -or $report.Captures.Count -ne 5 -or $report.ImportReadbacks -ne 0 -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.Mappings -ne 0 -or $report.UiSessions -ne 0 -or $report.Children.Count -ne 2 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            foreach ($capture in $report.Captures) {
                if ((Get-FileHash -LiteralPath (Join-Path $caseDirectory $capture.Image)).Hash -cne $capture.Sha256) { throw "$name image fingerprint mismatch." }
            }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'scene'; Report = "$name/scene-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeInput) {
            $name = "$configuration-input"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--input-proof',$caseDirectory) 200
            $report = Get-Content (Join-Path $caseDirectory 'input-report.json') -Raw | ConvertFrom-Json
            if (!$report.Passed -or !$report.SenderCompleted -or $report.Cases.Count -ne 15 -or $report.RawKeyCallbacks -lt 20 -or $report.Captures.Count -ne 5 -or $report.SentPackets -lt 10 -or $report.ImportReadbacks -ne 0 -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.Mappings -ne 0 -or $report.UiSessions -ne 0 -or $report.Children.Count -ne 2 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            foreach ($capture in $report.Captures) {
                if ((Get-FileHash -LiteralPath (Join-Path $caseDirectory $capture.Image)).Hash -cne $capture.Sha256) { throw "$name image fingerprint mismatch." }
            }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'input'; Report = "$name/input-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeGpuImport) {
            $name = "$configuration-gpu-import"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--gpu-import',$caseDirectory) 200
            $report = Get-Content (Join-Path $caseDirectory 'gpu-import-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or $report.Cases.Count -ne 14 -or !$report.EditorPreserved -or !$report.CpuViewportDefault -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.ActiveUiSessions -ne 0 -or $report.Children.Count -ne 8 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'gpu-import'; Report = "$name/gpu-import-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeGpuSurface) {
            $name = "$configuration-gpu-surface"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--gpu-surface',$caseDirectory) 200
            $report = Get-Content (Join-Path $caseDirectory 'gpu-surface-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or $report.Cases.Count -ne 21 -or $report.Frames.Count -ne 80 -or !$report.EditorPreserved -or !$report.CpuViewportDefault -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.ActiveUiSessions -ne 0 -or $report.Children.Count -ne 12 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'gpu-surface'; Report = "$name/gpu-surface-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeGpuViewport) {
            $name = "$configuration-gpu-viewport"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--gpu-viewport',$caseDirectory) 180
            $report = Get-Content (Join-Path $caseDirectory 'gpu-viewport-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or $report.Cases.Count -ne 17 -or $report.Captures.Count -ne 14 -or $report.Copies -ne 20 -or $report.ImportReadbacks -ne 0 -or !$report.EditorPreserved -or !$report.CpuViewportDefault -or $report.Resources -ne 0 -or $report.Handles -ne 0 -or $report.ActiveUiSessions -ne 0 -or $report.Children.Count -ne 6 -or @($report.Children | Where-Object { !$_.Reaped -or !$_.ResourcesReleased }).Count) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'gpu-viewport'; Report = "$name/gpu-viewport-report.json"; Passed = $true; ValidationRequested = $report.ValidationRequested }
        }
        if ($IncludeEditorGpu) {
            $name = "$configuration-editor-gpu"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--editor-gpu-proof',$caseDirectory) 330
            $report = Get-Content (Join-Path $caseDirectory 'editor-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or $report.Transport -ne 'Gpu' -or $report.ImportReadbacks -ne 0 -or $report.GpuResources -ne 0 -or $report.GpuHandles -ne 0 -or $report.GpuCopies -lt 20 -or $report.Cycles.Count -ne 20 -or !$report.ChildrenReleased -or $report.Mappings -ne 0 -or $report.ActiveUiSessions -ne 0 -or !$report.EditorPreserved -or $report.GameLoadedInEditor -or $report.PixelChecks -lt 20 -or $report.MaxLive -ne 1) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'editor-gpu'; Report = "$name/editor-report.json"; Passed = $true }
        }
        if ($IncludeViewport) {
            $name = "$configuration-viewport"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--viewport',$caseDirectory) 180
            $report = Get-Content (Join-Path $caseDirectory 'viewport-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or !$report.ChildrenReleased -or $report.Mappings -ne 0 -or $report.ActiveUiSessions -ne 0 -or $report.PixelChecks -lt 20 -or $report.Copy.Samples -eq 0) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'viewport'; Report = "$name/viewport-report.json"; Passed = $true }
            $name = "$configuration-editor-proof"; $caseDirectory = Join-Path $run $name
            Invoke-Owned $name (Join-Path $binaryDirectory 'EngineSessionProbe.exe') @('--editor-proof',$caseDirectory) 300
            $report = Get-Content (Join-Path $caseDirectory 'editor-report.json') -Raw | ConvertFrom-Json
            if (!$report.Completed -or $report.Cycles.Count -ne 20 -or !$report.ChildrenReleased -or $report.Mappings -ne 0 -or $report.ActiveUiSessions -ne 0 -or !$report.EditorPreserved -or $report.GameLoadedInEditor -or $report.PixelChecks -lt 20 -or $report.MaxLive -ne 1) { throw "$name report incomplete." }
            $summary.Cases += [ordered]@{ Configuration = $configuration; Scenario = 'editor-proof'; Report = "$name/editor-report.json"; Passed = $true }
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
    foreach ($binary in $summary.Binaries) {
        if ((Get-FileHash -LiteralPath $binary.Path).Hash -cne $binary.Sha256) { throw "Binary changed during verification: $($binary.Path)" }
    }
    $summary.Completed = $true
}
catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'summary.json') }
