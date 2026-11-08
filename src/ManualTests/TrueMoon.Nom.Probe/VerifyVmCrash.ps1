[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$BaseOsImage,
    [PSCredential]$GuestCredential,
    [ValidateRange(60,300)][int]$BootTimeoutSeconds=180
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'VmCrashProtocol.ps1')
. (Join-Path $PSScriptRoot 'DurabilityVolume.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($EvidenceDirectory)){$EvidenceDirectory}else{Join-Path $workspace $EvidenceDirectory}))
Assert-NomVmPath $workspace $evidence
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh VM evidence directory'}
[void][IO.Directory]::CreateDirectory($evidence)
$token=[Guid]::NewGuid().ToString('N');$vmRoot=Join-Path $evidence 'vm';$osDisk=Join-Path $vmRoot 'os-child.vhdx';$dataDisk=Join-Path $vmRoot 'data.vhdx'
$owner=[ordered]@{Token=$token;Name='NomCrash-'+$token;VmId=[Guid]::Empty;Root=$vmRoot;OsDisk=$osDisk;DataDisk=$dataDisk}
$result=[ordered]@{Schema=1;Completed=$false;ExecutionStatus='Preparing';VmCrashTested=$false;HardwarePowerLossTested=$false;HardwarePowerLossRequired=$false;
    Scope='Owned Hyper-V guest TurnOff only; host/device power excluded by user';Head=(& git -C $workspace rev-parse HEAD);Owner=$owner;Trials=@();Failure=$null;CleanupFailure=$null;Sources=@()}
$inputs=@(Get-Item $PSCommandPath,(Join-Path $PSScriptRoot 'VmCrashProtocol.ps1'),(Join-Path $PSScriptRoot 'DurabilityVolume.ps1'))
$result.Sources=@($inputs|ForEach-Object{@{Path=$_.FullName;Sha256=(Get-FileHash $_.FullName).Hash}})
$exitCode=1;$parentLease=$null;$dataCreated=$false;$dataMountAttempted=$false;$vmCreated=$false;$stage='protocol';$protocol=$null
function Save-Result{$result|ConvertTo-Json -Depth 18|Set-Content -LiteralPath (Join-Path $evidence 'vm-crash.json') -Encoding utf8}
function Current-VM {
    $v=Get-VM -Id $owner.VmId -ErrorAction Stop
    Assert-NomVmIdentity $v $owner @(Get-VMHardDiskDrive -VM $v) @(Get-VMCheckpoint -VM $v) @(Get-VMNetworkAdapter -VM $v)
    return $v
}
function Host-Budget {
    if(([IO.DriveInfo]::new([IO.Path]::GetPathRoot($evidence))).AvailableFreeSpace -lt 2GB){throw 'VM host free-space guard: need2GiB reserve'}
    $files=@(Get-ChildItem -LiteralPath $evidence -Recurse -File);if($files.Count -gt 4096 -or ($files|Measure-Object Length -Sum).Sum -gt 8GB){throw 'VM artifact budget8GiB/4096files exceeded'}
}
function Guest([scriptblock]$Script,[object[]]$Arguments=@(),[int]$Seconds=20){
    Host-Budget;[void](Current-VM)
    $job=Invoke-Command -VMId $owner.VmId -Credential $GuestCredential -ScriptBlock $Script -ArgumentList $Arguments -AsJob -ErrorAction Stop
    try{if(!(Wait-Job $job -Timeout $Seconds)){throw 'Bounded PowerShell Direct call timed out'};Receive-Job $job -ErrorAction Stop}
    finally{if($job.State -eq 'Running'){Stop-Job $job};Remove-Job $job -Force}
}
function Boot-Guest {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while($watch.Elapsed.TotalSeconds -lt $BootTimeoutSeconds){
        try{return Guest { $ErrorActionPreference='Stop'; (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('O') } @() 10}
        catch{if($watch.Elapsed.TotalSeconds -ge $BootTimeoutSeconds){throw};Start-Sleep -Milliseconds 500}
    }
    throw 'Guest did not become ready within boot deadline'
}
function Turn-OffOwnedVM([switch]$Measured) {
    $vm=Current-VM
    if($Measured -and $vm.State -ne 'Running'){throw 'Measured cut requires a running owned VM'}
    if($vm.State -ne 'Off'){
        $job=Stop-VM -VM $vm -TurnOff -Force -Confirm:$false -AsJob -ErrorAction Stop
        try{if(!(Wait-Job $job -Timeout 30)){throw 'Owned TurnOff operation timed out'};Receive-Job $job -ErrorAction Stop|Out-Null}
        finally{if($job.State -eq 'Running'){Stop-Job $job};Remove-Job $job -Force}
    }
    $watch=[Diagnostics.Stopwatch]::StartNew()
    do{$vm=Current-VM;if($vm.State -eq 'Off'){return};Start-Sleep -Milliseconds 200}while($watch.Elapsed.TotalSeconds -lt 30)
    throw 'Owned VM did not reach Off'
}
function Copy-RawData([string]$Path){
    if((Current-VM).State -ne 'Off'){throw 'Raw data image requires actual VM Off'}
    Assert-NomVmPath $evidence $Path
    $input=[IO.FileStream]::new($dataDisk,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{$output=[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read);try{$input.CopyTo($output,65536);$output.Flush($true)}finally{$output.Dispose()}}finally{$input.Dispose()}
}
try{
    & (Join-Path $PSScriptRoot 'TestVmCrashGuards.ps1') -Report (Join-Path $evidence 'guards.json')
    & (Join-Path $PSScriptRoot 'Verify.ps1') -Profile VmProtocol -EvidenceDirectory ([IO.Path]::GetRelativePath($workspace,(Join-Path $evidence 'Protocol')))
    $protocol=Get-Content (Join-Path $evidence 'Protocol/verification.json') -Raw|ConvertFrom-Json
    if(!$protocol.Completed){throw 'Host cut/recovery protocol prerequisite failed'}
    $publish=Join-Path $protocol.RunDirectory 'publish';$result.ProtocolProof=Join-Path $evidence 'Protocol/verification.json'
    $result.ModuleSha256=(Get-FileHash (Join-Path $publish 'TrueMoon.Nom.dll')).Hash
    $stage='prerequisites'
    if(!$BaseOsImage){$result.ExecutionStatus='Blocked';$result.Failure='Prepared Windows Gen2 VHDX is required; no host/guest power action was attempted';$exitCode=2}
    elseif(!$GuestCredential){$result.ExecutionStatus='Blocked';$result.Failure='GuestCredential is required via local Get-Credential, never plaintext/chat';$exitCode=2}
    else{
        if(![OperatingSystem]::IsWindows() -or !([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run VM controller in an Administrator PowerShell (UAC)'}
        $base=[IO.Path]::GetFullPath($BaseOsImage)
        if(!(Test-Path -LiteralPath $base -PathType Leaf) -or [IO.Path]::GetExtension($base) -ne '.vhdx'){throw 'Explicit prepared VHDX file required'}
        for($part=$base;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Base-image reparse path rejected'}}
        $descriptor=Get-VHD -Path $base -ErrorAction Stop
        if($descriptor.Attached -or $descriptor.VhdType -eq 'Differencing' -or $descriptor.Size -lt 8GB -or $descriptor.Size -gt 256GB){throw 'Base must be detached standalone8–256GiB VHDX'}
        Host-Budget
        # Hold a read-only parent lease throughout: the user's source image cannot be written.
        $parentLease=[IO.FileStream]::new($base,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        $result.BaseImage=@{Path=$base;Sha256=(Get-FileHash -LiteralPath $base).Hash;DiskIdentifier=[string]$descriptor.DiskIdentifier}
        [void][IO.Directory]::CreateDirectory($vmRoot)
        Write-NomVmDurableJson (Join-Path $vmRoot 'owner.json') $owner
        New-VHD -Path $osDisk -ParentPath $base -Differencing -ErrorAction Stop|Out-Null
        # Prepare only a new, identity-checked data VHDX on the host before attaching it to a VM.
        $data=New-VHD -Path $dataDisk -Fixed -SizeBytes 128MB -ErrorAction Stop;$dataCreated=$true;$dataId=[Guid]$data.DiskIdentifier
        $dataMountAttempted=$true;$mounted=Mount-VHD -Path $dataDisk -PassThru -ErrorAction Stop;$disk=@($mounted|Get-Disk -ErrorAction Stop)
        if($disk.Count -ne 1){throw 'One new data disk expected'};$number=$disk[0].Number;$unique=$disk[0].UniqueId
        Assert-DurabilityDisk (Get-VHD -Path $dataDisk) $disk[0] $dataDisk $dataId $number $unique -Raw
        $disk[0]|Initialize-Disk -PartitionStyle GPT -ErrorAction Stop|Out-Null
        $disk=Get-Disk -Number $number;Assert-DurabilityDisk (Get-VHD -Path $dataDisk) $disk $dataDisk $dataId $number $unique
        $partition=$disk|New-Partition -UseMaximumSize -AssignDriveLetter -ErrorAction Stop
        Assert-DurabilityPartition $partition $disk $number
        $partition|Format-Volume -FileSystem NTFS -NewFileSystemLabel ('NOM7_'+$token.Substring(0,24)) -Confirm:$false -ErrorAction Stop|Out-Null
        $partition=Get-Partition -DiskNumber $number -PartitionNumber $partition.PartitionNumber
        $partitionGuid=[string]$partition.Guid
        Write-NomVmDurableJson (Join-Path ([string]$partition.DriveLetter+':\') '.nom-vm-owner.json') @{Token=$token;DiskIdentifier=[string]$dataId}
        Dismount-VHD -Path $dataDisk -ErrorAction Stop;$dataMountAttempted=$false
        $stage='create-vm'
        $vm=New-VM -Name $owner.Name -Generation 2 -MemoryStartupBytes 2GB -VHDPath $osDisk -Path $vmRoot -ErrorAction Stop
        $owner.VmId=[Guid]$vm.Id;$vmCreated=$true
        $vm|Set-VM -AutomaticCheckpointsEnabled $false -CheckpointType Disabled -AutomaticStartAction Nothing -AutomaticStopAction TurnOff -ErrorAction Stop
        $vm|Set-VMProcessor -Count 2 -ErrorAction Stop
        $vm|Get-VMNetworkAdapter|Remove-VMNetworkAdapter -Confirm:$false -ErrorAction Stop
        $vm|Add-VMHardDiskDrive -Path $dataDisk -ErrorAction Stop
        Write-NomVmDurableJson (Join-Path $evidence 'owned-vm.json') $owner
        $vm=Current-VM;Start-VM -VM $vm -ErrorAction Stop|Out-Null;$beforeBoot=Boot-Guest
        $bridge=Guest {
            param($token,$guid)
            $ErrorActionPreference='Stop'
            $parts=@(Get-Partition|Where-Object {[string]$_.Guid -and [Guid]$_.Guid -eq [Guid]$guid})
            if($parts.Count -ne 1 -or $parts[0].IsBoot -or $parts[0].IsSystem -or $parts[0].Size -lt 64MB -or $parts[0].Size -gt 128MB){throw 'Owned guest data partition missing/unsafe'}
            $part=$parts[0];$disk=$part|Get-Disk
            if($disk.IsBoot -or $disk.IsSystem -or $disk.Size -ne 128MB){throw 'Guest data disk role mismatch'}
            if(!$part.DriveLetter){$part|Add-PartitionAccessPath -AssignDriveLetter -ErrorAction Stop;$part=Get-Partition -DiskNumber $part.DiskNumber -PartitionNumber $part.PartitionNumber}
            $root=[string]$part.DriveLetter+':\';$volume=$part|Get-Volume
            if($volume.FileSystem -ne 'NTFS' -or $volume.FileSystemLabel -ne ('NOM7_'+$token.Substring(0,24))){throw 'Guest data volume label mismatch'}
            $marker=Get-Content -LiteralPath (Join-Path $root '.nom-vm-owner.json') -Raw|ConvertFrom-Json
            if($marker.Token -cne $token){throw 'Guest data owner mismatch'}
            $dotnet=(Get-Command dotnet -ErrorAction Stop).Source
            if(!(& $dotnet --list-runtimes|Where-Object {$_ -match '^Microsoft.NETCore.App 10\.'})){throw 'Guest .NET10 runtime required'}
            $bridge=Join-Path $env:ProgramData ('NomVmCrash\'+$token);if(Test-Path $bridge){throw 'Guest bridge must be new'}
            [void][IO.Directory]::CreateDirectory($bridge)
            @{Bridge=$bridge;DataRoot=$root;Dotnet=$dotnet}
        } @($token,$partitionGuid)
        $archive=Join-Path $evidence 'probe.zip';Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive
        $bytes=[IO.File]::ReadAllBytes($archive);if($bytes.Length -gt 16MB){throw 'Guest bundle budget16MiB'}
        $guestZip=Join-Path $bridge.Bridge 'probe.zip'
        Guest {param($p);$f=[IO.File]::Open($p,[IO.FileMode]::CreateNew);$f.Dispose()} @($guestZip)|Out-Null
        for($offset=0;$offset -lt $bytes.Length;$offset+=49152){$count=[Math]::Min(49152,$bytes.Length-$offset);$chunk=[Convert]::ToBase64String($bytes,$offset,$count)
            Guest {param($p,$b);$f=[IO.File]::Open($p,[IO.FileMode]::Append);try{$a=[Convert]::FromBase64String($b);$f.Write($a,0,$a.Length)}finally{$f.Dispose()}} @($guestZip,$chunk)|Out-Null
        }
        $bundleHash=(Get-FileHash $archive).Hash
        Guest {param($p,$hash,$dir);if((Get-FileHash $p).Hash -cne $hash){throw 'Guest bundle hash differs'};Expand-Archive -LiteralPath $p -DestinationPath (Join-Path $dir 'probe') -ErrorAction Stop} @($guestZip,$bundleHash,$bridge.Bridge)|Out-Null
        $stage='vm-trials'
        foreach($phase in @(Get-NomVmPhases)){
            Host-Budget;$trial=[Guid]::NewGuid();$folder=Join-Path $evidence $phase;[void][IO.Directory]::CreateDirectory($folder)
            $config=[ordered]@{Schema=1;TrialId=$trial.ToString('D');Phase=$phase;Root=(Join-Path $bridge.DataRoot ('trial-'+$trial.ToString('N')));Signal=(Join-Path $bridge.Bridge ($phase+'-ready.json'));BootId=[string]$beforeBoot;
                ProjectId=[Guid]::NewGuid().ToString('D');StoreId=[Guid]::NewGuid().ToString('D');OldId=[Guid]::NewGuid().ToString('D');NewId=[Guid]::NewGuid().ToString('D');SourceId=[Guid]::NewGuid().ToString('D');KindId=[Guid]::NewGuid().ToString('D');HoldSeconds=180}
            Write-NomVmDurableJson (Join-Path $folder 'case.json') $config
            $guestConfig=Join-Path $bridge.Bridge ($phase+'-case.json');$json=$config|ConvertTo-Json -Depth 5
            $launch=Guest {
                param($path,$json,$dir,$dotnet,$phase)
                $ErrorActionPreference='Stop';if(Test-Path $path){throw 'Guest case already exists'};[IO.File]::WriteAllText($path,$json)
                $dll=Join-Path $dir 'probe\TrueMoon.Nom.Probe.dll'
                $p=Start-Process -FilePath $dotnet -WindowStyle Hidden -ArgumentList @(('"'+$dll+'"'),'vm-child',('"'+$path+'"')) -RedirectStandardOutput (Join-Path $dir ($phase+'.stdout.log')) -RedirectStandardError (Join-Path $dir ($phase+'.stderr.log')) -PassThru
                @{Id=$p.Id;StartUtc=$p.StartTime.ToUniversalTime().ToString('O')}
            } @($guestConfig,$json,$bridge.Bridge,$bridge.Dotnet,$phase)
            $ready=$null;$watch=[Diagnostics.Stopwatch]::StartNew()
            while(!$ready -and $watch.Elapsed.TotalSeconds -lt 60){
                $ready=Guest {param($signal,$id,$start);$ErrorActionPreference='Stop';$p=Get-Process -Id $id -ErrorAction Stop;if($p.StartTime.ToUniversalTime().ToString('O') -cne $start){throw 'Guest worker identity changed'};if(Test-Path $signal){[IO.File]::ReadAllText($signal)}} @($config.Signal,$launch.Id,$launch.StartUtc)
                if(!$ready){Start-Sleep -Milliseconds 200}
            }
            if(!$ready){throw 'Guest did not reach exact cutpoint'}
            $witness=$ready|ConvertFrom-Json;Assert-NomVmWitness $witness ([pscustomobject]$config) ([string]$beforeBoot) $result.ModuleSha256
            if($witness.ProcessId -ne $launch.Id -or $witness.ProcessStartUtc -cne $launch.StartUtc){throw 'Ready signal does not belong to live owned worker'}
            Write-NomVmDurableJson (Join-Path $folder 'host-witness.json') $witness
            # Witness is now durable outside the VM. Cut only this fresh owned VM, never the host.
            Turn-OffOwnedVM -Measured
            $cut=[ordered]@{VmId=[string]$owner.VmId;TrialId=[string]$trial;Phase=$phase;BeforeBoot=[string]$beforeBoot;TurnOffRequested=$true;ObservedState='Off';Utc=[DateTime]::UtcNow.ToString('O')}
            Write-NomVmDurableJson (Join-Path $folder 'host-cut.json') $cut
            Copy-RawData (Join-Path $folder 'before-recovery.vhdx')
            Start-VM -VM (Current-VM) -ErrorAction Stop|Out-Null;$afterBoot=Boot-Guest
            Assert-NomVmBootTransition ([pscustomobject]$cut) ([string]$afterBoot)
            $guestWitness=Join-Path $bridge.Bridge ($phase+'-host-witness.json');$wjson=$witness|ConvertTo-Json -Depth 16
            Guest {param($p,$json);if(Test-Path $p){throw 'Expected witness must be new'};[IO.File]::WriteAllText($p,$json)} @($guestWitness,$wjson)|Out-Null
            # Expected configuration also comes from the host ledger, not potentially lost guest OS writes.
            $recoveryConfig=Join-Path $bridge.Bridge ($phase+'-recovery-case.json')
            Guest {
                param($path,$json,$root,$guid,$token)
                $ErrorActionPreference='Stop'
                $parts=@(Get-Partition|Where-Object {[string]$_.Guid -and [Guid]$_.Guid -eq [Guid]$guid})
                if($parts.Count -ne 1 -or ([string]$parts[0].DriveLetter+':\') -cne $root -or $parts[0].IsBoot -or $parts[0].IsSystem){throw 'Reboot data partition mapping differs'}
                $marker=Get-Content -LiteralPath (Join-Path $root '.nom-vm-owner.json') -Raw|ConvertFrom-Json
                if($marker.Token -cne $token -or (Test-Path $path)){throw 'Recovery data ownership/config mismatch'}
                [IO.File]::WriteAllText($path,$json)
            } @($recoveryConfig,$json,$bridge.DataRoot,$partitionGuid,$token)|Out-Null
            $guestReport=Join-Path $bridge.Bridge ($phase+'-recovery.json')
            $recovered=Guest {
                param($dir,$dotnet,$config,$witness,$report)
                $ErrorActionPreference='Stop';$dll=Join-Path $dir 'probe\TrueMoon.Nom.Probe.dll'
                & $dotnet $dll vm-verify $config $witness $report|Out-Null
                if($LASTEXITCODE -ne 0){throw 'Guest exact recovery verification failed'}
                [IO.File]::ReadAllText($report)
            } @($bridge.Bridge,$bridge.Dotnet,$recoveryConfig,$guestWitness,$guestReport) 30
            $proof=$recovered|ConvertFrom-Json;if(!$proof.Completed){throw 'Recovery report incomplete'}
            Write-NomVmDurableJson (Join-Path $folder 'recovery.json') $proof
            Write-NomVmDurableJson (Join-Path $folder 'boot-transition.json') @{Before=$beforeBoot;After=$afterBoot;VmId=[string]$owner.VmId;TrialId=[string]$trial}
            $result.Trials+=@{Phase=$phase;TrialId=[string]$trial;BeforeBoot=$beforeBoot;AfterBoot=$afterBoot;RecoveryChecks=$proof.Checks.Count;RawImage=Join-Path $folder 'before-recovery.vhdx';RawSha256=(Get-FileHash (Join-Path $folder 'before-recovery.vhdx')).Hash}
            $beforeBoot=$afterBoot;Save-Result
        }
        $result.VmCrashTested=$true;$result.ExecutionStatus='Passed';$result.Completed=$true;$exitCode=0
    }
}catch{$result.Failure=$_.ToString();$result.ExecutionStatus='Failed';$exitCode=1}
finally{
    if($vmCreated){try{Turn-OffOwnedVM;$result.VmStopped=$true}catch{$result.CleanupFailure=$_.ToString();$result.Completed=$false;$result.ExecutionStatus='CleanupFailed';$exitCode=1}}
    if($dataCreated -and $dataMountAttempted){try{$current=Get-VHD -Path $dataDisk;if([Guid]$current.DiskIdentifier -ne $dataId){throw 'Data cleanup identity mismatch'};if($current.Attached){Dismount-VHD -Path $dataDisk -ErrorAction Stop}}catch{$result.CleanupFailure=$_.ToString();$exitCode=1}}
    if($parentLease){$parentLease.Dispose()}
    foreach($file in $result.Sources){if((Get-FileHash $file.Path).Hash -ne $file.Sha256){$result.Failure='VM controller source drift';$result.Completed=$false;$exitCode=1}}
    if($protocol){foreach($file in $protocol.Sources){if((Get-FileHash $file.Path).Hash -ne $file.Sha256){$result.Failure='VM protocol source drift';$result.Completed=$false;$exitCode=1}}}
    Save-Result
}
Write-Output "VM crash harness: $($result.ExecutionStatus); trials=$($result.Trials.Count); hardware power excluded. See $evidence/vm-crash.json"
exit $exitCode
