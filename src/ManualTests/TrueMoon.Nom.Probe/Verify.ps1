[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[ValidateSet('All','Core','Kv','Fault','Maintenance','Retention','Backup','Performance','Capacity','VmProtocol')][string]$Profile='All')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(![OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64'){throw 'Nom proof profile is Windows x64.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-Nom-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$project=Join-Path $PSScriptRoot 'TrueMoon.Nom.Probe.csproj'
$inputs=@(Get-ChildItem -LiteralPath $PSScriptRoot,(Join-Path $workspace 'TrueMoon.Nom'),(Join-Path $workspace 'TrueMoon.Contracts') -Recurse -File | Where-Object { $_.Extension -in '.cs','.ps1','.csproj','.template' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
$inputs+=Get-Item -LiteralPath (Join-Path $workspace 'Directory.Build.props'),(Join-Path $workspace 'Directory.Packages.props')
$summary=[ordered]@{Completed=$false;RunDirectory=$run;Project=$project;ProofProfile=$Profile;Sources=@($inputs | ForEach-Object{[pscustomobject]@{Path=$_.FullName;Sha256=(Get-FileHash $_.FullName).Hash}});Commands=@();Profiles=@();Failure=$null}
function Invoke-Dotnet([string]$name,[string[]]$arguments,[int]$seconds=60){
    if($arguments[0] -in @('restore','build','publish')){
        $arguments+="-bl:$evidence/$name-{}.binlog"
        $arguments+="-p:DirectoryPackagesPropsPath=$(Join-Path $run 'Directory.Packages.props')"
        $arguments+='-p:UseArtifactsOutput=true',"-p:ArtifactsPath=$(Join-Path $run 'artifacts')"
    }
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$run;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in $arguments){$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        if(!$process.Start()){throw "Start failed $name"};$started=$true
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit($seconds*1000)){$process.Kill($true);if(!$process.WaitForExit(10000)){throw "Failed to reap $name"};throw "Timeout $name"}
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode}
        if($process.ExitCode -ne 0){throw "Failed $name; see $evidence"}
        Write-Host "$name passed"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);if(!$process.WaitForExit(10000)){throw "Cleanup did not reap $name"}};$process.Dispose()}
}
function Assert-Output([string]$directory){
    if(@(Get-ChildItem -LiteralPath $directory -Recurse -File -Filter 'LiteDB.dll').Count){throw 'Removed backend binary in output'}
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.deps.json'){
        $deps=Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json -AsHashtable
        if(@($deps.libraries.Keys|Where-Object{$_ -match '^LiteDB/'}).Count){throw 'Removed backend in runtime graph'}
    }
}
function Run-Profiles([string]$name,[string]$assembly){
    Assert-Output ([IO.Path]::GetDirectoryName($assembly))
    foreach($proof in @('Core','Kv','Fault','Maintenance','Retention','Backup','Performance','Capacity','VmProtocol')){
        if($proof -eq 'VmProtocol' -and $Profile -ne 'VmProtocol'){continue}
        if($proof -eq 'Performance' -and ($Profile -ne 'Performance' -or $name -ne 'Release')){continue}
        if($proof -eq 'Capacity' -and $Profile -ne 'Capacity'){continue}
        if($Profile -ne 'All' -and $Profile -ne $proof){continue}
        $report=Join-Path $evidence "$name-$proof.json"
        if($proof -eq 'VmProtocol'){
            Invoke-Dotnet "$name-$proof" @($assembly,'vm-protocol-proof',$report) 180
            $result=Get-Content $report -Raw|ConvertFrom-Json
            if(!$result.Completed -or $result.Passed.Count -ne 10 -or $result.StartedChildren -ne 7 -or $result.ReapedChildren -ne 7 -or $result.VmCrashTested -or $result.HardwarePowerLossTested){throw 'Incomplete host VM protocol proof'}
            $summary.Profiles+=@{Name=$name;Proof=$proof;Report=$report;Groups=10};continue
        }
        if($proof -eq 'Performance'){
            Invoke-Dotnet "$name-$proof" @($assembly,'performance-proof',$report,'all') 300
            $result=Get-Content $report -Raw|ConvertFrom-Json
            if(!$result.Completed -or $result.Library.Module -ne 'TrueMoon.Nom' -or (Get-FileHash -LiteralPath $result.Library.Path).Hash -ne $result.Library.Sha256){throw 'Incomplete performance profile'}
            $summary.Profiles+=@{Name=$name;Proof=$proof;Report=$report;Groups=1};continue
        }
        if($proof -eq 'Capacity'){
            Invoke-Dotnet "$name-$proof" @($assembly,'capacity-proof',$report) 300
            $result=Get-Content $report -Raw|ConvertFrom-Json
            if(!$result.Completed -or @($result.Passed).Count -ne 4 -or $result.Library.Module -ne 'TrueMoon.Nom' -or (Get-FileHash -LiteralPath $result.Library.Path).Hash -ne $result.Library.Sha256){throw 'Incomplete capacity profile'}
            $summary.Profiles+=@{Name=$name;Proof=$proof;Report=$report;Groups=4};continue
        }
        $arguments=if($proof -eq 'Kv'){@($assembly,'kv-proof',$report)}elseif($proof -eq 'Fault'){@($assembly,'fault-proof',$report)}elseif($proof -eq 'Maintenance'){@($assembly,'maintenance-proof',$report)}elseif($proof -eq 'Retention'){@($assembly,'retention-proof',$report)}elseif($proof -eq 'Backup'){@($assembly,'backup-proof',$report)}else{@($assembly,$report)}
        Invoke-Dotnet "$name-$proof" $arguments $(if($proof -eq 'Fault'){120}else{60})
        $result=Get-Content $report -Raw|ConvertFrom-Json
        $expected=if($proof -eq 'Core'){18}elseif($proof -eq 'Kv'){6}elseif($proof -eq 'Fault'){41}elseif($proof -eq 'Maintenance'){27}elseif($proof -eq 'Retention'){12}else{16}
        if(!$result.Completed -or @($result.Passed).Count -ne $expected -or $result.Library.Module -ne 'TrueMoon.Nom'){throw 'Incomplete proof'}
        if((Get-FileHash -LiteralPath $result.Library.Path).Hash -ne $result.Library.Sha256){throw 'Loaded module hash mismatch'}
        $summary.Profiles+=@{Name=$name;Proof=$proof;Report=$report;Groups=$expected}
    }
}
try{
    # Build the real projects with an empty feed/cache; suppress unrelated legacy CLI tool references.
    '<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup></Project>'|Set-Content (Join-Path $run 'Directory.Packages.props')
    $config=Join-Path $run 'NuGet.Config'
    '<configuration><packageSources><clear/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>'|Set-Content $config -Encoding utf8
    Invoke-Dotnet 'restore' @('restore',$project,'--configfile',$config,'--packages',(Join-Path $run 'packages'),'-p:NuGetAudit=false')
    $graphs=@(Get-ChildItem -LiteralPath (Join-Path $run 'artifacts') -Recurse -Filter 'project.assets.json' | ForEach-Object {
        $graph=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Keys|Where-Object{$graph.libraries[$_].type -eq 'package'}).Count){throw 'Unexpected external package in own KV graph'}
        [pscustomobject]@{Project=$graph.project.restore.projectPath;Libraries=@($graph.libraries.Keys|Sort-Object)}
    })
    if(@($graphs|Where-Object Project -eq $project).Count -ne 1){throw 'Missing actual probe restore graph'}
    $summary.RestoredGraphs=$graphs
    Invoke-Dotnet 'Debug' @('build',$project,'--no-restore','-c','Debug')
    $configs=@(Get-ChildItem -LiteralPath (Join-Path $run 'artifacts/bin') -Recurse -Filter 'TrueMoon.Nom.Probe.runtimeconfig.json')
    if($configs.Count -ne 1){throw 'Missing source runtime output'}
    Run-Profiles 'Debug' (Join-Path $configs[0].DirectoryName 'TrueMoon.Nom.Probe.dll')
    $publish=Join-Path $run 'publish'
    Invoke-Dotnet 'publish' @('publish',$project,'--no-restore','-c','Release','--self-contained','false','-o',$publish)
    Run-Profiles 'Release' (Join-Path $publish 'TrueMoon.Nom.Probe.dll')
    foreach($file in $summary.Sources){if((Get-FileHash -LiteralPath $file.Path).Hash -ne $file.Sha256){throw 'Source changed during run'}}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 10|Set-Content (Join-Path $evidence 'verification.json') -Encoding utf8}
