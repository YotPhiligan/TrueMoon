#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh regression evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$report=[ordered]@{Completed=$false;Stage='ARCH3.9';BuildProfile=$profile;Commands=@();Builds=@();Tests=@();Sources=@();Failure=$null}
function Save-Report{$report|ConvertTo-Json -Depth 15|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Regression([string]$name,[string[]]$arguments){
    $arguments+="-bl:$evidence/$name-{}.binlog"
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$workspace;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(1800000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode};Save-Report
        if($process.ExitCode -ne 0){throw "$name failed"}
        if(!(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")){throw 'Missing regression binlog.'}
        Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
try{
    foreach($path in (& rg --files $workspace -g '*.cs' -g '*.csproj' -g '*.props' -g '*.targets' -g 'global.json')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    $projects=@(& rg --files $workspace -g '*Tests.csproj' | Sort-Object)
    if($projects.Count -ne 14){throw 'Review the changed test-project discovery before accepting the matrix.'}
    foreach($configuration in @('Debug','Release')){
        $name='solution-'+$configuration
        Invoke-Regression $name @('build','TrueMoon.slnx','-c',$configuration,'--no-incremental','--verbosity','minimal')
        $content=[IO.File]::ReadAllText("$evidence/$name.stdout.log")
        $errors=[regex]::Match($content,'(?:Ошибок:|Error\(s\))\s*(\d+)');$warnings=[regex]::Match($content,'Предупреждений:\s*(\d+)')
        $report.Builds+=@{Configuration=$configuration;Log="$evidence/$name.stdout.log";Errors=$(if($errors.Success){[int]$errors.Groups[1].Value}else{0});Warnings=$(if($warnings.Success){[int]$warnings.Groups[1].Value}else{-1})}
    }
    # Aether owns an exclusive SDK lease during nested compiler probes: all SDK commands remain sequential.
    foreach($configuration in @('Debug','Release')){
        foreach($project in $projects){
            $module=[IO.Path]::GetFileNameWithoutExtension($project);$name=$module+'-'+$configuration
            Invoke-Regression $name @('test',$project,'-c',$configuration,'--no-build','--no-restore','--logger',"trx;LogFileName=$name.trx",'--results-directory',$evidence)
            [xml]$trx=Get-Content -LiteralPath "$evidence/$name.trx" -Raw
            $counts=$trx.TestRun.ResultSummary.Counters
            if([int]$counts.total -eq 0 -or [int]$counts.failed -ne 0 -or [int]$counts.total -ne [int]$counts.passed){throw "Discovery/failure/skip mismatch: $name"}
            $report.Tests+=@{Module=$module;Configuration=$configuration;Passed=[int]$counts.passed;Failed=[int]$counts.failed;Skipped=[int]$counts.notExecuted;Path="$evidence/$name.trx";Sha256=(Get-FileHash -LiteralPath "$evidence/$name.trx").Hash};Save-Report
        }
    }
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'C#/build input drift since regression began.'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
