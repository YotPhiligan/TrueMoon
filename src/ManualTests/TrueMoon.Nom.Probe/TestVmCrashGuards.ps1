[CmdletBinding()]
param([Parameter(Mandatory)][string]$Report)
$ErrorActionPreference='Stop'
$reportPath=[IO.Path]::GetFullPath($Report)
if(Test-Path -LiteralPath $reportPath){throw 'Use a fresh VM guard report.'}
if(!(Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($reportPath)) -PathType Container)){throw 'VM guard report parent must exist.'}
$sources=@($PSCommandPath,(Join-Path $PSScriptRoot 'VmCrashProtocol.ps1'))|ForEach-Object{@{Path=$_;Sha256=(Get-FileHash -LiteralPath $_).Hash}}
. (Join-Path $PSScriptRoot 'VmCrashProtocol.ps1')
$passed=[Collections.Generic.List[string]]::new()
function Check([bool]$Value,[string]$Name){if(!$Value){throw "VM guard assertion failed: $Name"};$passed.Add($Name)}
function Reject([string]$Name,[scriptblock]$Action,[string]$ExpectedMessage){
    $caught=$null;try{& $Action}catch{$caught=$_}
    if($null -eq $caught){throw "Guard accepted unsafe case: $Name"}
    if($ExpectedMessage -and $caught.Exception.Message -notmatch $ExpectedMessage){throw "Wrong rejection in ${Name}: $($caught.Exception.Message)"}
    $passed.Add($Name)
}
function Clone($Value){$Value|ConvertTo-Json -Depth 16|ConvertFrom-Json -AsHashtable}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ownedRoot=Join-Path $workspace 'TestResults/VmGuardOnly'
$token='0123456789abcdef0123456789abcdef'
$owner=@{Token=$token;VmId=[Guid]::NewGuid();Name='NomCrash-'+$token;Root=$ownedRoot;OsDisk=(Join-Path $ownedRoot 'os-child.vhdx');DataDisk=(Join-Path $ownedRoot 'data.vhdx')}
$vm=@{Id=$owner.VmId;Name=$owner.Name;Path=(Join-Path $ownedRoot 'vm');Generation=2;AutomaticCheckpointsEnabled=$false;CheckpointType='Disabled';State='Off'}
$disks=@(@{Path=$owner.OsDisk},@{Path=$owner.DataDisk})
function Identity($VMValue=$vm,$OwnerValue=$owner,$DiskValues=$disks,$SnapshotValues=@(),$AdapterValues=@()){
    Assert-NomVmIdentity ([pscustomobject]$VMValue) ([pscustomobject]$OwnerValue) @($DiskValues|ForEach-Object{[pscustomobject]$_}) $SnapshotValues $AdapterValues
}
Identity;$passed.Add('OwnedIsolatedVmAndExactDisksAccepted')
Identity $vm $owner @($disks[1],$disks[0]);$passed.Add('OwnedDiskOrderDoesNotAffectIdentity')
foreach($state in 'Running','Starting','Stopping'){$copy=Clone $vm;$copy.State=$state;Identity $copy;$passed.Add('OwnedVm'+$state+'Accepted')}
foreach($test in @(
    @('ForeignVmIdRejected','Id',[Guid]::NewGuid(),'Foreign VM identity'),
    @('ForeignVmNameRejected','Name','existing-user-vm','Foreign VM identity'),
    @('CaseChangedVmNameRejected','Name',$owner.Name.ToUpperInvariant(),'Foreign VM identity'),
    @('ForeignVmPathRejected','Path',(Join-Path $workspace 'ExistingVm'),'VM artifact must stay'),
    @('GenerationOneRejected','Generation',1,'generation/checkpoint/network'),
    @('AutomaticCheckpointRejected','AutomaticCheckpointsEnabled',$true,'generation/checkpoint/network'),
    @('EnabledCheckpointTypeRejected','CheckpointType','Standard','generation/checkpoint/network'),
    @('SavedVmRejected','State','Saved','Saved/paused/unknown'),
    @('PausedVmRejected','State','Paused','Saved/paused/unknown'),
    @('UnknownVmStateRejected','State','Unknown','Saved/paused/unknown')
)){$copy=Clone $vm;$copy[$test[1]]=$test[2];Reject $test[0] {Identity $copy} $test[3]}
$copy=Clone $owner;$copy.VmId=[Guid]::Empty;Reject 'EmptyOwnedVmIdRejected' {Identity $vm $copy} 'Foreign VM identity'
$copy=Clone $owner;$copy.Token='../../escape';$copy.Name='NomCrash-'+$copy.Token;$changedVm=Clone $vm;$changedVm.Name=$copy.Name;Reject 'InvalidOwnershipTokenRejected' {Identity $changedVm $copy} 'Invalid VM ownership token'
$copy=Clone $owner;$copy.Token=$token.ToUpperInvariant();$copy.Name='NomCrash-'+$copy.Token;$changedVm=Clone $vm;$changedVm.Name=$copy.Name;Reject 'UppercaseOwnershipTokenRejected' {Identity $changedVm $copy} 'Invalid VM ownership token'
Reject 'VmCheckpointPresentRejected' {Identity $vm $owner $disks @(@{Name='saved-before-cut'}) @()} 'generation/checkpoint/network'
Reject 'ConnectedOrUnconnectedAdapterRejected' {Identity $vm $owner $disks @() @(@{SwitchName=''})} 'generation/checkpoint/network'
Reject 'MissingDataDiskRejected' {Identity $vm $owner @($disks[0])} 'VM disks differ'
Reject 'AdditionalDiskRejected' {Identity $vm $owner @($disks[0],$disks[1],@{Path=(Join-Path $ownedRoot 'extra.vhdx')})} 'VM disks differ'
Reject 'DuplicatedOsDiskRejected' {Identity $vm $owner @($disks[0],$disks[0])} 'VM disks differ'
Reject 'ForeignHostDiskRejected' {Identity $vm $owner @($disks[0],@{Path=(Join-Path $workspace 'host-boot.vhdx')})} 'VM disks differ'
$copy=Clone $owner;$copy.DataDisk=Join-Path $workspace 'host-boot.vhdx';Reject 'EscapedDiskEvenIfListedAsOwnedRejected' {Identity $vm $copy @($disks[0],@{Path=$copy.DataDisk})} 'VM artifact must stay'
Reject 'ArtifactRootItselfRejected' {Assert-NomVmPath $ownedRoot $ownedRoot} 'VM artifact must stay'
Reject 'ArtifactSiblingPrefixRejected' {Assert-NomVmPath $ownedRoot ($ownedRoot+'-other/file.json')} 'VM artifact must stay'
Reject 'ArtifactParentTraversalRejected' {Assert-NomVmPath $ownedRoot (Join-Path $ownedRoot '../foreign/file.json')} 'VM artifact must stay'

