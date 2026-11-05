param(
    [Parameter(Mandatory)][string]$TraceTool,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$BaselineVerification='TestResults/ObserverPerformance-20261104/Final/verification.json',
    [ValidateSet('files','bytes')][string[]]$Profiles=@('files','files','bytes')
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory,$workspace)
$baseline=Get-Content -LiteralPath ([IO.Path]::GetFullPath($BaselineVerification,$workspace)) -Raw|ConvertFrom-Json
$tool=(Resolve-Path -LiteralPath $TraceTool).Path
$dotnet=(Get-Command dotnet -CommandType Application).Source
if(!$baseline.Completed){throw 'A completed baseline is required.'}
if(Test-Path -LiteralPath "$evidence/verification.json"){throw 'Choose a new output directory.'}
New-Item -ItemType Directory -Path $evidence -Force|Out-Null
foreach($input in $baseline.Inputs){if((Get-FileHash -LiteralPath $input.Path -Algorithm SHA256).Hash -ne $input.Sha256){throw "Baseline input drift: $($input.Path)"}}

function Launch([string]$file,[string[]]$arguments,[string]$prefix){
    $info=[Diagnostics.ProcessStartInfo]::new($file)
    $info.WorkingDirectory=$workspace;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $arguments){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info
    $output=[IO.FileStream]::new("$prefix.stdout.log",[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite,1,[IO.FileOptions]::Asynchronous)
    $errorOutput=[IO.FileStream]::new("$prefix.stderr.log",[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite,1,[IO.FileOptions]::Asynchronous)
    try{
        if(!$process.Start()){throw 'Could not start owned process.'}
        return @{Process=$process;Output=$output;ErrorOutput=$errorOutput;OutTask=$process.StandardOutput.BaseStream.CopyToAsync($output);ErrorTask=$process.StandardError.BaseStream.CopyToAsync($errorOutput);Arguments=$arguments;File=$file;StartedUtc=[DateTimeOffset]::UtcNow}
    }catch{$output.Dispose();$errorOutput.Dispose();$process.Dispose();throw}
}
function Finish($owned,[int]$seconds){
    if(!$owned.Process.WaitForExit($seconds*1000)){$owned.Process.Kill($true);$owned.Process.WaitForExit();throw 'Owned process exceeded profiling deadline.'}
    $null=$owned.OutTask.GetAwaiter().GetResult();$null=$owned.ErrorTask.GetAwaiter().GetResult();$owned.Output.Flush();$owned.ErrorOutput.Flush()
    if($owned.Process.ExitCode -ne 0){throw "Process failed ($($owned.Process.ExitCode)): $($owned.File)"}
}
function Release($owned){
    if($null -eq $owned){return}
    try{if(!$owned.Process.HasExited){$owned.Process.Kill($true);$owned.Process.WaitForExit()};$null=$owned.OutTask.GetAwaiter().GetResult();$null=$owned.ErrorTask.GetAwaiter().GetResult()}
    finally{$owned.Output.Dispose();$owned.ErrorOutput.Dispose();$owned.Process.Dispose()}
}
function ReadLive([string]$path){
    $stream=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    try{
        $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8,$true,1024,$true)
        try{return $reader.ReadToEnd()}finally{$reader.Dispose()}
    }finally{$stream.Dispose()}
}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
$manifest=[ordered]@{Completed=$false;StartedUtc=[DateTimeOffset]::UtcNow;Baseline=$BaselineVerification;BaselineInputs=$baseline.Inputs;
    Script=@{Path=$PSCommandPath;Sha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash};
    Tool=@{Path=$tool;Version=(& $tool --version);Sha256=(Get-FileHash -LiteralPath $tool -Algorithm SHA256).Hash};
    Environment=$baseline.Environment;ElevatedToken=$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator);
    Cases=[Collections.Generic.List[object]]::new();Failure=$null}
