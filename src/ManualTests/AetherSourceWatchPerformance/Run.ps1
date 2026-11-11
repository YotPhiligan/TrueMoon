param(
    [string]$EvidenceDirectory = "TestResults/ObserverPerformance-$(Get-Date -Format yyyyMMdd-HHmmss)",
    [ValidateSet('control','files','bytes')][string[]]$Profiles = @('control','files','bytes'),
    [switch]$SkipBuild
)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))


$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory, $workspace)
if (Test-Path -LiteralPath (Join-Path $evidence verification.json)) { throw 'Choose a new evidence directory; existing runs are immutable.' }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$dotnet = (Get-Command dotnet -CommandType Application).Source
if (!$SkipBuild) {
    & $dotnet build "$PSScriptRoot/AetherSourceWatchPerformance.csproj" -c Release --verbosity quiet "-bl:$evidence/harness-build-{}.binlog" *> "$evidence/build.log"
    if ($LASTEXITCODE -ne 0) { throw "Harness build failed. See $evidence/build.log" }
}
$harness = Join-Path $PSScriptRoot "bin/Release/$($trueMoonBuildProfile.TestFramework)/AetherSourceWatchPerformance.dll"
if (!(Test-Path -LiteralPath $harness)) { throw 'Build the Release harness first.' }
$inputs = @(
    Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object Extension -in @('.cs','.csproj','.ps1')
    Get-ChildItem -LiteralPath "$workspace/TrueMoon.Aether.Editor/Editing" -Filter 'EditorSource*.cs' -File
    Get-ChildItem -LiteralPath "$workspace/TrueMoon.Aether/Builds" -Filter '*.cs' -File
    Get-ChildItem -LiteralPath (Split-Path -Parent $harness) -Filter 'TrueMoon*.dll' -File
    Get-Item -LiteralPath $harness
) | Sort-Object FullName -Unique | ForEach-Object { @{ Path=$_.FullName; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
$run = [ordered]@{
    Completed=$false; StartedUtc=[DateTimeOffset]::UtcNow; Sdk=(& $dotnet --version);
    Environment=@{ OS=(Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,TotalVisibleMemorySize); CPU=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed) };
    Harness=$harness; Inputs=$inputs; Cases=[Collections.Generic.List[object]]::new(); Failure=$null
}
try {
    foreach ($profile in $Profiles) {
        foreach ($notifications in @($false,$true)) {
            $mode = if ($notifications) {'Notifications'} else {'Polling'}
            $name = "$profile-$mode"; $caseDirectory = Join-Path $evidence $name
            $arguments = @($harness,'--profile',$profile,'--notifications',$notifications.ToString().ToLowerInvariant(),'--output',$caseDirectory)
            $info = [Diagnostics.ProcessStartInfo]::new($dotnet)
            $info.WorkingDirectory=$workspace; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
            $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
            foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
            Write-Output "$([DateTimeOffset]::UtcNow.ToString('O')) Starting $name"
            $child = [Diagnostics.Process]::new(); $child.StartInfo=$info
            $clock = [Diagnostics.Stopwatch]::StartNew(); $timedOut=$false
            try {
                if (!$child.Start()) { throw "Could not start $name" }
                $stdout=$child.StandardOutput.ReadToEndAsync(); $stderr=$child.StandardError.ReadToEndAsync()
                while (!$child.WaitForExit(1000)) {
                    if ($clock.Elapsed.TotalSeconds -gt 240) { $timedOut=$true; $child.Kill($true); $child.WaitForExit(); break }
                }
                [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult())
                [IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
                $case=[ordered]@{ Name=$name; Arguments=$arguments; ProcessId=$child.Id; ExitCode=$child.ExitCode; TimedOut=$timedOut; Milliseconds=$clock.Elapsed.TotalMilliseconds; Report="$caseDirectory/report.json" }
                $run.Cases.Add($case)
                if ($timedOut -or $child.ExitCode -ne 0) { throw "Case failed: $name; see stdout/stderr and report.json." }
                $result=Get-Content -LiteralPath $case.Report -Raw | ConvertFrom-Json
                if (!$result.Completed -or $result.SourceDrift.Count -ne 0 -or $result.StoppedSnapshot.WatcherCount -ne 0 -or $result.StoppedSnapshot.State -ne 'Stopped') { throw "Incomplete observer ownership proof: $name" }
                Write-Output "$([DateTimeOffset]::UtcNow.ToString('O')) Completed $name; files=$($result.Build.Files), bytes=$($result.Build.Bytes), budgetExceeded=$($result.BudgetExceededPasses)"
            } finally { $child.Dispose() }
        }
    }
    foreach ($input in $inputs) {
        if ((Get-FileHash -LiteralPath $input.Path -Algorithm SHA256).Hash -ne $input.Sha256) { throw "Input changed during matrix: $($input.Path)" }
    }
    $run.Completed=$true
} catch { $run.Failure=$_.ToString(); throw }
finally {
    $run.FinishedUtc=[DateTimeOffset]::UtcNow
    $run | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8
}
