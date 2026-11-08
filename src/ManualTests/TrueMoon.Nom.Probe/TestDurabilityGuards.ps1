[CmdletBinding()]
param([Parameter(Mandatory)][string]$Report)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Report){throw 'Use a fresh guard report.'}
. (Join-Path $PSScriptRoot 'DurabilityVolume.ps1')
$passed=[Collections.Generic.List[string]]::new()
function Reject([string]$Name,[scriptblock]$Action){$rejected=$false;try{& $Action}catch{$rejected=$true};if(!$rejected){throw "Guard accepted unsafe case: $Name"};$passed.Add($Name)}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$token='0123456789abcdef0123456789abcdef';$folder=Join-Path $workspace 'TestResults/GuardOnly';$path=Join-Path $folder "isolated-$token.vhdx"
Assert-DurabilityPath $workspace $folder $path $token;$passed.Add('OwnedPathAcceptedWithoutMutation')
Reject 'WorkspaceRootRejected' {Assert-DurabilityPath $workspace $workspace $path $token}
Reject 'SiblingPrefixRejected' {Assert-DurabilityPath $workspace ($workspace+'-other') $path $token}
Reject 'EscapedVhdRejected' {Assert-DurabilityPath $workspace $folder (Join-Path $workspace 'existing.vhdx') $token}
Reject 'WrongTokenRejected' {Assert-DurabilityPath $workspace $folder $path '../escape'}
$id=[Guid]::NewGuid();$vhd=@{Path=$path;DiskIdentifier=$id;Attached=$true;DiskNumber=27;Size=128MB};$disk=@{Number=27;UniqueId='owned-virtual-disk';IsBoot=$false;IsSystem=$false;Size=128MB;PartitionStyle='RAW';NumberOfPartitions=0}
function Guard($v=$vhd,$d=$disk){Assert-DurabilityDisk ([pscustomobject]$v) ([pscustomobject]$d) $path $id 27 'owned-virtual-disk' -Raw}
Guard;$passed.Add('OwnedRawDiskAcceptedWithoutMutation')
foreach($test in @(@('BootDiskRejected','IsBoot',$true),@('SystemDiskRejected','IsSystem',$true),@('LargeDiskRejected','Size',1TB),@('DiskNumberChangedRejected','Number',28),@('UniqueIdChangedRejected','UniqueId','host-disk'),@('ExistingPartitionsRejected','NumberOfPartitions',1),@('ExistingGptRejected','PartitionStyle','GPT'))){
    $copy=$disk.Clone();$copy[$test[1]]=$test[2];Reject $test[0] {Guard $vhd $copy}
}
foreach($test in @(@('VhdPathChangedRejected','Path',(Join-Path $folder 'other.vhdx')),@('VhdIdentityChangedRejected','DiskIdentifier',[Guid]::NewGuid()),@('VhdDetachedRejected','Attached',$false),@('VhdNumberChangedRejected','DiskNumber',28),@('VhdSizeChangedRejected','Size',256MB))){
    $copy=$vhd.Clone();$copy[$test[1]]=$test[2];Reject $test[0] {Guard $copy $disk}
}
$partition=@{DiskNumber=27;Size=96MB;DriveLetter='X';IsBoot=$false;IsSystem=$false}
Assert-DurabilityPartition ([pscustomobject]$partition) ([pscustomobject]$disk) 27;$passed.Add('OwnedPartitionAcceptedWithoutMutation')
foreach($test in @(@('PartitionWrongDiskRejected','DiskNumber',0),@('PartitionBootRejected','IsBoot',$true),@('PartitionSystemRejected','IsSystem',$true),@('PartitionHugeRejected','Size',1TB),@('PartitionNoLetterRejected','DriveLetter',''))){
    $copy=$partition.Clone();$copy[$test[1]]=$test[2];Reject $test[0] {Assert-DurabilityPartition ([pscustomobject]$copy) ([pscustomobject]$disk) 27}
}
[ordered]@{Completed=$true;Scope='Pure safety admission, no disk cmdlets or full-volume writes';Checks=$passed;PhysicalDiskFullTested=$false;PowerLossTested=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $Report -Encoding utf8
Write-Output "$($passed.Count) durability admission guards passed; physical gates unchanged."
