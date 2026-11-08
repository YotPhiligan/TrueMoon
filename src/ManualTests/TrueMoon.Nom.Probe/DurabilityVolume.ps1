# Pure admission guards shared by the physical runner and its non-privileged tests.
Set-StrictMode -Version Latest
function Assert-DurabilityPath([string]$Workspace,[string]$Evidence,[string]$VhdPath,[string]$Token){
    if($Token -notmatch '^[a-f0-9]{32}$'){throw 'Invalid volume token.'}
    $base=[IO.Path]::GetFullPath($Workspace).TrimEnd('\')+'\';$folder=[IO.Path]::GetFullPath($Evidence)
    if(!$folder.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or $folder -eq $base.TrimEnd('\')){throw 'Evidence must be a workspace child.'}
    $expected=Join-Path $folder ("isolated-$Token.vhdx")
    if(![string]::Equals([IO.Path]::GetFullPath($VhdPath),$expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Only the new owned VHDX is allowed.'}
    for($part=$folder;$part;$part=[IO.Path]::GetDirectoryName($part)){
        if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse ancestor rejected.'}
    }
}
function Assert-DurabilityDisk($Vhd,$Disk,[string]$Path,[Guid]$Identifier,[int]$Number,[string]$UniqueId,[switch]$Raw){
    if($Identifier -eq [Guid]::Empty -or ![string]::Equals([IO.Path]::GetFullPath($Vhd.Path),[IO.Path]::GetFullPath($Path),[StringComparison]::OrdinalIgnoreCase) -or
        [Guid]$Vhd.DiskIdentifier -ne $Identifier -or !$Vhd.Attached -or $Vhd.DiskNumber -ne $Number -or $Disk.Number -ne $Number){throw 'Owned VHD/disk identity mismatch.'}
    if($Disk.IsBoot -or $Disk.IsSystem -or $Disk.Size -ne 128MB -or $Vhd.Size -ne 128MB -or [string]::IsNullOrWhiteSpace($UniqueId) -or $Disk.UniqueId -ne $UniqueId){throw 'Unsafe disk identity, size or role.'}
    if($Raw -and ($Disk.PartitionStyle -ne 'RAW' -or $Disk.NumberOfPartitions -ne 0)){throw 'Only a fresh RAW disk may be initialized.'}
}
function Assert-DurabilityPartition($Partition,$Disk,[int]$Number){
    if($Partition.DiskNumber -ne $Number -or $Disk.Number -ne $Number -or $Disk.IsBoot -or $Disk.IsSystem -or $Partition.Size -lt 64MB -or $Partition.Size -gt 128MB -or
        $Partition.DriveLetter -notmatch '^[A-Z]$' -or $Partition.IsBoot -or $Partition.IsSystem){throw 'Unsafe partition selection.'}
}