# Golden vectors independently generated by byte recurrence (v=(v+73)&255), not this helper.
$oldDigest='021119ba13bde34cae75892dfd696e0bf831dbf9480daad64018a7570173a42c'
$newDigest='adc0c3e0553ab5351cc3b29d62e00b4b7d401d549b9d3c0b6b3039749a88670a'
Check ((Get-NomVmPatternDigest $false) -ceq $oldDigest) 'OldPatternMatchesIndependentGoldenSha256'
Check ((Get-NomVmPatternDigest $true) -ceq $newDigest) 'CandidatePatternMatchesIndependentGoldenSha256'
$boot='boot-before-1';$moduleHash='A'*64
$expected=@{Schema=1;TrialId=[Guid]::NewGuid();Phase='commit-ack';Root='N:\nom-trial\store';Signal='C:\nom-bridge\ready.json';BootId=$boot;
    ProjectId=[Guid]::NewGuid();StoreId=[Guid]::NewGuid();OldId=[Guid]::NewGuid();NewId=[Guid]::NewGuid();SourceId=[Guid]::NewGuid();KindId=[Guid]::NewGuid();HoldSeconds=120}
$witness=@{Schema=1;Configuration=(Clone $expected);ProcessId=4242;ProcessStartUtc='2026-11-08T12:00:00.0000000Z';ModuleSha256=$moduleHash;
    OldTicket=@{StoreId=$expected.StoreId;Sequence=1;RequestDigest=('c'*64)};
    NewTicket=@{StoreId=$expected.StoreId;Sequence=2;RequestDigest=('d'*64)};
    OldVersion=@{AssetId=$expected.OldId;KindId=$expected.KindId;Length=32785;Revision=('a'*64);Digest=$oldDigest};
    CandidateVersion=@{AssetId=$expected.NewId;KindId=$expected.KindId;Length=65553;Revision=('b'*64);Digest=$newDigest};AcknowledgedGeneration=2}
