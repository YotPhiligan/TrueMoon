[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))


$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'DurabilityVolume.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($EvidenceDirectory)){$EvidenceDirectory}else{Join-Path $workspace $EvidenceDirectory}));$token=[Guid]::NewGuid().ToString('N');$vhdPath=Join-Path $evidence "isolated-$token.vhdx"
Assert-DurabilityPath $workspace $evidence $vhdPath $token
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$state=[ordered]@{Completed=$false;ExecutionStatus='Running';AllPhysicalGatesClosed=$false;DiskFullStatus='NotRun';PhysicalDiskFullTested=$false;PowerLossStatus='Requires external isolated power-cut rig';PowerLossTested=$false;
    Head=(& git -C $workspace rev-parse HEAD);Administrator=$false;VhdPath=$vhdPath;Created=$false;MountAttempted=$false;Formatted=$false;Dismounted=$false;Commands=@();Sources=@();GuardProfiles=@();Profiles=@();ExpectedCases=@('staging','catalog-append','checkpoint','export','restore');Failure=$null;CleanupFailure=$null}
$sources=@(Get-Item $PSCommandPath,(Join-Path $PSScriptRoot 'DurabilityVolume.ps1'),(Join-Path $PSScriptRoot 'TestDurabilityGuards.ps1'),(Join-Path $PSScriptRoot 'PublicDurabilityConsumer.cs.template'))
$state.Sources=@($sources|ForEach-Object{@{Path=$_.FullName;Sha256=(Get-FileHash $_.FullName).Hash}})
$exitCode=1;$identifier=[Guid]::Empty;$stage='build';$vhd=$null
function Save-Result{$state|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $evidence 'durability.json') -Encoding utf8}
function Invoke-ConsumerDotnet([string]$Name,[string[]]$Arguments,[int]$Expected=0){
    if($Arguments[0] -in @('restore','build','publish')){$Arguments+="-bl:$evidence/$Name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WorkingDirectory=$consumer;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $cache 'http';$info.Environment['NUGET_FALLBACK_PACKAGES']=''
    foreach($argument in $Arguments){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        if(!$process.Start()){throw 'Consumer launch failed'};$started=$true;$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(180000)){throw "Timeout $Name"}
        [IO.File]::WriteAllText((Join-Path $evidence "$Name.stdout.log"),$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $evidence "$Name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $state.Commands+=@{Name=$Name;Arguments=$Arguments;ExitCode=$process.ExitCode;Reaped=$true}
        if($process.ExitCode -ne $Expected){throw "Consumer command failed: $Name"}
        if($Arguments[0] -in @('restore','build','publish') -and !(Get-ChildItem $evidence -Filter "$Name-*.binlog")){throw 'Missing unique binlog'}
    }finally{if($started -and !$process.HasExited){$process.Kill($true);if(!$process.WaitForExit(10000)){throw 'Consumer not reaped'}};$process.Dispose()}
}
function Current-OwnedDisk([switch]$Raw){
    $current=Get-VHD -Path $vhdPath -ErrorAction Stop;$disk=Get-Disk -Number $number -ErrorAction Stop
    Assert-DurabilityPath $workspace $evidence $vhdPath $token
    Assert-DurabilityDisk $current $disk $vhdPath $identifier $number $unique -Raw:$Raw
    return $disk
}
try{
    if(![OperatingSystem]::IsWindows()){throw 'Windows x64 is required.'}
    & (Join-Path $PSScriptRoot 'TestDurabilityGuards.ps1') -Report (Join-Path $evidence 'guards.json')
    & (Join-Path $PSScriptRoot 'VerifyPackages.ps1') -EvidenceDirectory ([IO.Path]::GetRelativePath($workspace,(Join-Path $evidence 'Delivery')))
    $delivery=Get-Content (Join-Path $evidence 'Delivery/verification.json') -Raw|ConvertFrom-Json
    if(!$delivery.Completed -or $delivery.Packages.Count -ne 4){throw 'Fresh package delivery prerequisite failed.'}
    $state.PackageVersion=$delivery.Version;$state.PackageProof=Join-Path $evidence 'Delivery/verification.json'
    $consumer=Join-Path ([IO.Path]::GetTempPath()) ('Nom-PhysicalConsumer-'+$token);[void][IO.Directory]::CreateDirectory($consumer);$state.ConsumerDirectory=$consumer
    $cache=Join-Path $consumer 'cache';$feed=Join-Path $evidence 'Delivery/packages';$config=Join-Path $consumer 'NuGet.Config';$escaped=[Security.SecurityElement]::Escape($feed)
    "<configuration><packageSources><clear/><add key='current' value='$escaped'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>"|Set-Content $config
    '<Project/>'|Set-Content (Join-Path $consumer 'Directory.Build.props');'<Project/>'|Set-Content (Join-Path $consumer 'Directory.Build.targets');'<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>'|Set-Content (Join-Path $consumer 'Directory.Packages.props')
    Copy-Item (Join-Path $workspace 'global.json') $consumer;Copy-Item (Join-Path $PSScriptRoot 'PublicDurabilityConsumer.cs.template') (Join-Path $consumer 'Program.cs')
    $project=Join-Path $consumer 'NomPhysicalConsumer.csproj';$version=$delivery.Version
    "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>$($trueMoonBuildProfile.Framework)</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><UseAppHost>false</UseAppHost></PropertyGroup><ItemGroup><PackageReference Include='TrueMoon.Nom' Version='$version'/><PackageReference Include='TrueMoon' Version='$version'/></ItemGroup></Project>"|Set-Content $project
    Invoke-ConsumerDotnet 'restore' @('restore',$project,'--configfile',$config,'--packages',$cache,'-p:NuGetAudit=false')
    $graph=Get-Content (Join-Path $consumer 'obj/project.assets.json') -Raw|ConvertFrom-Json -AsHashtable
    if($graph.libraries.Count -ne 4 -or @($graph.libraries.Values|Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or [IO.Path]::TrimEndingDirectorySeparator(@($graph.packageFolders.Keys)[0]) -ne $cache){throw 'Physical consumer graph isolation failed'}
    foreach($package in $delivery.Packages){$cached=Join-Path $cache ($package.Id.ToLowerInvariant()+"/$version/"+$package.Id.ToLowerInvariant()+".$version.nupkg");if((Get-FileHash $cached).Hash -ne $package.Sha256 -or (Get-FileHash $package.DeliveryPath).Hash -ne $package.Sha256){throw 'Physical consumer package bytes differ'}}
    $outputs=@{}
    foreach($profile in 'Debug','Release'){
        if($profile -eq 'Debug'){Invoke-ConsumerDotnet 'build-Debug' @('build',$project,'-c','Debug','--no-restore');$output=Join-Path $consumer "bin/Debug/$($trueMoonBuildProfile.TestFramework)"}
        else{$output=Join-Path $consumer 'publish';Invoke-ConsumerDotnet 'publish-Release' @('publish',$project,'-c','Release','--no-restore','--self-contained','false','-p:UseAppHost=false','-o',$output)}
        $expected=@($delivery.Packages|Where-Object Id -eq 'TrueMoon.Nom')[0].DllSha256
        if((Get-FileHash (Join-Path $output 'TrueMoon.Nom.dll')).Hash -ne $expected){throw 'Physical output Nom hash mismatch'}
        $dll=Join-Path $output 'NomPhysicalConsumer.dll';$report=Join-Path $evidence "consumer-guards-$profile.json"
        Invoke-ConsumerDotnet "consumer-guards-$profile" @($dll,'guard',$evidence,$token,$report)
        $proof=Get-Content $report -Raw|ConvertFrom-Json;if(!$proof.Completed -or $proof.PhysicalDiskFullTested -or $proof.PowerLossTested){throw 'Guard proof mismatch'}
        $state.GuardProfiles+=@{Profile=$profile;Checks=$proof.Checks.Count;Report=$report;ModuleSha256=$proof.ModuleSha256};$outputs[$profile]=$dll
    }
    $stage='volume-creation';$state.Administrator=([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    foreach($command in 'New-VHD','Mount-VHD','Get-VHD','Dismount-VHD','Get-Disk','Initialize-Disk','New-Partition','Format-Volume'){if(!(Get-Command $command -ErrorAction SilentlyContinue)){throw "Required volume capability unavailable: $command"}}
    Assert-DurabilityPath $workspace $evidence $vhdPath $token
    if(Test-Path -LiteralPath $vhdPath){throw 'Refusing to reuse a VHDX'}
    if(([IO.DriveInfo]::new([IO.Path]::GetPathRoot($vhdPath))).AvailableFreeSpace -lt 512MB){throw 'Host free-space guard: need512MiB, only128MiB VHD is created.'}
    $vhd=New-VHD -Path $vhdPath -Fixed -SizeBytes 128MB -ErrorAction Stop;$state.Created=$true;$identifier=[Guid]$vhd.DiskIdentifier
    $state.MountAttempted=$true;$mounted=Mount-VHD -Path $vhdPath -PassThru -ErrorAction Stop;$disks=@($mounted|Get-Disk -ErrorAction Stop)
    if($disks.Count -ne 1){throw 'Expected one owned attached virtual disk'};$number=[int]$disks[0].Number;$unique=[string]$disks[0].UniqueId
    $disk=Current-OwnedDisk -Raw
    $disk|Initialize-Disk -PartitionStyle GPT -ErrorAction Stop|Out-Null
    $disk=Current-OwnedDisk
    $partition=$disk|New-Partition -UseMaximumSize -AssignDriveLetter -ErrorAction Stop
    $disk=Current-OwnedDisk;Assert-DurabilityPartition $partition $disk $number
    $label='NOM7_'+$token.Substring(0,24)
    $partition|Format-Volume -FileSystem NTFS -NewFileSystemLabel $label -Confirm:$false -ErrorAction Stop|Out-Null;$state.Formatted=$true
    $volumeRoot=[string]$partition.DriveLetter+':\';$state.VolumeRoot=$volumeRoot
    [IO.File]::WriteAllText((Join-Path $volumeRoot '.nom-durability-owner.json'),(@{Token=$token;VolumeRoot=$volumeRoot;DiskIdentifier=$identifier;VhdPath=$vhdPath}|ConvertTo-Json))
    $stage='physical-cases'
    foreach($profile in 'Debug','Release'){
        $disk=Current-OwnedDisk;Assert-DurabilityPartition $partition $disk $number
        $report=Join-Path $evidence "physical-$profile.json";Invoke-ConsumerDotnet "physical-$profile" @($outputs[$profile],'disk-full',$volumeRoot,$token,$report)
        $proof=Get-Content $report -Raw|ConvertFrom-Json
        if(!$proof.Completed -or !$proof.PhysicalDiskFullTested -or $proof.Cases.Count -ne 5 -or $proof.PowerLossTested){throw 'Physical profile incomplete'}
        $state.Profiles+=@{Profile=$profile;Cases=5;Checks=$proof.Checks.Count;Report=$report;ModuleSha256=$proof.ModuleSha256}
    }
    $state.DiskFullStatus='Passed';$state.PhysicalDiskFullTested=$true;$state.ExecutionStatus='DiskFullPassed-PowerLossOpen';$exitCode=2
}catch{
    $state.Failure=$_.ToString();$state.ErrorId=$_.FullyQualifiedErrorId
    if($stage -eq 'volume-creation' -and !$state.Created){$state.ExecutionStatus='Blocked';$state.DiskFullStatus='Blocked';$exitCode=2}
    else{$state.ExecutionStatus='Failed';$state.DiskFullStatus='NotProven';$exitCode=1}
}finally{
    if($state.Created -and $state.MountAttempted){
        try{
            Assert-DurabilityPath $workspace $evidence $vhdPath $token
            $current=Get-VHD -Path $vhdPath -ErrorAction Stop
            if([Guid]$current.DiskIdentifier -ne $identifier){throw 'Cleanup VHD identity changed'}
            if($current.Attached){Dismount-VHD -Path $vhdPath -ErrorAction Stop};$state.Dismounted=$true
        }catch{$state.CleanupFailure=$_.ToString();$state.ExecutionStatus='CleanupFailed';$exitCode=1}
    }
    foreach($source in $state.Sources){if((Get-FileHash $source.Path).Hash -ne $source.Sha256){$state.Failure='Durability source drift';$state.ExecutionStatus='Failed';$exitCode=1}}
    Save-Result
}
Write-Output "Durability: $($state.ExecutionStatus); disk-full=$($state.DiskFullStatus); power-loss remains open. See $evidence/durability.json"
exit $exitCode