try{
    $number=0
    foreach($profile in $Profiles){
        $number++;$name="$profile-$number";$directory=Join-Path $evidence $name
        New-Item -ItemType Directory -Path $directory|Out-Null
        $target=$null;$collector=$null
        try{
            Write-Output "$([DateTimeOffset]::UtcNow.ToString('O')) Starting $name"
            $target=Launch $dotnet @($baseline.Harness,'--profile',$profile,'--notifications','false','--output',"$directory/benchmark") "$directory/target"
            $wait=[Diagnostics.Stopwatch]::StartNew()
            while($true){
                $stageLog=ReadLive "$directory/target.stdout.log"
                if($stageLog -like '*Steady polling pass 1*'){break}
                if($target.Process.HasExited -or $wait.Elapsed.TotalSeconds -gt 160){throw 'Target did not reach the post-Build/initial-pass attach point.'}
                Start-Sleep -Milliseconds 100
            }
            # Discover only the owned target, never attach to a guessed/reused PID or persist unrelated command lines.
            $targetPid=$target.Process.Id
            $discovered=@(& $tool ps|Where-Object{$_ -match "^\s*$targetPid\s"})
            if($LASTEXITCODE -ne 0 -or $discovered.Count -ne 1 -or $target.Process.HasExited){throw 'Owned target was not confirmed by dotnet-trace ps.'}
            $discovered|Set-Content -LiteralPath "$directory/process-discovery.log" -Encoding utf8
            $trace=Join-Path $directory observer.nettrace
            $arguments=@('collect','--process-id',"$targetPid",'--profile','dotnet-common,dotnet-sampled-thread-time','--buffersize','64','--duration','00:00:30','--format','Speedscope','--output',$trace)
            $collector=Launch $tool $arguments "$directory/collector"
            Finish $collector 90
            $speedscope=@(Get-ChildItem -LiteralPath $directory -Filter '*.speedscope.json' -File)
            if(!(Test-Path -LiteralPath $trace)-or (Get-Item -LiteralPath $trace).Length -le 0 -or $speedscope.Count -ne 1){throw 'Trace or Speedscope conversion missing.'}
            Finish $target 100
            $result=Get-Content -LiteralPath "$directory/benchmark/report.json" -Raw|ConvertFrom-Json
            if(!$result.Completed -or $result.BudgetExceededPasses -ne 0 -or $result.SourceDrift.Count -ne 0 -or $result.StoppedSnapshot.WatcherCount -ne 0){throw 'Profiled target behavior failed.'}
            & $tool report $trace topN --number 80 --inclusive --verbose *> "$directory/top-inclusive.txt"
            if($LASTEXITCODE -ne 0){throw 'Trace report failed.'}
            & $tool report $trace topN --number 80 --verbose *> "$directory/top-exclusive.txt"
            if($LASTEXITCODE -ne 0){throw 'Trace report failed.'}
            $manifest.Cases.Add(@{Name=$name;Profile=$profile;TargetPid=$targetPid;TargetStartUtc=$target.Process.StartTime.ToUniversalTime().ToString('O');
                TargetArguments=$target.Arguments;CollectorArguments=$arguments;CollectorStartUtc=$collector.StartedUtc;TargetExitCode=$target.Process.ExitCode;CollectorExitCode=$collector.Process.ExitCode;
                Trace=$trace;TraceSha256=(Get-FileHash -LiteralPath $trace -Algorithm SHA256).Hash;Speedscope=$speedscope[0].FullName;SpeedscopeSha256=(Get-FileHash -LiteralPath $speedscope[0].FullName -Algorithm SHA256).Hash;
                BenchmarkReport="$directory/benchmark/report.json"})
            Write-Output "$([DateTimeOffset]::UtcNow.ToString('O')) Completed $name"
        }finally{Release $collector;Release $target}
    }
    foreach($input in $baseline.Inputs){if((Get-FileHash -LiteralPath $input.Path -Algorithm SHA256).Hash -ne $input.Sha256){throw "Input drift: $($input.Path)"}}
    $manifest.Completed=$true
}catch{$manifest.Failure=$_.ToString();throw}
finally{$manifest.FinishedUtc=[DateTimeOffset]::UtcNow;$manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
