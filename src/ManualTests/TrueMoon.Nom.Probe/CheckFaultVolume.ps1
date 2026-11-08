[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')+'\'
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(!$evidence.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must stay inside this workspace.'}
if(Test-Path -LiteralPath $evidence){throw 'Use a new evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$path=Join-Path $evidence ('isolated-'+[Guid]::NewGuid().ToString('N')+'.vhdx')
$result=[ordered]@{Attempted=$false;Command='New-VHD -Dynamic -SizeBytes 67108864';Path=$path;
    Administrator=([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator);
    Created=$false;Mounted=$false;Formatted=$false;PhysicalDiskFullTested=$false;Gate='unavailable';Error=$null}
try{
    if(!(Get-Command New-VHD -ErrorAction SilentlyContinue)){throw 'Hyper-V New-VHD is unavailable.'}
    $result.Attempted=$true
    New-VHD -Path $path -Dynamic -SizeBytes 64MB -ErrorAction Stop|Out-Null
    $result.Created=$true;$result.Gate='volume-creation-available; physical fill/recovery acceptance still required'
}catch{$result.Error=$_.ToString();$result.ErrorId=$_.FullyQualifiedErrorId}
finally{$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $evidence 'volume-capability.json') -Encoding utf8}
# This capability check never attaches/formats/fills a host volume. A created empty VHDX is retained as evidence.
$result