function Witness($Value=$witness,$Expected=$expected,$Boot=$boot,$Module=$moduleHash){Assert-NomVmWitness ([pscustomobject]$Value) ([pscustomobject]$Expected) $Boot $Module}
Witness;$passed.Add('ExactCommitAcknowledgementWitnessAccepted')
foreach($phase in @('cas-finalized','journal-partial','checkpoint-ready','root-switched','backup-published','restore-published')){
    $e=Clone $expected;$e.Phase=$phase;$w=Clone $witness;$w.Configuration=Clone $e;$w.AcknowledgedGeneration=1
    if($phase -notin 'cas-finalized','journal-partial'){$w.NewTicket=$null}
    Witness $w $e;$passed.Add('Exact'+$phase+'WitnessAccepted')
}
foreach($test in @(
    @('ForeignWitnessSchemaRejected','Schema',2,'Stale/foreign'),
    @('ForeignModuleHashRejected','ModuleSha256',('B'*64),'Stale/foreign'),
    @('MissingLiveProcessRejected','ProcessId',0,'Stale/foreign'),
    @('WrongAcknowledgedGenerationRejected','AcknowledgedGeneration',1,'acknowledged generation')
)){$w=Clone $witness;$w[$test[1]]=$test[2];Reject $test[0] {Witness $w} $test[3]}
foreach($test in @(
    @('ForeignConfigurationSchemaRejected','Schema',2,'Stale/foreign'),
    @('ForeignTrialRejected','TrialId',[Guid]::NewGuid(),'Stale/foreign'),
    @('DifferentCutPhaseRejected','Phase','root-switched','Stale/foreign'),
    @('OldBootSignalRejected','BootId','boot-older','Stale/foreign'),
    @('RootMismatchRejected','Root','N:\another-store','configuration mismatch'),
    @('SignalMismatchRejected','Signal','C:\nom-bridge\other.json','configuration mismatch'),
    @('ProjectMismatchRejected','ProjectId',[Guid]::NewGuid(),'configuration mismatch'),
    @('StoreMismatchRejected','StoreId',[Guid]::NewGuid(),'configuration mismatch'),
    @('OldAssetConfigurationMismatchRejected','OldId',[Guid]::NewGuid(),'configuration mismatch'),
    @('NewAssetConfigurationMismatchRejected','NewId',[Guid]::NewGuid(),'configuration mismatch'),
    @('SourceConfigurationMismatchRejected','SourceId',[Guid]::NewGuid(),'configuration mismatch'),
    @('KindConfigurationMismatchRejected','KindId',[Guid]::NewGuid(),'configuration mismatch'),
    @('HoldBudgetConfigurationMismatchRejected','HoldSeconds',180,'configuration mismatch')
)){$w=Clone $witness;$w.Configuration[$test[1]]=$test[2];Reject $test[0] {Witness $w} $test[3]}
$e=Clone $expected;$e.Phase='unknown';$w=Clone $witness;$w.Configuration=Clone $e;Reject 'UnsupportedCutPhaseRejected' {Witness $w $e} 'asset/ticket identity'
foreach($test in @(
    @('OldTicketWrongStoreRejected','OldTicket','StoreId',[Guid]::NewGuid(),'asset/ticket identity'),
    @('OldTicketSequenceZeroRejected','OldTicket','Sequence',0,'ticket/resource shape'),
    @('OldTicketMalformedDigestRejected','OldTicket','RequestDigest','short','ticket/resource shape'),
    @('OldTicketUppercaseDigestRejected','OldTicket','RequestDigest',('C'*64),'ticket/resource shape'),
    @('OldVersionWrongAssetRejected','OldVersion','AssetId',[Guid]::NewGuid(),'asset/ticket identity'),
    @('OldVersionWrongKindRejected','OldVersion','KindId',[Guid]::NewGuid(),'ticket/resource shape'),
    @('OldVersionWrongLengthRejected','OldVersion','Length',32784,'ticket/resource shape'),
    @('OldVersionMalformedRevisionRejected','OldVersion','Revision','not-a-revision','ticket/resource shape'),
    @('OldVersionUppercaseRevisionRejected','OldVersion','Revision',('A'*64),'ticket/resource shape'),
    @('OldVersionWrongBytesRejected','OldVersion','Digest',('0'*64),'independent host bytes'),
    @('CandidateWrongAssetRejected','CandidateVersion','AssetId',[Guid]::NewGuid(),'asset/ticket identity'),
    @('CandidateWrongKindRejected','CandidateVersion','KindId',[Guid]::NewGuid(),'ticket/resource shape'),
    @('CandidateWrongLengthRejected','CandidateVersion','Length',65554,'ticket/resource shape'),
    @('CandidateMalformedRevisionRejected','CandidateVersion','Revision','not-a-revision','ticket/resource shape'),
    @('CandidateUppercaseRevisionRejected','CandidateVersion','Revision',('B'*64),'ticket/resource shape'),
    @('CandidateWrongBytesRejected','CandidateVersion','Digest',('f'*64),'independent host bytes'),
    @('CandidateTicketWrongStoreRejected','NewTicket','StoreId',[Guid]::NewGuid(),'issued candidate ticket'),
    @('CandidateTicketReusedSequenceRejected','NewTicket','Sequence',1,'issued candidate ticket'),
    @('CandidateTicketMalformedDigestRejected','NewTicket','RequestDigest','bad','issued candidate ticket'),
    @('CandidateTicketUppercaseDigestRejected','NewTicket','RequestDigest',('D'*64),'issued candidate ticket')
)){$w=Clone $witness;$w[$test[1]][$test[2]]=$test[3];Reject $test[0] {Witness $w} $test[4]}
$w=Clone $witness;$w.NewTicket=$null;Reject 'CommitAckWithoutCandidateTicketRejected' {Witness $w} 'issued candidate ticket'
foreach($phase in 'cas-finalized','journal-partial'){$e=Clone $expected;$e.Phase=$phase;$w=Clone $witness;$w.Configuration=Clone $e;$w.NewTicket=$null;$w.AcknowledgedGeneration=1;Reject ($phase+'WithoutIssuedTicketRejected') {Witness $w $e} 'issued candidate ticket'}

