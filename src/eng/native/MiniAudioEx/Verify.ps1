[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$Device,
    [switch]$Audible,
    [string]$SourceArchive,
    [string]$ManagedArchive
)
$ErrorActionPreference='Stop'
if($Audible -and !$Device){throw '-Audible requires -Device. Coordinate listening before starting audible playback.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory,$workspace)
if(Test-Path -LiteralPath $evidence){throw 'Choose a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$native=Join-Path $evidence 'native'
$buildArguments=@{OutputDirectory=$native}
if($SourceArchive){$buildArguments.SourceArchive=[IO.Path]::GetFullPath($SourceArchive)}
if($ManagedArchive){$buildArguments.ManagedArchive=[IO.Path]::GetFullPath($ManagedArchive)}
& (Join-Path $PSScriptRoot 'Build.ps1') @buildArguments
$receiptPath=Join-Path $native 'native-build.json'
$receipt=Get-Content -Raw -LiteralPath $receiptPath|ConvertFrom-Json
if(!$receipt.Completed){throw 'Native build did not complete.'}
$arguments=@{
    EvidenceDirectory=(Join-Path $evidence 'consumer');PatchedPackage=$receipt.Package.Path
    NativeBuildReceipt=$receiptPath;Device=$Device;MutedDevice=(!$Audible)
}
& (Join-Path $workspace 'ManualTests/MiniAudioExResearch/Verify.ps1') @arguments
Write-Host "Patched dependency verification completed: $evidence"
