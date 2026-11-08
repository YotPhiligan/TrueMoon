# Pure controller admission; no Hyper-V operation is hidden in these functions.
Set-StrictMode -Version Latest
function Get-NomVmPhases { @('commit-ack','cas-finalized','journal-partial','checkpoint-ready','root-switched','backup-published','restore-published') }
function Assert-NomVmPath([string]$Root,[string]$Path){
    $prefix=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\';$full=[IO.Path]::GetFullPath($Path)
    if(!$full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'VM artifact must stay under its owned root'}
    for($part=$full;$part;$part=[IO.Path]::GetDirectoryName($part)){
        if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'VM reparse ancestor rejected'}
    }
}
function Assert-NomVmIdentity($VM,$Owner,$Disks,$Snapshots,$Adapters){
    if($Owner.Token -cnotmatch '^[a-f0-9]{32}$'){throw 'Invalid VM ownership token'}
    if([Guid]$Owner.VmId -eq [Guid]::Empty -or [Guid]$VM.Id -ne [Guid]$Owner.VmId -or $VM.Name -cne $Owner.Name -or $VM.Name -cne ('NomCrash-'+$Owner.Token)) {throw 'Foreign VM identity rejected'}
    Assert-NomVmPath $Owner.Root $VM.Path
    if($VM.Generation -ne 2 -or $VM.AutomaticCheckpointsEnabled -or $VM.CheckpointType -ne 'Disabled' -or @($Snapshots).Count -ne 0 -or @($Adapters).Count -ne 0){throw 'VM generation/checkpoint/network admission failed'}
    if($VM.State -notin @('Off','Running','Starting','Stopping')){throw 'Saved/paused/unknown VM is not admitted'}
    $expected=@([IO.Path]::GetFullPath($Owner.OsDisk),[IO.Path]::GetFullPath($Owner.DataDisk)|Sort-Object)
    $actual=@($Disks|ForEach-Object{[IO.Path]::GetFullPath($_.Path)}|Sort-Object)
    if($actual.Count -ne 2 -or (Compare-Object $expected $actual)){throw 'VM disks differ from the two owned disks'}
    foreach($path in $actual){Assert-NomVmPath $Owner.Root $path}
}
function Assert-NomVmWitness($Witness,$Expected,[string]$BootId,[string]$ModuleSha256){
    if($Witness.Schema -ne 1 -or $Witness.Configuration.Schema -ne 1 -or [Guid]$Witness.Configuration.TrialId -ne [Guid]$Expected.TrialId -or
       $Witness.Configuration.Phase -cne $Expected.Phase -or $Witness.Configuration.BootId -cne $BootId -or $Witness.ModuleSha256 -cne $ModuleSha256 -or $Witness.ProcessId -le 0){throw 'Stale/foreign VM witness rejected'}
    foreach($name in @('Root','Signal','ProjectId','StoreId','OldId','NewId','SourceId','KindId','HoldSeconds')){if([string]$Witness.Configuration.$name -cne [string]$Expected.$name){throw "Witness configuration mismatch: $name"}}
    if($Expected.Phase -notin @(Get-NomVmPhases) -or $Witness.OldTicket.StoreId -ne $Expected.StoreId -or $Witness.OldVersion.AssetId -ne $Expected.OldId -or $Witness.CandidateVersion.AssetId -ne $Expected.NewId){throw 'Witness asset/ticket identity mismatch'}
    if($Witness.OldTicket.Sequence -le 0 -or $Witness.OldTicket.RequestDigest -cnotmatch '^[a-f0-9]{64}$' -or $Witness.OldVersion.Length -ne 32785 -or $Witness.CandidateVersion.Length -ne 65553 -or
        $Witness.OldVersion.KindId -ne $Expected.KindId -or $Witness.CandidateVersion.KindId -ne $Expected.KindId -or $Witness.OldVersion.Revision -cnotmatch '^[a-f0-9]{64}$' -or $Witness.CandidateVersion.Revision -cnotmatch '^[a-f0-9]{64}$'){throw 'Witness ticket/resource shape mismatch'}
    if($Expected.Phase -in @('commit-ack','cas-finalized','journal-partial') -and ($null -eq $Witness.NewTicket -or $Witness.NewTicket.StoreId -ne $Expected.StoreId -or $Witness.NewTicket.Sequence -le $Witness.OldTicket.Sequence -or $Witness.NewTicket.RequestDigest -cnotmatch '^[a-f0-9]{64}$')){throw 'Missing/invalid issued candidate ticket'}
    if($Witness.AcknowledgedGeneration -ne $(if($Expected.Phase -eq 'commit-ack'){2}else{1})){throw 'Witness acknowledged generation mismatch'}
    if($Witness.OldVersion.Digest -cne (Get-NomVmPatternDigest $false) -or $Witness.CandidateVersion.Digest -cne (Get-NomVmPatternDigest $true)){throw 'Witness differs from independent host bytes'}
}
function Get-NomVmPatternDigest([bool]$Candidate){
    $length=if($Candidate){65553}else{32785};$salt=if($Candidate){43}else{19};$bytes=[byte[]]::new($length)
    for($i=0;$i -lt $length;$i++){$bytes[$i]=[byte](($i*73+$salt)%256)}
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Assert-NomVmBootTransition($Cut,[string]$AfterBoot){
    if(!$Cut.TurnOffRequested -or $Cut.ObservedState -ne 'Off' -or [Guid]$Cut.VmId -eq [Guid]::Empty -or [Guid]$Cut.TrialId -eq [Guid]::Empty -or
        [string]::IsNullOrWhiteSpace($Cut.BeforeBoot) -or [string]::IsNullOrWhiteSpace($AfterBoot) -or $Cut.BeforeBoot -ceq $AfterBoot){throw 'A process restart/save/resume cannot attest VM crash'}
}
function Write-NomVmDurableJson([string]$Path,$Value){
    $bytes=[Text.Encoding]::UTF8.GetBytes(($Value|ConvertTo-Json -Depth 16))
    if($bytes.Length -gt 65536){throw 'Controller ledger document exceeds64KiB'}
    $file=[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    try{$file.Write($bytes);$file.Flush($true)}finally{$file.Dispose()}
}