$cut=@{TurnOffRequested=$true;ObservedState='Off';VmId=$owner.VmId;TrialId=$expected.TrialId;BeforeBoot=$boot}
Assert-NomVmBootTransition ([pscustomobject]$cut) 'boot-after-2';$passed.Add('ExternalTurnOffOffStateAndDifferentBootAccepted')
Reject 'ProcessRestartSameBootRejected' {Assert-NomVmBootTransition ([pscustomobject]$cut) $boot} 'process restart/save/resume'
Reject 'MissingAfterBootRejected' {Assert-NomVmBootTransition ([pscustomobject]$cut) ''} 'process restart/save/resume'
foreach($test in @(
    @('NoExternalTurnOffRejected','TurnOffRequested',$false),
    @('StillRunningVmRejected','ObservedState','Running'),
    @('SavedVmInsteadOfCutRejected','ObservedState','Saved'),
    @('MissingCutVmIdentityRejected','VmId',[Guid]::Empty),
    @('MissingCutTrialIdentityRejected','TrialId',[Guid]::Empty),
    @('MissingBeforeBootRejected','BeforeBoot','')
)){$value=Clone $cut;$value[$test[1]]=$test[2];Reject $test[0] {Assert-NomVmBootTransition ([pscustomobject]$value) 'boot-after-2'} 'process restart/save/resume'}

$ledgerRoot=Join-Path ([IO.Path]::GetDirectoryName($reportPath)) ('vm-ledger-guards-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($ledgerRoot)
$ledger=Join-Path $ledgerRoot 'witness.json';$value=[ordered]@{TrialId=$expected.TrialId;Phase='commit-ack';Bytes=@(4,5,6);AcknowledgedGeneration=2}
Write-NomVmDurableJson $ledger $value;$read=Get-Content -LiteralPath $ledger -Raw|ConvertFrom-Json
Check ($read.TrialId -eq $expected.TrialId -and $read.Phase -ceq 'commit-ack' -and $read.AcknowledgedGeneration -eq 2 -and (@($read.Bytes)-join ',') -ceq '4,5,6') 'DurableLedgerRetainsExactControllerWitness'
$hash=(Get-FileHash -LiteralPath $ledger).Hash
Reject 'LedgerOverwriteRejected' {Write-NomVmDurableJson $ledger @{TrialId='replacement'}} 'already exists|уже существует|существует'
Check ((Get-FileHash -LiteralPath $ledger).Hash -ceq $hash) 'RejectedOverwritePreservesOriginalLedgerBytes'
Reject 'ExistingGuardReportRejectedBeforeTests' {& $PSCommandPath -Report $ledger} 'fresh VM guard report'
Check ((Get-FileHash -LiteralPath $ledger).Hash -ceq $hash) 'ExistingGuardReportPreservesOriginalBytes'
$emptyBytes=[Text.Encoding]::UTF8.GetByteCount((@{Data=''}|ConvertTo-Json -Depth 16));$length=65536-$emptyBytes
$boundary=Join-Path $ledgerRoot 'boundary.json';Write-NomVmDurableJson $boundary @{Data=('x'*$length)}
Check ((Get-Item -LiteralPath $boundary).Length -eq 65536 -and (Get-Content -LiteralPath $boundary -Raw|ConvertFrom-Json).Data -ceq ('x'*$length)) 'Exactly65536ByteLedgerAcceptedAndPreserved'
$oversized=Join-Path $ledgerRoot 'oversized.json';Reject 'Ledger65537BytesRejectedBeforeCreate' {Write-NomVmDurableJson $oversized @{Data=('x'*($length+1))}} 'exceeds64KiB'
Check (!(Test-Path -LiteralPath $oversized)) 'OversizedLedgerCreatesNoPartialFile'
$unicodeValue=('Ж'*[int][Math]::Floor($length/2))+('x'*($length%2))
$unicodeBoundary=Join-Path $ledgerRoot 'utf8-boundary.json';Write-NomVmDurableJson $unicodeBoundary @{Data=$unicodeValue}
Check ((Get-Item -LiteralPath $unicodeBoundary).Length -eq 65536 -and (Get-Content -LiteralPath $unicodeBoundary -Raw|ConvertFrom-Json).Data -ceq $unicodeValue) 'Utf8ByteBoundaryAcceptedWithoutCharacterLoss'
$unicodeOversized=Join-Path $ledgerRoot 'utf8-oversized.json';Reject 'Utf8ByteBudgetCannotBecomeCharacterBudget' {Write-NomVmDurableJson $unicodeOversized @{Data=($unicodeValue+'x')}} 'exceeds64KiB'
Check (!(Test-Path -LiteralPath $unicodeOversized)) 'Utf8OversizedLedgerCreatesNoPartialFile'
foreach($source in $sources){if((Get-FileHash -LiteralPath $source.Path).Hash -cne $source.Sha256){throw 'VM guard source drift during execution.'}}

[ordered]@{Completed=$true;Scope='Pure VM ownership/witness/boot/ledger admission; no Hyper-V commands or VM cuts';Checks=$passed;
    LedgerFixtures=$ledgerRoot;Sources=$sources;VmCrashTested=$false;HardwarePowerLossTested=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output "$($passed.Count) VM crash admission checks passed; no actual VM cut or hardware power loss claimed."
